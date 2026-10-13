namespace Marbas.FileSystem

open System
open System.IO.Abstractions
open Marbas.Domain

type FileSystemPath = private FileSystemPath of string

type PathKind =
    | NoEntry
    | FileEntry
    | DirectoryEntry

module FileSystemPath =

    let create (path: string) = FileSystemPath path

    let value (FileSystemPath path) = path

    let kind (fs: IFileSystem) (FileSystemPath path) =
        if fs.Directory.Exists path then DirectoryEntry
        elif fs.File.Exists path then FileEntry
        else NoEntry

    let linkTargetOfInfo (info: IFileSystemInfo) =
        try
            if info.Attributes.HasFlag IO.FileAttributes.ReparsePoint then
                info.LinkTarget |> Option.ofObj |> Option.map FileSystemPath
            else
                None
        with _ ->
            None

    let linkTarget (fs: IFileSystem) (FileSystemPath path) = linkTargetOfInfo (fs.FileInfo.New path)

    let isLink (fs: IFileSystem) (path: FileSystemPath) = linkTarget fs path |> Option.isSome

    let combine (fs: IFileSystem) (FileSystemPath path) (name: string) =
        FileSystemPath(fs.Path.Combine(path, name))

    let name (fs: IFileSystem) (FileSystemPath path) =
        fs.Path.GetFileName path |> EntryName.create

    let platformOf (fs: IFileSystem) =
        if fs.Path.DirectorySeparatorChar = '\\' then
            Windows
        else
            Unix

    let private separatorsOf platform =
        match platform with
        | Windows -> [| '\\'; '/' |]
        | Unix -> [| '/' |]

    let private caseRule platform =
        match platform with
        | Windows -> StringComparison.OrdinalIgnoreCase
        | Unix -> StringComparison.Ordinal

    let comparer platform =
        StringComparer.FromComparison(caseRule platform)

    let private withoutTrailingSeparator platform (FileSystemPath path) = path.TrimEnd(separatorsOf platform)

    let sameEntry platform one other =
        String.Equals(withoutTrailingSeparator platform one, withoutTrailingSeparator platform other, caseRule platform)

    let distinctEntries platform (paths: FileSystemPath list) =
        let keep kept path =
            if kept |> List.exists (sameEntry platform path) then
                kept
            else
                path :: kept

        paths |> List.fold keep [] |> List.rev

    let strictlyUnder platform root (FileSystemPath path) =
        let above = withoutTrailingSeparator platform root

        path.Length > above.Length
        && path.StartsWith(above, caseRule platform)
        && Array.contains path[above.Length] (separatorsOf platform)

    let atOrUnder platform root path =
        sameEntry platform root path || strictlyUnder platform root path

    let pathAfter platform root path =
        if strictlyUnder platform root path then
            let above = withoutTrailingSeparator platform root
            Some((value path).Substring(above.Length + 1))
        else
            None

    let isBareName (fs: IFileSystem) (name: string) =
        not (fs.Path.IsPathRooted name)
        && String.IsNullOrEmpty(fs.Path.GetDirectoryName name)

    let directory (fs: IFileSystem) (FileSystemPath path) =
        FileSystemPath(fs.Path.GetDirectoryName path)

    let resolvedFrom (fs: IFileSystem) (FileSystemPath directory) (path: string) =
        FileSystemPath(fs.Path.GetFullPath(fs.Path.Combine(directory, path)))

    let root (fs: IFileSystem) (FileSystemPath path) =
        match fs.Path.GetPathRoot path with
        | null
        | "" -> None
        | found -> Some(FileSystemPath found)

    let parent (fs: IFileSystem) (FileSystemPath path) =
        match fs.DirectoryInfo.New(path).Parent with
        | null -> None
        | parent -> Some(FileSystemPath parent.FullName)

    let rec nearest (fs: IFileSystem) (path: FileSystemPath) =
        match kind fs path with
        | DirectoryEntry -> Some path
        | FileEntry
        | NoEntry -> parent fs path |> Option.bind (nearest fs)
