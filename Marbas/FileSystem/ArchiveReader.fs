namespace Marbas.FileSystem

open System
open System.IO
open System.IO.Abstractions
open SharpCompress.Archives
open SharpCompress.Readers

type ArchiveItem =
    { Segments: EntryName list
      IsDirectory: bool
      Size: int64
      Modified: DateTimeOffset
      IsEncrypted: bool
      WriteTo: Stream -> unit }

type ArchiveError =
    | KindCannotBeRead of path: FileSystemPath
    | ArchiveNotFound of path: FileSystemPath
    | PasswordNeeded of path: FileSystemPath
    | ArchiveDamaged of path: FileSystemPath * message: string

module ArchiveReader =

    let mapException path (ex: exn) =
        match ex with
        | :? FileNotFoundException
        | :? DirectoryNotFoundException -> ArchiveNotFound path
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
            Password = (password |> Option.map ArchivePassword.value |> Option.toObj)
        )

    let private modifiedAt (moment: Nullable<DateTime>) =
        moment
        |> Option.ofNullable
        |> Option.map DateTimeOffset
        |> Option.defaultValue DateTimeOffset.MinValue

    let private stored (entry: IArchiveEntry) =
        { Segments = segmentsOf (Option.ofObj entry.Key |> Option.defaultValue "")
          IsDirectory = entry.IsDirectory
          Size = entry.Size
          Modified = modifiedAt entry.LastModifiedTime
          IsEncrypted = entry.IsEncrypted
          WriteTo =
            fun target ->
                use content = entry.OpenEntryStream()
                content.CopyTo target }

    let private streamed (reader: IReader) =
        { Segments = segmentsOf (Option.ofObj reader.Entry.Key |> Option.defaultValue "")
          IsDirectory = reader.Entry.IsDirectory
          Size = reader.Entry.Size
          Modified = modifiedAt reader.Entry.LastModifiedTime
          IsEncrypted = reader.Entry.IsEncrypted
          WriteTo = reader.WriteEntryTo }

    let items (fs: IFileSystem) (password: ArchivePassword option) (kind: ArchiveKind) (path: FileSystemPath) =
        seq {
            use stream = fs.File.OpenRead(FileSystemPath.value path)

            match kind with
            | Tar
            | TarGz
            | TarBz2
            | TarXz ->
                use reader = ReaderFactory.OpenReader(stream, options password)

                while reader.MoveToNextEntry() do
                    yield streamed reader
            | Zip
            | SevenZip
            | Rar ->
                use archive = ArchiveFactory.OpenArchive(stream, options password)

                yield! archive.Entries |> Seq.map stored
        }
