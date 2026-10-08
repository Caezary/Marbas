module Marbas.FileSystem.Archive

open System
open System.IO
open System.IO.Abstractions
open System.IO.Compression
open System.Formats.Tar
open ICSharpCode.SharpZipLib.Zip
open SharpCompress.Common
open SharpCompress.Writers.SevenZip
open FsToolkit.ErrorHandling
open Marbas.Domain
open Marbas.FileSystem.DirectoryListing

[<Literal>]
let private AesKeySize = 256

[<Literal>]
let private RewriteSuffix = ".marbas-new"

type private Stored =
    { Size: int64
      Modified: DateTimeOffset option
      Content: unit -> Stream }

type private Sink =
    { AddDirectory: string -> DateTimeOffset option -> unit
      AddFile: string -> Stored -> unit }

let private attempt path action =
    Operations.attempting path action |> Result.ignore

let private known (moment: DateTimeOffset) =
    if moment = DateTimeOffset.MinValue then
        None
    else
        Some moment

let private nullableTime moment =
    moment
    |> Option.map (fun (m: DateTimeOffset) -> m.LocalDateTime)
    |> Option.toNullable

let private entryName (fs: IFileSystem) (root: FileSystemPath) (path: FileSystemPath) =
    fs.Path
        .GetRelativePath(FileSystemPath.value root, FileSystemPath.value path)
        .Replace(fs.Path.DirectorySeparatorChar, '/')

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

let private onDisk (fs: IFileSystem) (entry: Entry) (path: FileSystemPath) =
    { Size = entry.Size |> Option.defaultValue 0L
      Modified = known entry.Modified
      Content = fun () -> reading fs path }

let private intoZip compression (password: ArchivePassword option) (stream: Stream) pack =
    use zip = new ZipOutputStream(stream)
    zip.SetLevel(zipLevel compression)

    password
    |> Option.iter (fun secret -> zip.Password <- ArchivePassword.value secret)

    pack
        { AddDirectory =
            fun name _ ->
                zip.PutNextEntry(ZipEntry(name + "/"))
                zip.CloseEntry()
          AddFile =
            fun name stored ->
                let entry = ZipEntry(name, Size = stored.Size)

                stored.Modified
                |> Option.iter (fun moment -> entry.DateTime <- moment.LocalDateTime)

                if password.IsSome then
                    entry.AESKeySize <- AesKeySize

                zip.PutNextEntry entry
                use content = stored.Content()
                content.CopyTo zip
                zip.CloseEntry() }

let private intoTarGz compression (stream: Stream) pack =
    use compressed = new GZipStream(stream, gzipLevel compression)
    use tar = new TarWriter(compressed, TarEntryFormat.Pax, true)

    let dated (entry: PaxTarEntry) moment =
        moment |> Option.iter (fun (m: DateTimeOffset) -> entry.ModificationTime <- m)

        tar.WriteEntry entry

    pack
        { AddDirectory = fun name -> dated (PaxTarEntry(TarEntryType.Directory, name + "/"))
          AddFile =
            fun name stored ->
                use content = stored.Content()
                dated (PaxTarEntry(TarEntryType.RegularFile, name, DataStream = content)) stored.Modified }

let private keyAtLevel (level: ArchivePath) (name: string) =
    match ArchivePath.value level with
    | "" -> name
    | prefix -> $"{prefix}/{name}"

let private sinkAtLevel (level: ArchivePath) sink =
    { AddDirectory = fun name -> sink.AddDirectory(keyAtLevel level name)
      AddFile = fun name -> sink.AddFile(keyAtLevel level name) }

let private lzmaLevel compression =
    match compression with
    | Stored -> 0
    | Normal -> 5
    | Maximum -> 9

let private intoSevenZip compression (stream: Stream) pack =
    use writer =
        new SevenZipWriter(
            stream,
            SevenZipWriterOptions(
                CompressionType.LZMA2,
                CompressionLevel = lzmaLevel compression,
                LeaveStreamOpen = true
            )
        )

    pack
        { AddDirectory = fun name moment -> writer.WriteDirectory(name, nullableTime moment)
          AddFile =
            fun name stored ->
                use content = stored.Content()
                writer.Write(name, content, nullableTime stored.Modified) }

