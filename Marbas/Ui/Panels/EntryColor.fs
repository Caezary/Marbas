module Marbas.Ui.Panels.EntryColor

open Marbas.Domain
open Marbas.FileSystem.DirectoryListing
open Marbas.Ui.Theme

type EntryColor =
    | DirectoryColor
    | PlainColor
    | ArchiveColor
    | ExecutableColor
    | LinkColor

let private archiveExtensions =
    set
        [ "7z"
          "arj"
          "bz2"
          "cab"
          "gz"
          "iso"
          "jar"
          "lzh"
          "nupkg"
          "rar"
          "tar"
          "tbz2"
          "tgz"
          "xz"
          "zip"
          "zst" ]

let private isArchive (name: EntryName) =
    archiveExtensions.Contains((EntryName.extension name).ToLowerInvariant())

let classify (entry: Entry) =
    match entry.LinkTarget, entry.Kind with
    | Some _, _ -> LinkColor
    | None, Directory -> DirectoryColor
    | None, File when isArchive entry.Name -> ArchiveColor
    | None, File when entry.Executability = Executable -> ExecutableColor
    | None, File -> PlainColor

let attribute color =
    match color with
    | DirectoryColor -> ClassicScheme.directoryEntry
    | PlainColor -> ClassicScheme.plainEntry
    | ArchiveColor -> ClassicScheme.archiveEntry
    | ExecutableColor -> ClassicScheme.executableEntry
    | LinkColor -> ClassicScheme.linkEntry
