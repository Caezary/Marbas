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

[<Struct>]
type ArchiveWalk<'walked> =
    | KeepReading of kept: 'walked
    | StopReading of stopped: 'walked

type ArchiveStep<'walked> = delegate of 'walked * ArchiveItem -> ArchiveWalk<'walked>

type ArchiveWalked<'walked> =
    | Walked of 'walked
    | WalkBroken of reached: 'walked * failure: exn

module ArchiveWalked =

    let reached walked =
        match walked with
        | Walked reached
        | WalkBroken(reached, _) -> reached

    let failure walked =
        match walked with
        | Walked _ -> None
        | WalkBroken(_, failure) -> Some failure

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

    let private modifiedOf (exact: DateTimeOffset voption) (entry: IEntry) =
        match exact with
        | ValueSome moment -> moment
        | ValueNone -> modifiedAt entry.LastModifiedTime

    let private kindOfEntry (entry: IEntry) =
        if entry.IsDirectory then StoredDirectory
        elif entry.IsEncrypted then EncryptedFile
        else StoredFile

    let private infoOf (exact: DateTimeOffset voption) (entry: IEntry) =
        { Segments = segmentsOf (Option.ofObj entry.Key |> Option.defaultValue "")
          Kind = kindOfEntry entry
          Size = entry.Size
          Modified = modifiedOf exact entry }

    let private streaming (reader: IReader) =
        seq {
            while reader.MoveToNextEntry() do
                yield reader.Entry, ValueNone, (fun () -> reader.OpenEntryStream() :> Stream)
        }

    let private stepping
        (step: ArchiveStep<'walked>)
        (initial: 'walked)
        (entries: seq<IEntry * DateTimeOffset voption * (unit -> Stream)>)
        =
        let mutable walked = initial
        let mutable reading = true

        try
            use walker = entries.GetEnumerator()

            while reading && walker.MoveNext() do
                let entry, exact, openContent = walker.Current

                let item =
                    { Info = infoOf exact entry
                      OpenContent = openContent }

                match step.Invoke(walked, item) with
                | KeepReading kept -> walked <- kept
                | StopReading stopped ->
                    walked <- stopped
                    reading <- false

            Walked walked
        with failure ->
            WalkBroken(walked, failure)

    let private readingSequentially password step initial (stream: Stream) =
        use reader = ReaderFactory.OpenReader(stream, options password)
        stepping step initial (streaming reader)

    let private exactMoments kind (stream: Stream) =
        match kind with
        | Zip -> ZipMoments.read stream
        | SevenZip
        | Rar
        | Tar
        | TarGz
        | TarBz2
        | TarXz -> ZipMoments.none

    let private readingWholeArchive kind password step initial (stream: Stream) =
        let moments = exactMoments kind stream
        use archive = ArchiveFactory.OpenArchive(stream, options password)

        archive.Entries
        |> Seq.mapi (fun ordinal entry ->
            entry :> IEntry, ZipMoments.at ordinal moments, fun () -> entry.OpenEntryStream())
        |> stepping step initial

    let private isSequential kind =
        match kind with
        | Tar
        | TarGz
        | TarBz2
        | TarXz -> true
        | Zip
        | SevenZip
        | Rar -> false

    let walk
        (fs: IFileSystem)
        password
        (kind: ArchiveKind)
        (path: FileSystemPath)
        (step: ArchiveStep<'walked>)
        (initial: 'walked)
        =
        try
            use stream = fs.File.OpenRead(FileSystemPath.value path)

            if isSequential kind then
                readingSequentially password step initial stream
            else
                readingWholeArchive kind password step initial stream
        with failure ->
            WalkBroken(initial, failure)

    let fold fs password kind path (step: ArchiveStep<'walked>) (initial: 'walked) =
        match walk fs password kind path step initial with
        | Walked walked -> walked
        | WalkBroken(_, failure) -> raise failure

    let entryInfo fs password kind path (segments: EntryName list) =
        let looking =
            ArchiveStep<ArchiveEntryInfo option>(fun found item ->
                if ArchiveEntryInfo.isDirectory item.Info || item.Info.Segments <> segments then
                    KeepReading found
                else
                    StopReading(Some item.Info))

        fold fs password kind path looking None

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
