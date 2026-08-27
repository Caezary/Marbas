namespace Marbas.FileSystem

type ArchiveFormat =
    | Zip
    | TarGz

type ArchiveCompression =
    | Stored
    | Normal
    | Maximum

type ArchiveRequest =
    { Path: FileSystemPath
      Format: ArchiveFormat
      Compression: ArchiveCompression }

module ArchiveFormat =

    let all = [ Zip; TarGz ]

    let extension format =
        match format with
        | Zip -> "zip"
        | TarGz -> "tar.gz"

    let carriesPassword format =
        match format with
        | Zip -> true
        | TarGz -> false