let private intoArchive format compression password stream pack =
    match ArchiveFormat.kind format with
    | Zip -> intoZip compression password stream pack
    | TarGz -> intoTarGz compression stream pack
    | SevenZip -> intoSevenZip compression stream pack
    | uncreatable -> invalidOp $"no writer for {ArchiveKind.extension uncreatable}"

let rec private adding (fs: IFileSystem) (control: Operations.BatchControl) sink root (child: Child) =
    if control.IsCancelled() then
        Error(Operations.Cancelled child.Path)
    else
        match child.Entry.Kind, child.Entry.LinkTarget with
        | Directory, Some _ ->
            control.Report child.Path
            attempt child.Path (fun () -> sink.AddDirectory (entryName fs root child.Path) None)
        | Directory, None ->
            result {
                do! attempt child.Path (fun () -> sink.AddDirectory (entryName fs root child.Path) None)
                let! below = Operations.attempting child.Path (fun () -> children fs child.Path)
                do! below |> List.traverseResultM (adding fs control sink root) |> Result.ignore
            }
        | File, _ ->
            control.Report child.Path

            attempt child.Path (fun () ->
                sink.AddFile (entryName fs root child.Path) (onDisk fs child.Entry child.Path))

let private add (fs: IFileSystem) control sink root source =
    match existingEntry fs source with
    | None -> Error(Operations.SourceNotFound source)
    | Some entry -> adding fs control sink root { Path = source; Entry = entry }

let private packAll (fs: IFileSystem) control sources sink =
    sources
    |> Operations.allWatched control (fun source -> add fs control sink (FileSystemPath.directory fs source) source)

let private packInto fs control (request: ArchiveRequest) sources stream =
    intoArchive request.Format request.Compression request.Password stream (packAll fs control sources)

let private failingEvery sources error =
    sources |> List.map (fun source -> Operations.itemResult source (Error error))

let write (fs: IFileSystem) control (request: ArchiveRequest) sources =
    match Operations.attempting request.Path (fun () -> fs.File.Create(FileSystemPath.value request.Path)) with
    | Error error -> failingEvery sources error
    | Ok created ->
        let packed =
            Operations.attempting request.Path (fun () ->
                use stream = created
                packInto fs control request sources stream)

        let discard () =
            Operations.delete fs Operations.unattended request.Path |> ignore

        if control.IsCancelled() then
            discard ()

            sources
            |> List.map (fun source -> Operations.itemResult source (Error(Operations.Cancelled source)))
        else
            match packed with
            | Ok results -> results
            | Error error ->
                discard ()
                failingEvery sources error

let wouldPackItself (fs: IFileSystem) (target: FileSystemPath) sources =
    let platform = FileSystemPath.platformOf fs

    sources
    |> List.exists (fun source -> FileSystemPath.atOrUnder platform source target)

let private entryAtOrUnder (target: ArchivePath) (info: ArchiveEntryInfo) =
    ArchivePath.atOrUnder target info.Segments

let private segmentsFromTargetName (target: ArchivePath) (info: ArchiveEntryInfo) =
    info.Segments |> List.skip (max 0 (ArchivePath.depth target - 1))

let private landingPlace (fs: IFileSystem) (destination: FileSystemPath) segments =
    segments
    |> List.fold (fun path name -> FileSystemPath.combine fs path (EntryName.value name)) destination
    |> FileSystemPath.value
    |> fs.Path.GetFullPath
    |> FileSystemPath.create

let private makeRoom (fs: IFileSystem) (path: FileSystemPath) =
    fs.Directory.CreateDirectory(fs.Path.GetDirectoryName(FileSystemPath.value path))
    |> ignore

let private writeContent (fs: IFileSystem) (openContent: unit -> Stream) (path: FileSystemPath) =
    use stream = fs.File.Create(FileSystemPath.value path)
    use content = openContent ()
    content.CopyTo stream

let private restoreTime (fs: IFileSystem) (info: ArchiveEntryInfo) (path: FileSystemPath) =
    fs.FileInfo.New(FileSystemPath.value path).LastWriteTimeUtc <- info.Modified.UtcDateTime

