[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Marbas.FileSystem.FindGround

open System
open System.IO.Abstractions
open Marbas.Domain

let private allRoots (fs: IFileSystem) =
    Drives.list fs None |> List.map DriveRow.root

let pathDirectories (fs: IFileSystem) (separator: char) (raw: string) =
    if String.IsNullOrEmpty raw then
        []
    else
        raw.Split separator
        |> Array.toList
        |> List.map (fun piece -> piece.Trim().Trim '"')
        |> List.filter (String.IsNullOrEmpty >> not)
        |> List.map FileSystemPath.create
        |> FileSystemPath.distinctEntries (FileSystemPath.platformOf fs)
        |> List.filter (fun path -> fs.Directory.Exists(FileSystemPath.value path))

let private onDisk paths = paths |> List.map OnDisk

let resolve (fs: IFileSystem) location marked onPath scope =
    match scope with
    | FromDirectory
    | DirectoryOnly ->
        { Roots = [ location ]
          OtherRoots = [] }
    | MarkedDirectories -> { Roots = marked; OtherRoots = [] }
    | FromVolumeRoot ->
        let start = PanelLocation.holding fs location
        let root = Drives.holding fs start |> Option.defaultValue start
        let isRoot = FileSystemPath.sameEntry (FileSystemPath.platformOf fs) root

        { Roots = [ OnDisk root ]
          OtherRoots = allRoots fs |> List.filter (isRoot >> not) }
    | AllDrives ->
        { Roots = allRoots fs |> onDisk
          OtherRoots = [] }
    | PathDirectories ->
        { Roots = onPath |> onDisk
          OtherRoots = [] }
