namespace Marbas.FileSystem

open System.IO.Abstractions

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

    let combine (fs: IFileSystem) (FileSystemPath path) (name: string) =
        FileSystemPath(fs.Path.Combine(path, name))

    let name (fs: IFileSystem) (FileSystemPath path) = fs.Path.GetFileName path

    let directory (fs: IFileSystem) (FileSystemPath path) =
        FileSystemPath(fs.Path.GetDirectoryName path)

    let parent (fs: IFileSystem) (FileSystemPath path) =
        match fs.DirectoryInfo.New(path).Parent with
        | null -> None
        | parent -> Some(FileSystemPath parent.FullName)
