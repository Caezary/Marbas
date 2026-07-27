module Marbas.FileSystem.Operations

open System
open System.IO.Abstractions

type OperationError =
    | SourceNotFound of path: FileSystemPath
    | TargetExists of path: FileSystemPath
    | AccessDenied of path: FileSystemPath
    | OperationFailed of path: FileSystemPath * message: string

type ItemResult =
    { Name: string
      Outcome: Result<unit, OperationError> }

let mapException (path: FileSystemPath) (ex: exn) =
    match ex with
    | :? UnauthorizedAccessException -> AccessDenied path
    | _ -> OperationFailed(path, ex.Message)

let private attempt path action =
    try
        action ()
        Ok()
    with ex ->
        Error(mapException path ex)

let makeDirectory (fs: IFileSystem) (parent: FileSystemPath) (name: string) =
    let path = FileSystemPath.combine fs parent name

    match FileSystemPath.kind fs path with
    | NoEntry -> attempt path (fun () -> fs.Directory.CreateDirectory(FileSystemPath.value path) |> ignore)
    | _ -> Error(TargetExists path)

let delete (fs: IFileSystem) (path: FileSystemPath) =
    let raw = FileSystemPath.value path

    match FileSystemPath.kind fs path with
    | DirectoryEntry -> attempt path (fun () -> fs.Directory.Delete(raw, true))
    | FileEntry -> attempt path (fun () -> fs.File.Delete raw)
    | NoEntry -> Error(SourceNotFound path)

let rec private copyTo (fs: IFileSystem) overwrite (source: FileSystemPath) (target: FileSystemPath) =
    let rawSource = FileSystemPath.value source
    let rawTarget = FileSystemPath.value target

    match FileSystemPath.kind fs source with
    | DirectoryEntry ->
        attempt target (fun () -> fs.Directory.CreateDirectory rawTarget |> ignore)
        |> Result.bind (fun () ->
            fs.Directory.EnumerateFileSystemEntries rawSource
            |> Seq.fold
                (fun acc entry ->
                    acc
                    |> Result.bind (fun () ->
                        let child = FileSystemPath.create entry
                        copyTo fs overwrite child (FileSystemPath.combine fs target (FileSystemPath.name fs child))))
                (Ok()))
    | _ when not overwrite && FileSystemPath.kind fs target <> NoEntry -> Error(TargetExists target)
    | _ -> attempt source (fun () -> fs.File.Copy(rawSource, rawTarget, overwrite))

let private inDirectory (fs: IFileSystem) (targetDirectory: FileSystemPath) (source: FileSystemPath) =
    FileSystemPath.combine fs targetDirectory (FileSystemPath.name fs source)

let copy (fs: IFileSystem) overwrite (source: FileSystemPath) (targetDirectory: FileSystemPath) =
    match FileSystemPath.kind fs source with
    | NoEntry -> Error(SourceNotFound source)
    | _ -> copyTo fs overwrite source (inDirectory fs targetDirectory source)

let private moveTo (fs: IFileSystem) overwrite (source: FileSystemPath) (target: FileSystemPath) =
    match FileSystemPath.kind fs source with
    | NoEntry -> Error(SourceNotFound source)
    | _ when not overwrite && FileSystemPath.kind fs target <> NoEntry -> Error(TargetExists target)
    | DirectoryEntry -> copyTo fs overwrite source target |> Result.bind (fun () -> delete fs source)
    | FileEntry -> attempt source (fun () -> fs.File.Move(FileSystemPath.value source, FileSystemPath.value target, overwrite))

let move (fs: IFileSystem) overwrite (source: FileSystemPath) (targetDirectory: FileSystemPath) =
    moveTo fs overwrite source (inDirectory fs targetDirectory source)

let rename (fs: IFileSystem) (source: FileSystemPath) (newName: string) =
    moveTo fs false source (FileSystemPath.combine fs (FileSystemPath.directory fs source) newName)

let private itemResults (fs: IFileSystem) sources outcome =
    sources
    |> List.map (fun source ->
        { Name = FileSystemPath.name fs source
          Outcome = outcome source })

let deleteAll (fs: IFileSystem) sources = itemResults fs sources (delete fs)

let copyAll (fs: IFileSystem) overwrite sources targetDirectory =
    itemResults fs sources (fun source -> copy fs overwrite source targetDirectory)

let moveAll (fs: IFileSystem) overwrite sources targetDirectory =
    itemResults fs sources (fun source -> move fs overwrite source targetDirectory)