let private unpackEntry (fs: IFileSystem) (control: Operations.BatchControl) destination target info openContent =
    let path = landingPlace fs destination (segmentsFromTargetName target info)

    if control.IsCancelled() then
        Error(Operations.Cancelled path)
    elif not (FileSystemPath.strictlyUnder (FileSystemPath.platformOf fs) destination path) then
        Error(Operations.EntryOutsideTarget path)
    elif ArchiveEntryInfo.isDirectory info then
        attempt path (fun () -> fs.Directory.CreateDirectory(FileSystemPath.value path) |> ignore)
    else
        control.Report path

        attempt path (fun () ->
            makeRoom fs path
            writeContent fs openContent path
            restoreTime fs info path)

type private TargetProgress =
    | NotReached
    | Unpacking
    | Whole
    | Failed of Operations.OperationError

type private WalkEnd =
    | ReadThrough
    | StoppedByUser

type private UnpackWalk =
    { Progress: TargetProgress array
      mutable Unsettled: int
      mutable End: WalkEnd }

let private isTargetItself (target: ArchivePath) (info: ArchiveEntryInfo) =
    not (ArchiveEntryInfo.isDirectory info)
    && info.Segments = ArchivePath.segments target

let private advanced target info outcome =
    match outcome with
    | Error error -> Failed error
    | Ok() when isTargetItself target info -> Whole
    | Ok() -> Unpacking

let private stillOpen progress =
    match progress with
    | Failed _ -> false
    | NotReached
    | Unpacking
    | Whole -> true

let private settled progress =
    match progress with
    | Failed _
    | Whole -> true
    | NotReached
    | Unpacking -> false

let private targetsBySegments (targets: ArchivePath array) =
    targets
    |> Array.indexed
    |> Array.groupBy (snd >> ArchivePath.segments)
    |> Array.map (fun (segments, indexed) -> segments, indexed |> Array.map fst |> List.ofArray)
    |> Map.ofArray

let private prefixesOf (segments: EntryName list) =
    [ 0 .. segments.Length ]
    |> List.map (fun length -> List.truncate length segments)

let private unpackingAll fs control destination (targets: ArchivePath array) (walk: UnpackWalk) =
    let bySegments = targetsBySegments targets

    let reached (info: ArchiveEntryInfo) =
        prefixesOf info.Segments
        |> List.collect (fun prefix -> bySegments |> Map.tryFind prefix |> Option.defaultValue [])
        |> List.filter (fun index -> stillOpen walk.Progress[index])

    let unpackInto info openContent index =
        let target = targets[index]
        let before = walk.Progress[index]

        let after =
            advanced target info (unpackEntry fs control destination target info openContent)

        if settled after && not (settled before) then
            walk.Unsettled <- walk.Unsettled - 1

        walk.Progress[index] <- after

    ArchiveStep(fun item ->
        if control.IsCancelled() then
            walk.End <- StoppedByUser
            StopReading
        else
            let info = item.Info
            let openContent = item.OpenContent

            reached info |> List.iter (unpackInto info openContent)

            if walk.Unsettled = 0 then StopReading else KeepReading)

let private targetLanding (fs: IFileSystem) destination target =
    ArchivePath.segments target
    |> List.tryLast
    |> Option.toList
    |> landingPlace fs destination

let private outcomeOf (fs: IFileSystem) (request: ExtractRequest) ending target progress =
    match progress, ending with
    | NotReached, StoppedByUser -> None
    | NotReached, ReadThrough ->
        ArchivePath.spelled fs request.Archive target
        |> Operations.SourceNotFound
        |> Error
        |> Some
    | Failed error, _ -> Some(Error error)
    | Whole, _
    | Unpacking, ReadThrough -> Some(Ok())
    | Unpacking, StoppedByUser ->
        targetLanding fs request.Destination target
        |> Operations.Cancelled
        |> Error
        |> Some

let private entryResult archive target outcome : Operations.ItemResult =
    { Source = Operations.ItemInArchive(archive, target)
      Outcome = outcome }

let private unpackingEach (fs: IFileSystem) control (request: ExtractRequest) kind (targets: ArchivePath list) =
    let asked = Array.ofList targets

    let walk =
        { Progress = Array.create asked.Length NotReached
          Unsettled = asked.Length
          End = ReadThrough }

    let step = unpackingAll fs control request.Destination asked walk

    ArchiveReader.withItems fs request.Password kind request.Archive step

    List.zip targets (List.ofArray walk.Progress)
    |> List.choose (fun (target, reached) ->
        outcomeOf fs request walk.End target reached
        |> Option.map (entryResult request.Archive target))

