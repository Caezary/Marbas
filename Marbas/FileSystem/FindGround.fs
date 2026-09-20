namespace Marbas.FileSystem

open System
open System.IO.Abstractions
open Marbas.Domain

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module FindGround =

    let private separatorsOf (fs: IFileSystem) =
        [| fs.Path.DirectorySeparatorChar; fs.Path.AltDirectorySeparatorChar |]

    let private withoutTrailingSeparator (fs: IFileSystem) (raw: string) =
        match raw.TrimEnd(separatorsOf fs) with
        | "" -> raw
        | trimmed -> trimmed

    let private sameRoot (fs: IFileSystem) a b =
        FileSystemPath.sameEntry (FileSystemPath.platformOf fs) a b

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
            |> List.distinctBy (withoutTrailingSeparator fs)
            |> List.map FileSystemPath.create
            |> List.filter (fun path -> fs.Directory.Exists(FileSystemPath.value path))

    let private onDisk paths = paths |> List.map OnDisk

    let resolve (fs: IFileSystem) location marked onPath scope =
        match scope with
        | FromDirectory
        | DirectoryOnly -> { Roots = [ location ]; OtherRoots = [] }
        | MarkedDirectories -> { Roots = marked; OtherRoots = [] }
        | FromVolumeRoot ->
            let start = PanelLocation.holding fs location
            let root = Drives.holding fs start |> Option.defaultValue start

            { Roots = [ OnDisk root ]
              OtherRoots = allRoots fs |> List.filter (sameRoot fs root >> not) }
        | AllDrives ->
            { Roots = allRoots fs |> onDisk
              OtherRoots = [] }
        | PathDirectories -> { Roots = onPath |> onDisk; OtherRoots = [] }
