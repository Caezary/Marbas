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

    let combine (fs: IFileSystem) (FileSystemPath path) (name: string) =
        FileSystemPath(fs.Path.Combine(path, name))

    let name (fs: IFileSystem) (FileSystemPath path) =
        fs.Path.GetFileName path |> EntryName.create

    let isBareName (fs: IFileSystem) (name: string) =
        not (fs.Path.IsPathRooted name) && String.IsNullOrEmpty(fs.Path.GetDirectoryName name)

    let directory (fs: IFileSystem) (FileSystemPath path) =
        FileSystemPath(fs.Path.GetDirectoryName path)

    let parent (fs: IFileSystem) (FileSystemPath path) =
        match fs.DirectoryInfo.New(path).Parent with
        | null -> None
        | parent -> Some(FileSystemPath parent.FullName)

    let rec nearest (fs: IFileSystem) (path: FileSystemPath) =
        match kind fs path with
        | DirectoryEntry -> Some path
        | FileEntry
        | NoEntry -> parent fs path |> Option.bind (nearest fs)