let private eachTarget (control: Operations.BatchControl) (archive: FileSystemPath) unpack targets =
    let rec run finished remaining =
        match remaining with
        | [] -> List.rev finished
        | target :: rest ->
            let item: Operations.ItemResult =
                { Source = Operations.ItemInArchive(archive, target)
                  Outcome = unpack target }

            if control.IsCancelled() then
                List.rev (item :: finished)
            else
                run (item :: finished) rest

    run [] targets

let needsPassword (fs: IFileSystem) (archive: FileSystemPath) =
    match ArchiveReader.kindOf fs archive with
    | Some kind when ArchiveKind.carriesPassword kind ->
        ArchiveIndex.read fs archive
        |> Result.map (List.exists ArchiveEntryInfo.isEncrypted)
    | _ -> Ok false

let private whenUnlocked (fs: IFileSystem) control (request: ExtractRequest) kind targets =
    match needsPassword fs request.Archive with
    | Error error -> Error error
    | Ok true -> Error(PasswordNeeded request.Archive)
    | Ok false -> Ok(unpackingEach fs control request kind targets)

let extract (fs: IFileSystem) control (request: ExtractRequest) (targets: ArchivePath list) =
    match ArchiveReader.kindOf fs request.Archive with
    | None -> Error(KindCannotBeRead request.Archive)
    | Some kind ->
        try
            match request.Password with
            | Some _ -> Ok(unpackingEach fs control request kind targets)
            | None -> whenUnlocked fs control request kind targets
        with ex ->
            Error(ArchiveReader.mapException request.Archive ex)

let extractAll (fs: IFileSystem) control (plans: ExtractPlan list) =
    let refused (request: ExtractRequest) error : Operations.ItemResult =
        { Source = Operations.ItemOnDisk request.Archive
          Outcome = Error(Operations.ArchiveFailed error) }

    plans
    |> List.collect (fun plan ->
        match extract fs control plan.Request plan.Targets with
        | Ok results -> results
        | Error error -> [ refused plan.Request error ])

let changeableKind (fs: IFileSystem) (archive: FileSystemPath) =
    FileSystemPath.name fs archive
    |> ArchiveKind.recognise
    |> Option.filter (ArchiveKind.capabilities >> Set.contains Changeable)

let changeRefusal (fs: IFileSystem) (archive: FileSystemPath) =
    match changeableKind fs archive with
    | None -> Some(KindCannotBeChanged archive)
    | Some _ ->
        match needsPassword fs archive with
        | Error error -> Some error
        | Ok true -> Some(ProtectedArchiveCannotBeChanged archive)
        | Ok false -> None

type private Update<'a> =
    | Commit of 'a
    | Abort of 'a

type private OpenForChange =
    { Held: EntryName list list
      Sink: Sink }

let private replacing (zip: ZipFile) (key: string) =
    match zip.GetEntry key with
    | null -> ()
    | existing -> zip.Delete existing

let private staticSource (stored: Stored) =
    { new IStaticDataSource with
        member _.GetSource() = stored.Content() }

let private intoUpdate (zip: ZipFile) =
    { AddDirectory =
        fun name _ ->
            replacing zip (name + "/")
            zip.AddDirectory(name + "/")
      AddFile =
        fun name stored ->
            replacing zip name
            let entry = ZipEntry(name, Size = stored.Size)

            stored.Modified
            |> Option.iter (fun moment -> entry.DateTime <- moment.LocalDateTime)

            zip.Add(staticSource stored, entry) }

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
    |> List.filter (fun (_, segments) -> dropped |> List.exists (fun target -> ArchivePath.atOrUnder target segments))
    |> List.iter (fst >> zip.Delete)

    match
        work
            { Held = held |> List.map snd
              Sink = sinkAtLevel level (intoUpdate zip) }
    with
    | Commit value ->
        zip.CommitUpdate()
        Ok value
    | Abort value ->
        zip.AbortUpdate()
        Ok value

let private buffered (info: ArchiveEntryInfo) (openContent: unit -> Stream) =
    let content = new MemoryStream()
    use source = openContent ()
    source.CopyTo content
    content.Position <- 0L

    { Size = info.Size
      Modified = known info.Modified
      Content = fun () -> content :> Stream }

