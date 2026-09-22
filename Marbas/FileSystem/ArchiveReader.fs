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
      WriteTo: Stream -> unit }

type ArchiveWalk =
    | KeepReading
    | StopReading

type ArchiveStep = delegate of ArchiveItem -> ArchiveWalk

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
        |> Option.filter (ArchiveKind.capabilities >> _.CanRead)

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

    let private writeEntry (entry: IArchiveEntry) (target: Stream) =
        use content = entry.OpenEntryStream()
        content.CopyTo target

    let private streaming (reader: IReader) =
        seq {
            while reader.MoveToNextEntry() do
                yield reader.Entry, (reader.WriteEntryTo: Stream -> unit)
        }

    let private walk (step: ArchiveStep) (entries: seq<IEntry * (Stream -> unit)>) =
        use walker = entries.GetEnumerator()
        let mutable reading = KeepReading

        while reading = KeepReading && walker.MoveNext() do
            let entry, writeTo = walker.Current
            reading <- step.Invoke { Info = infoOf entry; WriteTo = writeTo }

    let withItems (fs: IFileSystem) password (kind: ArchiveKind) (path: FileSystemPath) (step: ArchiveStep) =
        use stream = fs.File.OpenRead(FileSystemPath.value path)

        match kind with
        | Tar
        | TarGz
        | TarBz2
        | TarXz ->
            use reader = ReaderFactory.OpenReader(stream, options password)
            walk step (streaming reader)
        | Zip
        | SevenZip
        | Rar ->
            use archive = ArchiveFactory.OpenArchive(stream, options password)
            walk step (archive.Entries |> Seq.map (fun entry -> entry :> IEntry, writeEntry entry))
