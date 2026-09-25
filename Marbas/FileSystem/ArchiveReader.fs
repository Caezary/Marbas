namespace Marbas.FileSystem

open System
open System.IO
open System.IO.Abstractions
open System.Runtime.CompilerServices
open SharpCompress.Archives
open SharpCompress.Common
open SharpCompress.Readers
open Marbas.Domain

type ArchiveEntryKind =
    | StoredDirectory
    | StoredFile
    | EncryptedFile

type ArchiveEntryInfo =
    { Segments: EntryName list
      Kind: ArchiveEntryKind
      Size: int64
      Modified: DateTimeOffset }

[<Struct; IsByRefLike>]
type ArchiveItem =
    { Info: ArchiveEntryInfo
      OpenContent: unit -> Stream }

type ArchiveWalk =
    | KeepReading
    | StopReading

type ArchiveStep = delegate of ArchiveItem -> ArchiveWalk

type internal OwningStream(content: Stream, owned: IDisposable list) =
    inherit Stream()

    let mutable position = 0L

    override _.CanRead = true

    override _.CanSeek = false

    override _.CanWrite = false

    override _.Length = raise (NotSupportedException())

    override _.Position
        with get () = position
        and set _ = raise (NotSupportedException())

    override _.Flush() = ()

    override _.SetLength(_: int64) = raise (NotSupportedException())

    override _.Write(_: byte array, _: int, _: int) = raise (NotSupportedException())

    override _.Seek(_: int64, _: SeekOrigin) = raise (NotSupportedException())

    override _.Read(buffer: byte array, offset: int, count: int) =
        let read = content.Read(buffer, offset, count)
        position <- position + int64 read
        read

    override _.Dispose(disposing: bool) =
        if disposing then
            content.Dispose()
            owned |> List.iter _.Dispose()

        base.Dispose disposing

module ArchiveEntryInfo =

    let isDirectory (info: ArchiveEntryInfo) = info.Kind = StoredDirectory

    let isEncrypted (info: ArchiveEntryInfo) = info.Kind = EncryptedFile

module ArchiveReader =

    let mapException path (ex: exn) =
        match ex with
        | :? FileNotFoundException
        | :? DirectoryNotFoundException -> ArchiveNotFound path
        | :? UnauthorizedAccessException -> ArchiveAccessDenied path
        | :? SharpCompress.Common.CryptographicException
        | :? Security.Cryptography.CryptographicException -> PasswordNeeded path
        | _ -> ArchiveDamaged(path, ex.Message)

    let kindOf (fs: IFileSystem) (path: FileSystemPath) =
        FileSystemPath.name fs path
        |> ArchiveKind.recognise
        |> Option.filter (ArchiveKind.capabilities >> Set.contains Readable)

    let segmentsOf (key: string) =
        key.Split '/'
        |> List.ofArray
        |> List.filter (fun segment -> segment <> "" && segment <> ".")
        |> List.map EntryName.create

    let private options (password: ArchivePassword option) =
        ReaderOptions(
            LeaveStreamOpen = true,
            ArchiveEncoding = ArchiveNames.reading (),
            Password = (password |> Option.map ArchivePassword.value |> Option.toObj)
        )

    let private modifiedAt (moment: Nullable<DateTime>) =
        moment
        |> Option.ofNullable
        |> Option.map DateTimeOffset
        |> Option.defaultValue DateTimeOffset.MinValue

    let private kindOfEntry (entry: IEntry) =
        if entry.IsDirectory then StoredDirectory
        elif entry.IsEncrypted then EncryptedFile
        else StoredFile

    let private infoOf (entry: IEntry) =
        { Segments = segmentsOf (Option.ofObj entry.Key |> Option.defaultValue "")
          Kind = kindOfEntry entry
          Size = entry.Size
          Modified = modifiedAt entry.LastModifiedTime }

    let private streaming (reader: IReader) =
        seq {
            while reader.MoveToNextEntry() do
                yield reader.Entry, (fun () -> reader.OpenEntryStream() :> Stream)
        }

    let private walk (step: ArchiveStep) (entries: seq<IEntry * (unit -> Stream)>) =
        use walker = entries.GetEnumerator()
        let mutable reading = KeepReading

        while reading = KeepReading && walker.MoveNext() do
            let entry, openContent = walker.Current
            reading <- step.Invoke { Info = infoOf entry; OpenContent = openContent }

    let private readingSequentially password (step: ArchiveStep) (stream: Stream) =
        use reader = ReaderFactory.OpenReader(stream, options password)
        walk step (streaming reader)

    let private readingWholeArchive password (step: ArchiveStep) (stream: Stream) =
        use archive = ArchiveFactory.OpenArchive(stream, options password)
        walk step (archive.Entries |> Seq.map (fun entry -> entry :> IEntry, fun () -> entry.OpenEntryStream()))

    let private isSequential kind =
        match kind with
        | Tar
        | TarGz
        | TarBz2
        | TarXz -> true
        | Zip
        | SevenZip
        | Rar -> false

    let withItems (fs: IFileSystem) password (kind: ArchiveKind) (path: FileSystemPath) (step: ArchiveStep) =
        use stream = fs.File.OpenRead(FileSystemPath.value path)

        if isSequential kind then
            readingSequentially password step stream
        else
            readingWholeArchive password step stream

    let entryInfo fs password kind path (segments: EntryName list) =
        let found = ref None

        let looking =
            ArchiveStep(fun item ->
                if ArchiveEntryInfo.isDirectory item.Info || item.Info.Segments <> segments then
                    KeepReading
                else
                    found.Value <- Some item.Info
                    StopReading)

        withItems fs password kind path looking
        found.Value

    let private wanted (segments: EntryName list) (entry: IEntry) =
        not entry.IsDirectory
        && segmentsOf (Option.ofObj entry.Key |> Option.defaultValue "") = segments

    let private contentInArchive password segments (stream: Stream) =
        let archive = ArchiveFactory.OpenArchive(stream, options password)

        match archive.Entries |> Seq.tryFind (wanted segments) with
        | Some entry -> Some(new OwningStream(entry.OpenEntryStream(), [ archive ]) :> Stream)
        | None ->
            archive.Dispose()
            None

    let private contentSequentially password segments (stream: Stream) =
        let reader = ReaderFactory.OpenReader(stream, options password)

        let rec looking () =
            if not (reader.MoveToNextEntry()) then
                reader.Dispose()
                None
            elif wanted segments reader.Entry then
                Some(new OwningStream(reader.OpenEntryStream(), [ reader ]) :> Stream)
            else
                looking ()

        looking ()

    let openContent (fs: IFileSystem) password kind (path: FileSystemPath) segments =
        let stream = fs.File.OpenRead(FileSystemPath.value path)

        let content =
            try
                if isSequential kind then
                    contentSequentially password segments stream
                else
                    contentInArchive password segments stream
            with _ ->
                stream.Dispose()
                reraise ()

        match content with
        | Some found -> Some(new OwningStream(found, [ stream ]) :> Stream)
        | None ->
            stream.Dispose()
            None
