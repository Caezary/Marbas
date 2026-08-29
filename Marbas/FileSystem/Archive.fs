namespace Marbas.FileSystem

open System
open System.IO
open System.IO.Abstractions
open System.IO.Compression
open System.Formats.Tar
open ICSharpCode.SharpZipLib.Zip
open FsToolkit.ErrorHandling

module Archive =

    [<Literal>]
    let private AesKeySize = 256

    type private Sink =
        { AddDirectory: string -> unit
          AddFile: string -> FileSystemPath -> unit }

    let private attempting (path: FileSystemPath) action =
        try
            Ok(action ())
        with ex ->
            Error(Operations.mapException path ex)

    let private attempt path action = attempting path action |> Result.ignore

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
                        attempting source (fun () ->
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
        | uncreatable -> invalidOp $"no writer for {ArchiveKind.extension uncreatable}"

    let write (fs: IFileSystem) control (request: ArchiveRequest) sources =
        match attempting request.Path (fun () -> fs.File.Create(FileSystemPath.value request.Path)) with
        | Error error -> sources |> List.map (fun source -> Operations.itemResult fs source (Error error))
        | Ok created ->
            let packed =
                use stream = created
                packInto fs control request sources stream

            if control.IsCancelled() then
                Operations.delete fs request.Path |> ignore

                sources
                |> List.map (fun source -> Operations.itemResult fs source (Error(Operations.Cancelled source)))
            else
                packed

    let private within (fs: IFileSystem) (directory: FileSystemPath) (path: FileSystemPath) =
        let inside =
            FileSystemPath.value directory + string fs.Path.DirectorySeparatorChar

        (FileSystemPath.value path).StartsWith(inside, StringComparison.OrdinalIgnoreCase)

    let wouldPackItself (fs: IFileSystem) (target: FileSystemPath) sources =
        sources
        |> List.exists (fun source -> source = target || within fs source target)

    let private inArchive (fs: IFileSystem) (archive: FileSystemPath) (target: ArchivePath) =
        FileSystemPath.combine fs archive (ArchivePath.value target)

    let private under (target: ArchivePath) (item: ArchiveItem) =
        let names = ArchivePath.segments target

        item.Segments.Length >= names.Length
        && List.truncate names.Length item.Segments = names

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
            | 0 -> Error(Operations.SourceNotFound(inArchive fs request.Archive target))
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
