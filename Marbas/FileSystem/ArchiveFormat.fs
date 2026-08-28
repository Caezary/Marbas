namespace Marbas.FileSystem

type ArchiveFormat =
    | Zip
    | TarGz

type ArchiveCompression =
    | Stored
    | Normal
    | Maximum

type ArchivePassword = private ArchivePassword of string

type ArchiveRequest =
    { Path: FileSystemPath
      Format: ArchiveFormat
      Compression: ArchiveCompression }

type ArchiveChoice =
    { Target: string
      Format: ArchiveFormat
      Compression: ArchiveCompression
      Password: ArchivePassword option }

module ArchiveFormat =

    let all = [ Zip; TarGz ]

    let initial = Zip

    let extension format =
        match format with
        | Zip -> "zip"
        | TarGz -> "tar.gz"

    let carriesPassword format =
        match format with
        | Zip -> true
        | TarGz -> false

module ArchiveCompression =

    let all = [ Stored; Normal; Maximum ]

    let initial = Normal

module ArchivePassword =

    let tryCreate (raw: string) =
        match raw with
        | "" -> None
        | text -> Some(ArchivePassword text)

    let value (ArchivePassword text) = text