let private keyOf (info: ArchiveEntryInfo) =
    info.Segments |> List.map EntryName.value |> String.concat "/"

let private keeping (dropped: ArchivePath list) (control: Operations.BatchControl) sink =
    ArchiveStep(fun item ->
        let info = item.Info

        if control.IsCancelled() then
            StopReading
        elif dropped |> List.exists (fun target -> entryAtOrUnder target info) then
            KeepReading
        elif ArchiveEntryInfo.isDirectory info then
            sink.AddDirectory (keyOf info) (known info.Modified)
            KeepReading
        else
            sink.AddFile (keyOf info) (buffered info item.OpenContent)
            KeepReading)

let private copyKept (fs: IFileSystem) archive kind dropped control sink =
    ArchiveReader.withItems fs None kind archive (keeping dropped control sink)

let private workingFile (fs: IFileSystem) (archive: FileSystemPath) =
    let candidate ordinal =
        match ordinal with
        | 1 -> FileSystemPath.value archive + RewriteSuffix
        | taken -> $"{FileSystemPath.value archive}{RewriteSuffix}.{taken}"

    let rec pick ordinal =
        let path = FileSystemPath.create (candidate ordinal)

        if FileSystemPath.kind fs path = NoEntry then
            path
        else
            pick (ordinal + 1)

    pick 1

let private writingFreshCopy (fs: IFileSystem) archive kind format fresh dropped level control work held =
    try
        let outcome =
            use stream = fs.File.Create(FileSystemPath.value fresh)

            intoArchive format Normal None stream (fun sink ->
                copyKept fs archive kind dropped control sink

                work
                    { Held = held
                      Sink = sinkAtLevel level sink })

        match outcome with
        | Commit value ->
            fs.File.Move(FileSystemPath.value fresh, FileSystemPath.value archive, true)
            Ok value
        | Abort value -> Ok value
    finally
        Operations.delete fs Operations.unattended fresh |> ignore

let private rewritingWhole (fs: IFileSystem) archive kind dropped level control work =
    match ArchiveFormat.tryCreate kind with
    | None -> Error(KindCannotBeChanged archive)
    | Some format ->
        let rewriteHeld =
            writingFreshCopy fs archive kind format (workingFile fs archive) dropped level control work

        ArchiveIndex.read fs archive
        |> Result.map (List.map _.Segments)
        |> Result.bind rewriteHeld

let private withArchiveOpenForChange
    (fs: IFileSystem)
    archive
    control
    dropped
    level
    (work: OpenForChange -> Update<'a>)
    =
    match changeRefusal fs archive with
    | Some refusal -> Error refusal
    | None ->
        try
            match changeableKind fs archive with
            | Some Zip -> updatingZip fs archive dropped level work
            | Some kind -> rewritingWhole fs archive kind dropped level control work
            | None -> Error(KindCannotBeChanged archive)
        with ex ->
            Error(ArchiveReader.mapException archive ex)

let private cancelledEntries (fs: IFileSystem) archive targets =
    targets
    |> List.map (fun target ->
        { Source = Operations.ItemInArchive(archive, target)
          Outcome = Error(Operations.Cancelled(ArchivePath.spelled fs archive target)) }
        : Operations.ItemResult)

let addEntries (fs: IFileSystem) control (archive: FileSystemPath) (level: ArchivePath) sources =
    let replaced =
        sources
        |> List.map (fun source -> ArchivePath.child level (FileSystemPath.name fs source))

    withArchiveOpenForChange fs archive control replaced level (fun change ->
        let added = packAll fs control sources change.Sink

        if control.IsCancelled() then
            Abort(
                sources
                |> List.map (fun source -> Operations.itemResult source (Error(Operations.Cancelled source)))
            )
        else
            Commit added)

let removeEntries (fs: IFileSystem) (control: Operations.BatchControl) (archive: FileSystemPath) targets =
    withArchiveOpenForChange fs archive control targets ArchivePath.root (fun change ->
        let dropped =
            targets
            |> eachTarget control archive (fun target ->
                control.Report(ArchivePath.spelled fs archive target)

                if change.Held |> List.exists (ArchivePath.atOrUnder target) then
                    Ok()
                else
                    ArchivePath.spelled fs archive target |> Operations.SourceNotFound |> Error)

        if control.IsCancelled() then
            Abort(cancelledEntries fs archive targets)
        else
            Commit dropped)

let makeEntryDirectory (fs: IFileSystem) archive level (name: EntryName) =
    let target = ArchivePath.spelled fs archive (ArchivePath.child level name)

    match ArchiveListing.read fs archive level with
    | Error error -> Error(Operations.ArchiveFailed error)
    | Ok entries when entries |> List.exists (fun entry -> entry.Name = name) -> Error(Operations.TargetExists target)
    | Ok _ ->
        withArchiveOpenForChange fs archive Operations.unattended [] level (fun change ->
            change.Sink.AddDirectory (EntryName.value name) None
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
                    | Error error -> Some(item.Source, error)
                    | Ok() -> None)
                |> Map.ofList

            results
            |> List.map (fun item ->
                match item.Outcome, Map.tryFind item.Source refused with
                | Ok(), Some error -> { item with Outcome = Error error }
                | _ -> item))

