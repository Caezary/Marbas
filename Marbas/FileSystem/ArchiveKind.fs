namespace Marbas.FileSystem

open System
open Marbas.Domain

type ArchiveKind =
    | Zip
    | Tar
    | TarGz
    | TarBz2
    | TarXz
    | SevenZip
    | Rar

type ArchiveCapabilities =
    { CanRead: bool
      CanCreate: bool
      CanChange: bool }

module ArchiveKind =

    let all = [ Zip; Tar; TarGz; TarBz2; TarXz; SevenZip; Rar ]

    let extension kind =
        match kind with
        | Zip -> "zip"
        | Tar -> "tar"
        | TarGz -> "tar.gz"
        | TarBz2 -> "tar.bz2"
        | TarXz -> "tar.xz"
        | SevenZip -> "7z"
        | Rar -> "rar"

    let capabilities kind =
        match kind with
        | Zip
        | TarGz
        | SevenZip ->
            { CanRead = true
              CanCreate = true
              CanChange = true }
        | Tar
        | TarBz2
        | TarXz
        | Rar ->
            { CanRead = true
              CanCreate = false
              CanChange = false }

    let carriesPassword kind =
        match kind with
        | Zip -> true
        | Tar
        | TarGz
        | TarBz2
        | TarXz
        | SevenZip
        | Rar -> false

    let private byLongestSuffix =
        all |> List.sortByDescending (extension >> String.length)

    let private suffixed (raw: string) kind =
        let suffix = "." + extension kind

        raw.Length > suffix.Length
        && raw.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)

    let recognise (name: EntryName) =
        let raw = EntryName.value name

        byLongestSuffix |> List.tryFind (suffixed raw)
