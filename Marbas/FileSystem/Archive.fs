namespace Marbas.FileSystem

open System
open System.IO
open System.IO.Abstractions
open System.IO.Compression
open System.Formats.Tar
open ICSharpCode.SharpZipLib.Zip
open SharpCompress.Common
open SharpCompress.Writers.SevenZip
open FsToolkit.ErrorHandling

module Archive =

    [<Literal>]
    let private AesKeySize = 256

    [<Literal>]
    let private RewriteSuffix = ".marbas-new"

    type private Sink =
        { AddDirectory: string -> unit
          AddFile: string -> FileSystemPath -> unit }

    let private attempt path action =
        Operations.attempting path action |> Result.ignore

    let private entryName (fs: IFileSystem) (root: FileSystemPath) (path: FileSystemPath) =
        fs.Path
            .GetRelativePath(FileSystemPath.value root, FileSystemPath.value path)
            .Replace(fs.Path.DirectorySeparatorChar, '/')

    let private modifiedAt (fs: IFileSystem) (path: FileSystemPath) =
        DateTimeOffset(fs.FileInfo.New(FileSystemPath.value path).LastWriteTime)

    let private reading (fs: IFileSystem) (path: FileSystemPath) =
        fs.File.OpenRead(FileSystemPath.value path)

    let private zipLevel compression =
        match compression with
        | Stored -> 0
        | Normal -> 6
        | Maximum -> 9

    let private gzipLevel compression =
        match compression with
        | Stored -> CompressionLevel.NoCompression
        | Normal -> CompressionLevel.Optimal
        | Maximum -> CompressionLevel.SmallestSize

    let private intoZip (fs: IFileSystem) (request: ArchiveRequest) (stream: Stream) pack =
        use zip = new ZipOutputStream(stream)
        zip.SetLevel(zipLevel request.Compression)

        request.Password
        |> Option.iter (fun password -> zip.Password <- ArchivePassword.value password)

        pack
            { AddDirectory =
                fun name ->
                    zip.PutNextEntry(ZipEntry(name + "/"))
                    zip.CloseEntry()
              AddFile =
                fun name path ->
                    let entry =
                        ZipEntry(
                            name,
                            DateTime = (modifiedAt fs path).LocalDateTime,
                            Size = fs.FileInfo.New(FileSystemPath.value path).Length
                        )

                    if request.Password.IsSome then
                        entry.AESKeySize <- AesKeySize

                    zip.PutNextEntry entry
                    use content = reading fs path
                    content.CopyTo zip
                    zip.CloseEntry() }
    let private intoTarGz (fs: IFileSystem) compression (stream: Stream) pack =
        use compressed = new GZipStream(stream, gzipLevel compression)
        use tar = new TarWriter(compressed, TarEntryFormat.Pax, true)

        pack
            { AddDirectory = fun name -> tar.WriteEntry(PaxTarEntry(TarEntryType.Directory, name + "/"))
              AddFile =
                fun name path ->
                    use content = reading fs path

                    PaxTarEntry(
                        TarEntryType.RegularFile,
                        name,
                        DataStream = content,
                        ModificationTime = modifiedAt fs path
                    )
                    |> tar.WriteEntry }

    let private keyIn (level: ArchivePath) (name: string) =
        match ArchivePath.value level with
        | "" -> name
        | prefix -> $"{prefix}/{name}"

    let private lzmaLevel compression =
        match compression with
        | Stored -> 0
        | Normal -> 5
        | Maximum -> 9

    let private sevenZipSink (fs: IFileSystem) (writer: SevenZipWriter) (level: ArchivePath) =
        { AddDirectory = fun name -> writer.WriteDirectory(keyIn level name, Nullable())
          AddFile =
            fun name path ->
                use content = reading fs path
                writer.Write(keyIn level name, content, Nullable (modifiedAt fs path).LocalDateTime) }

    let private intoSevenZipStream (fs: IFileSystem) compression (stream: Stream) pack =
        use writer =
            new SevenZipWriter(
                stream,
                SevenZipWriterOptions(CompressionType.LZMA2, CompressionLevel = lzmaLevel compression, LeaveStreamOpen = true)
            )

        pack (sevenZipSink fs writer ArchivePath.root)

    let rec private add (fs: IFileSystem) (control: Operations.BatchControl) sink root source =
        if control.IsCancelled() then
            Error(Operations.Cancelled source)
        else
            match FileSystemPath.kind fs source with
            | NoEntry -> Error(Operations.SourceNotFound source)
            | DirectoryEntry ->
                result {
                    do! attempt source (fun () -> sink.AddDirectory(entryName fs root source))

                    let! children =
                        Operations.attempting source (fun () ->
                            fs.Directory.EnumerateFileSystemEntries(FileSystemPath.value source)
                            |> Seq.map FileSystemPath.create
                            |> List.ofSeq)

                    do!
                        children
                        |> List.traverseResultM (add fs control sink root)
                        |> Result.ignore
                }
            | FileEntry ->
                control.Report source
                attempt source (fun () -> sink.AddFile (entryName fs root source) source)

    let private packAll (fs: IFileSystem) control sources sink =
        sources
        |> Operations.allWatched fs control (fun source -> add fs control sink (FileSystemPath.directory fs source) source)

    let private packInto fs control (request: ArchiveRequest) sources stream =
        match ArchiveFormat.kind request.Format with
        | Zip -> intoZip fs request stream (packAll fs control sources)
        | TarGz -> intoTarGz fs request.Compression stream (packAll fs control sources)
        | SevenZip -> intoSevenZipStream fs request.Compression stream (packAll fs control sources)
        | uncreatable -> invalidOp $"no writer for {ArchiveKind.extension uncreatable}"

    let private failingEvery (fs: IFileSystem) sources error =
        sources
        |> List.map (fun source -> Operations.itemResult fs source (Error error))

    let write (fs: IFileSystem) control (request: ArchiveRequest) sources =
        match Operations.attempting request.Path (fun () -> fs.File.Create(FileSystemPath.value request.Path)) with
        | Error error -> failingEvery fs sources error
        | Ok created ->
            let packed =
                Operations.attempting request.Path (fun () ->
                    use stream = created
                    packInto fs control request sources stream)

            let discard () = Operations.delete fs request.Path |> ignore

            if control.IsCancelled() then
                discard ()

                sources
                |> List.map (fun source -> Operations.itemResult fs source (Error(Operations.Cancelled source)))
            else
                match packed with
                | Ok results -> results
                | Error error ->
                    discard ()
                    failingEvery fs sources error

    let private within (fs: IFileSystem) (directory: FileSystemPath) (path: FileSystemPath) =
        let inside =
            FileSystemPath.value directory + string fs.Path.DirectorySeparatorChar

        (FileSystemPath.value path).StartsWith(inside, StringComparison.OrdinalIgnoreCase)

    let wouldPackItself (fs: IFileSystem) (target: FileSystemPath) sources =
        sources
        |> List.exists (fun source -> source = target || within fs source target)

    let private underPath (target: ArchivePath) (segments: EntryName list) =
        let names = ArchivePath.segments target

        segments.Length >= names.Length && List.truncate names.Length segments = names

    let private under (target: ArchivePath) (item: ArchiveItem) = underPath target item.Segments

    let private beneath (target: ArchivePath) (item: ArchiveItem) =
        item.Segments
        |> List.skip (max 0 (List.length (ArchivePath.segments target) - 1))

    let private landingPlace (fs: IFileSystem) (destination: FileSystemPath) segments =
        segments
        |> List.fold (fun path name -> FileSystemPath.combine fs path (EntryName.value name)) destination
        |> FileSystemPath.value
        |> fs.Path.GetFullPath
        |> FileSystemPath.create

    let private makeRoom (fs: IFileSystem) (path: FileSystemPath) =
        fs.Directory.CreateDirectory(fs.Path.GetDirectoryName(FileSystemPath.value path)) |> ignore

    let private writeContent (fs: IFileSystem) (item: ArchiveItem) (path: FileSystemPath) =
        use stream = fs.File.Create(FileSystemPath.value path)
        item.WriteTo stream

    let private restoreTime (fs: IFileSystem) (item: ArchiveItem) (path: FileSystemPath) =
        fs.FileInfo.New(FileSystemPath.value path).LastWriteTimeUtc <- item.Modified.UtcDateTime

    let private unpackEntry (fs: IFileSystem) (control: Operations.BatchControl) destination target item =
        let path = landingPlace fs destination (beneath target item)

        if control.IsCancelled() then
            Error(Operations.Cancelled path)
        elif not (within fs destination path) then
            Error(Operations.EntryOutsideTarget path)
        elif item.IsDirectory then
            attempt path (fun () -> fs.Directory.CreateDirectory(FileSystemPath.value path) |> ignore)
        else
            control.Report path

            attempt path (fun () ->
                makeRoom fs path
                writeContent fs item path
                restoreTime fs item path)

    let private walking step (items: seq<ArchiveItem>) =
        use walker = items.GetEnumerator()

        let rec run seen =
            if walker.MoveNext() then
                match step walker.Current with
                | Ok() -> run (seen + 1)
                | Error error -> Error error
            else
                Ok seen

        run 0

    let private unpackTarget (fs: IFileSystem) control (request: ExtractRequest) kind target =
        ArchiveReader.items fs request.Password kind request.Archive
        |> Seq.filter (under target)
        |> walking (unpackEntry fs control request.Destination target)
        |> Result.bind (function
            | 0 -> Error(Operations.SourceNotFound(ArchivePath.spelled fs request.Archive target))
            | _ -> Ok())

    let private eachTarget (fs: IFileSystem) (control: Operations.BatchControl) (archive: FileSystemPath) unpack targets =
        let named target =
            ArchivePath.name target |> Option.defaultValue (FileSystemPath.name fs archive)

        let rec run finished remaining =
            match remaining with
            | [] -> List.rev finished
            | target :: rest ->
                let item: Operations.ItemResult =
                    { Name = named target
                      Outcome = unpack target }

                if control.IsCancelled() then
                    List.rev (item :: finished)
                else
                    run (item :: finished) rest

        run [] targets

    let needsPassword (fs: IFileSystem) (archive: FileSystemPath) =
        match ArchiveReader.kindOf fs archive with
        | Some kind when ArchiveKind.carriesPassword kind ->
            try
                ArchiveReader.items fs None kind archive |> Seq.exists _.IsEncrypted
            with _ ->
                false
        | _ -> false

    let extract (fs: IFileSystem) control (request: ExtractRequest) (targets: ArchivePath list) =
        match ArchiveReader.kindOf fs request.Archive with
        | None -> Error(KindCannotBeRead request.Archive)
        | Some kind ->
            try
                if request.Password.IsNone && needsPassword fs request.Archive then
                    Error(PasswordNeeded request.Archive)
                else
                    Ok(eachTarget fs control request.Archive (unpackTarget fs control request kind) targets)
            with ex ->
                Error(ArchiveReader.mapException request.Archive ex)

    let extractAll (fs: IFileSystem) control (plans: ExtractPlan list) =
        let refused (request: ExtractRequest) error : Operations.ItemResult =
            { Name = FileSystemPath.name fs request.Archive
              Outcome = Error(Operations.ArchiveFailed error) }

        plans
        |> List.collect (fun plan ->
            match extract fs control plan.Request plan.Targets with
            | Ok results -> results
            | Error error -> [ refused plan.Request error ])

    let changeableKind (fs: IFileSystem) (archive: FileSystemPath) =
        FileSystemPath.name fs archive
        |> ArchiveKind.recognise
        |> Option.filter (ArchiveKind.capabilities >> _.CanChange)

    let changeRefusal (fs: IFileSystem) (archive: FileSystemPath) =
        match changeableKind fs archive with
        | None -> Some(KindCannotBeChanged archive)
        | Some _ when needsPassword fs archive -> Some(ProtectedArchiveCannotBeChanged archive)
        | Some _ -> None

    type private Update<'a> =
        | Commit of 'a
        | Abort of 'a

    type private Changing =
        { Held: EntryName list list
          Into: Sink }

    let private replacing (zip: ZipFile) (key: string) =
        match zip.GetEntry key with
        | null -> ()
        | existing -> zip.Delete existing

    let private staticSource (fs: IFileSystem) (path: FileSystemPath) =
        { new IStaticDataSource with
            member _.GetSource() = reading fs path :> Stream }

    let private intoUpdate (fs: IFileSystem) (zip: ZipFile) (level: ArchivePath) =
        { AddDirectory =
            fun name ->
                let key = keyIn level name
                replacing zip (key + "/")
                zip.AddDirectory(key + "/")
          AddFile =
            fun name path ->
                let key = keyIn level name
                replacing zip key

                zip.Add(
                    staticSource fs path,
                    ZipEntry(key, DateTime = (modifiedAt fs path).LocalDateTime, Size = fs.FileInfo.New(FileSystemPath.value path).Length)
                ) }

    let private updatingZip (fs: IFileSystem) archive (dropped: ArchivePath list) level work =
        use stream =
            fs.File.Open(FileSystemPath.value archive, FileMode.Open, FileAccess.ReadWrite)

        use zip =
            new ZipFile(stream, true, StringCodec.FromEncoding(ArchiveNames.legacy ()))

        zip.IsStreamOwner <- false
        zip.BeginUpdate(MemoryArchiveStorage())

        let held =
            zip
            |> Seq.cast<ZipEntry>
            |> Seq.map (fun entry -> entry, ArchiveReader.segmentsOf entry.Name)
            |> List.ofSeq

        held
        |> List.filter (fun (_, segments) -> dropped |> List.exists (fun target -> underPath target segments))
        |> List.iter (fst >> zip.Delete)

        match work { Held = held |> List.map snd; Into = intoUpdate fs zip level } with
        | Commit value ->
            zip.CommitUpdate()
            Ok value
        | Abort value ->
            zip.AbortUpdate()
            Ok value

    let private storedTime (moment: DateTimeOffset) =
        if moment = DateTimeOffset.MinValue then
            Nullable()
        else
            Nullable moment.LocalDateTime

    let private copyKept (fs: IFileSystem) archive (dropped: ArchivePath list) (control: Operations.BatchControl) (writer: SevenZipWriter) =
        ArchiveReader.items fs None SevenZip archive
        |> Seq.filter (fun item -> not (dropped |> List.exists (fun target -> under target item)))
        |> Seq.iter (fun item ->
            if not (control.IsCancelled()) then
                let key = item.Segments |> List.map EntryName.value |> String.concat "/"

                if item.IsDirectory then
                    writer.WriteDirectory(key, storedTime item.Modified)
                else
                    use content = new MemoryStream()
                    item.WriteTo content
                    content.Position <- 0L
                    writer.Write(key, content, storedTime item.Modified))

    let private rewritingSevenZip (fs: IFileSystem) archive dropped level control work =
        let fresh =
            FileSystemPath.create (FileSystemPath.value archive + RewriteSuffix)

        let held =
            ArchiveReader.items fs None SevenZip archive
            |> Seq.map _.Segments
            |> List.ofSeq

        let outcome =
            use stream = fs.File.Create(FileSystemPath.value fresh)

            use writer =
                new SevenZipWriter(stream, SevenZipWriterOptions(CompressionType.LZMA2, LeaveStreamOpen = true))

            copyKept fs archive dropped control writer
            work { Held = held; Into = sevenZipSink fs writer level }

        match outcome with
        | Commit value ->
            fs.File.Move(FileSystemPath.value fresh, FileSystemPath.value archive, true)
            Ok value
        | Abort value ->
            fs.File.Delete(FileSystemPath.value fresh)
            Ok value

    let private changeWith (fs: IFileSystem) archive control dropped level (work: Changing -> Update<'a>) =
        match changeRefusal fs archive with
        | Some refusal -> Error refusal
        | None ->
            try
                match changeableKind fs archive with
                | Some Zip -> updatingZip fs archive dropped level work
                | Some SevenZip -> rewritingSevenZip fs archive dropped level control work
                | _ -> Error(KindCannotBeChanged archive)
            with ex ->
                Error(ArchiveReader.mapException archive ex)

    let private cancelledEntries (fs: IFileSystem) archive targets =
        targets
        |> List.map (fun target ->
            let path = ArchivePath.spelled fs archive target

            { Name = ArchivePath.name target |> Option.defaultValue (FileSystemPath.name fs archive)
              Outcome = Error(Operations.Cancelled path) }: Operations.ItemResult)

    let addEntries (fs: IFileSystem) control (archive: FileSystemPath) (level: ArchivePath) sources =
        let replaced =
            sources
            |> List.map (fun source -> ArchivePath.child level (FileSystemPath.name fs source))

        changeWith fs archive control replaced level (fun changing ->
            let added = packAll fs control sources changing.Into

            if control.IsCancelled() then
                Abort(
                    sources
                    |> List.map (fun source -> Operations.itemResult fs source (Error(Operations.Cancelled source)))
                )
            else
                Commit added)

    let removeEntries (fs: IFileSystem) (control: Operations.BatchControl) (archive: FileSystemPath) targets =
        changeWith fs archive control targets ArchivePath.root (fun changing ->
            let dropped =
                targets
                |> eachTarget fs control archive (fun target ->
                    control.Report(ArchivePath.spelled fs archive target)

                    if changing.Held |> List.exists (underPath target) then
                        Ok()
                    else
                        Error(Operations.SourceNotFound(ArchivePath.spelled fs archive target)))

            if control.IsCancelled() then
                Abort(cancelledEntries fs archive targets)
            else
                Commit dropped)

    let makeEntryDirectory (fs: IFileSystem) archive level (name: EntryName) =
        let target = ArchivePath.spelled fs archive (ArchivePath.child level name)

        match ArchiveListing.read fs archive level with
        | Error error -> Error(Operations.ArchiveFailed error)
        | Ok entries when entries |> List.exists (fun entry -> entry.Name = name) ->
            Error(Operations.TargetExists target)
        | Ok _ ->
            changeWith fs archive Operations.unattended [] level (fun changing ->
                changing.Into.AddDirectory(EntryName.value name)
                Commit())
            |> Result.mapError Operations.ArchiveFailed

    let private droppingArrived (fs: IFileSystem) control archive arrived (results: Operations.ItemResult list) =
        match arrived with
        | [] -> Ok results
        | targets ->
            removeEntries fs control archive targets
            |> Result.map (fun dropped ->
                let refused =
                    dropped
                    |> List.choose (fun item ->
                        match item.Outcome with
                        | Error error -> Some(item.Name, error)
                        | Ok() -> None)
                    |> Map.ofList

                results
                |> List.map (fun item ->
                    match item.Outcome, Map.tryFind item.Name refused with
                    | Ok(), Some error -> { item with Outcome = Error error }
                    | _ -> item))

    let moveOut (fs: IFileSystem) control (request: ExtractRequest) (targets: ArchivePath list) =
        extract fs control request targets
        |> Result.bind (fun results ->
            let arrived =
                List.zip (List.truncate results.Length targets) results
                |> List.filter (snd >> _.Outcome >> Result.isOk)
                |> List.map fst

            droppingArrived fs control request.Archive arrived results)

    let moveIn (fs: IFileSystem) control archive level (sources: FileSystemPath list) =
        addEntries fs control archive level sources
        |> Result.map (fun results ->
            List.zip (List.truncate results.Length sources) results
            |> List.map (fun (source, item) ->
                match item.Outcome with
                | Error _ -> item
                | Ok() ->
                    { item with
                        Outcome = Operations.delete fs source }))