let private arrivedOf (results: Operations.ItemResult list) =
    results
    |> List.choose (fun item ->
        match item.Outcome, item.Source with
        | Ok(), Operations.ItemInArchive(_, entry) -> Some entry
        | _ -> None)

let moveToDirectory (fs: IFileSystem) control (request: ExtractRequest) (targets: ArchivePath list) =
    extract fs control request targets
    |> Result.bind (fun results -> droppingArrived fs control request.Archive (arrivedOf results) results)

let landedAs (fs: IFileSystem) archive scratch target =
    ArchivePath.name target
    |> Option.defaultValue (FileSystemPath.name fs archive)
    |> EntryName.value
    |> FileSystemPath.combine fs scratch

let private carryingOver
    (came: Map<Operations.ItemSource, Operations.ItemSource>)
    (added: Operations.ItemResult list)
    (taken: Operations.ItemResult list)
    =
    let landed =
        added
        |> List.choose (fun item -> Map.tryFind item.Source came |> Option.map (fun from -> from, item.Outcome))
        |> Map.ofList

    taken
    |> List.map (fun item ->
        match item.Outcome, Map.tryFind item.Source landed with
        | Ok(), Some outcome -> { item with Outcome = outcome }
        | _ -> item)

let private refusedEntries archive error targets : Operations.ItemResult list =
    targets
    |> List.map (fun entry ->
        { Source = Operations.ItemInArchive(archive, entry)
          Outcome = Error error })

let private carriedInto fs control (request: ExtractRequest) targets target level scratch =
    let cameFrom entry =
        Operations.ItemOnDisk(landedAs fs request.Archive scratch entry),
        Operations.ItemInArchive(request.Archive, entry)

    result {
        let! taken = extract fs control { request with Destination = scratch } targets
        let arrived = arrivedOf taken

        let! added =
            arrived
            |> List.map (landedAs fs request.Archive scratch)
            |> addEntries fs control target level

        return carryingOver (arrived |> List.map cameFrom |> Map.ofList) added taken
    }

let copyToArchive (fs: IFileSystem) control (request: ExtractRequest) targets (target: FileSystemPath) level =
    let released scratch =
        try
            carriedInto fs control request targets target level scratch
        finally
            Operations.delete fs Operations.unattended scratch |> ignore

    match changeRefusal fs target with
    | Some refusal -> Error refusal
    | None ->
        match Workspace.scratch fs request.Destination with
        | Error error -> Ok(refusedEntries request.Archive error targets)
        | Ok scratch -> released scratch

let moveToArchive (fs: IFileSystem) control (request: ExtractRequest) targets target level =
    copyToArchive fs control request targets target level
    |> Result.bind (fun results -> droppingArrived fs control request.Archive (arrivedOf results) results)

let moveFromDisk (fs: IFileSystem) control archive level (sources: FileSystemPath list) =
    addEntries fs control archive level sources
    |> Result.map (fun results ->
        List.zip (List.truncate results.Length sources) results
        |> List.map (fun (source, item) ->
            match item.Outcome with
            | Error _ -> item
            | Ok() ->
                { item with
                    Outcome = Operations.delete fs Operations.unattended source }))
