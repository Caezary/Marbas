namespace Marbas.FileSystem

open System
open System.IO.Abstractions
open Marbas.Domain

type FindGround =
    { Roots: FileSystemPath list
      OtherRoots: FileSystemPath list }

module FindGround =

    let private separatorsOf (fs: IFileSystem) =
        [| fs.Path.DirectorySeparatorChar; fs.Path.AltDirectorySeparatorChar |]

    let private withoutTrailingSeparator (fs: IFileSystem) (raw: string) =
        match raw.TrimEnd(separatorsOf fs) with
        | "" -> raw
        | trimmed -> trimmed

    let private sameRoot a b =
        String.Equals(FileSystemPath.value a, FileSystemPath.value b, StringComparison.OrdinalIgnoreCase)

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

    let resolve (fs: IFileSystem) location marked onPath scope =
        let here () = PanelLocation.holding fs location

        match scope with
        | FromDirectory
        | DirectoryOnly -> { Roots = [ here () ]; OtherRoots = [] }
        | MarkedDirectories -> { Roots = marked; OtherRoots = [] }
        | FromVolumeRoot ->
            let start = here ()
            let root = Drives.holding fs start |> Option.defaultValue start

            { Roots = [ root ]
              OtherRoots = allRoots fs |> List.filter (sameRoot root >> not) }
        | AllDrives -> { Roots = allRoots fs; OtherRoots = [] }
        | PathDirectories -> { Roots = onPath; OtherRoots = [] }
