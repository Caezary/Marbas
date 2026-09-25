namespace Marbas.FileSystem

type ArchiveFormat = private ArchiveFormat of ArchiveKind

type ArchiveCompression =
    | Stored
    | Normal
    | Maximum

type ArchivePassword = private ArchivePassword of string

type ArchiveRequest =
    { Path: FileSystemPath
      Format: ArchiveFormat
      Compression: ArchiveCompression
      Password: ArchivePassword option }

type ExtractRequest =
    { Archive: FileSystemPath
      Password: ArchivePassword option
      Destination: FileSystemPath }

type ExtractPlan =
    { Request: ExtractRequest
      Targets: ArchivePath list }

type ArchiveChoice =
    { Target: string
      Format: ArchiveFormat
      Compression: ArchiveCompression
      Password: ArchivePassword option }

module ArchiveFormat =

    let all =
        ArchiveKind.all
        |> List.filter (ArchiveKind.capabilities >> Set.contains Creatable)
        |> List.map ArchiveFormat

    let kind (ArchiveFormat kind) = kind

    let tryCreate wanted = all |> List.tryFind (kind >> (=) wanted)

    let initial = List.head all

    let extension format = kind format |> ArchiveKind.extension

    let carriesPassword format = kind format |> ArchiveKind.carriesPassword

module ArchiveCompression =

    let all = [ Stored; Normal; Maximum ]

    let initial = Normal

module ArchivePassword =

    let tryCreate (raw: string) =
        match raw with
        | "" -> None
        | text -> Some(ArchivePassword text)

    let value (ArchivePassword text) = text
