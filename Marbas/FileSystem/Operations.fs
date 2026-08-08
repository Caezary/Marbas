module Marbas.FileSystem.Operations

open System
open System.IO.Abstractions
open FsToolkit.ErrorHandling

type OperationError =
    | SourceNotFound of path: FileSystemPath
    | TargetExists of path: FileSystemPath
    | AccessDenied of path: FileSystemPath
    | OperationFailed of path: FileSystemPath * message: string

type ItemResult =
    { Name: string
      Outcome: Result<unit, OperationError> }

type FileSystemAction = IFileSystem -> FileSystemPath -> unit

let mapException (path: FileSystemPath) (ex: exn) =
    match ex with
    | :? UnauthorizedAccessException -> AccessDenied path
    | _ -> OperationFailed(path, ex.Message)

let private attempt fs path (action: FileSystemAction) =
    try
        action fs path
        Ok()
    with ex ->
        Error(mapException path ex)

let private createDirectory (fs: IFileSystem) (path: FileSystemPath) =
    let raw = FileSystemPath.value path
    fs.Directory.CreateDirectory raw |> ignore

let private deleteDirectory (fs: IFileSystem) (path: FileSystemPath) =
    let raw = FileSystemPath.value path
    fs.Directory.Delete(raw, true)

let private deleteFile (fs: IFileSystem) (path: FileSystemPath) =
    let raw = FileSystemPath.value path
    fs.File.Delete raw

let private copyFile (fs: IFileSystem) overwrite (source: FileSystemPath) (target: FileSystemPath) =
    let rawSource = FileSystemPath.value source
    let rawTarget = FileSystemPath.value target
    fs.File.Copy(rawSource, rawTarget, overwrite)

let private moveFile (fs: IFileSystem) overwrite (source: FileSystemPath) (target: FileSystemPath) =
    let rawSource = FileSystemPath.value source
    let rawTarget = FileSystemPath.value target
    fs.File.Move(rawSource, rawTarget, overwrite)
    
let private getFileSystemEntries (fs: IFileSystem) (path: FileSystemPath) =
    let raw = FileSystemPath.value path
    fs.Directory.EnumerateFileSystemEntries raw
    |> Seq.map FileSystemPath.create

let isNonEmptyDirectory (fs: IFileSystem) (path: FileSystemPath) =
    match FileSystemPath.kind fs path with
    | DirectoryEntry ->
        try
            getFileSystemEntries fs path |> Seq.isEmpty |> not
        with _ ->
            true
    | _ -> false

let makeDirectory (fs: IFileSystem) (parent: FileSystemPath) (name: string) =
    let path = FileSystemPath.combine fs parent name

    match FileSystemPath.kind fs path with
    | NoEntry -> attempt fs path createDirectory
    | _ -> Error(TargetExists path)

let delete (fs: IFileSystem) (path: FileSystemPath) =
    match FileSystemPath.kind fs path with
    | DirectoryEntry -> attempt fs path deleteDirectory
    | FileEntry -> attempt fs path deleteFile
    | NoEntry -> Error(SourceNotFound path)

let rec private copyTo (fs: IFileSystem) overwrite (source: FileSystemPath) (target: FileSystemPath) =
    match FileSystemPath.kind fs source with
    | DirectoryEntry ->
        result {
            do! attempt fs target createDirectory

            do!
                getFileSystemEntries fs source
                |> List.ofSeq
                |> List.traverseResultM (fun entry ->
                    entry
                    |> FileSystemPath.name fs
                    |> FileSystemPath.combine fs target
                    |> copyTo fs overwrite entry)
                |> Result.ignore
        }
    | _ when not overwrite && FileSystemPath.kind fs target <> NoEntry -> Error(TargetExists target)
    | _ ->
        let copyToTarget fs' source' = copyFile fs' overwrite source' target
        attempt fs source copyToTarget

let private inDirectory (fs: IFileSystem) (targetDirectory: FileSystemPath) (source: FileSystemPath) =
    source
    |> FileSystemPath.name fs
    |> FileSystemPath.combine fs targetDirectory

let copy (fs: IFileSystem) overwrite (targetDirectory: FileSystemPath) (source: FileSystemPath) =
    match FileSystemPath.kind fs source with
    | NoEntry -> Error(SourceNotFound source)
    | _ ->
        source
        |> inDirectory fs targetDirectory
        |> copyTo fs overwrite source

let private moveTo (fs: IFileSystem) overwrite (source: FileSystemPath) (target: FileSystemPath) =
    match FileSystemPath.kind fs source with
    | NoEntry -> Error(SourceNotFound source)
    | _ when not overwrite && FileSystemPath.kind fs target <> NoEntry -> Error(TargetExists target)
    | DirectoryEntry ->
        result {
            do! copyTo fs overwrite source target
            do! delete fs source
        }
    | FileEntry ->
        let moveToTarget fs' source' = moveFile fs' overwrite source' target
        attempt fs source moveToTarget

let move (fs: IFileSystem) overwrite (targetDirectory: FileSystemPath) (source: FileSystemPath) =
    source
    |> inDirectory fs targetDirectory
    |> moveTo fs overwrite source

let rename (fs: IFileSystem) (source: FileSystemPath) (newName: string) =
    let directory = FileSystemPath.directory fs source
    newName
    |> FileSystemPath.combine fs directory
    |> moveTo fs false source

let private itemResults (fs: IFileSystem) sources outcome =
    sources
    |> List.map (fun source ->
        { Name = FileSystemPath.name fs source
          Outcome = outcome source })

let failures (results: ItemResult list) =
    results |> List.filter (_.Outcome >> Result.isError)

let deleteAll (fs: IFileSystem) sources = itemResults fs sources (delete fs)

let copyAll (fs: IFileSystem) overwrite sources targetDirectory =
    itemResults fs sources (copy fs overwrite targetDirectory)

let moveAll (fs: IFileSystem) overwrite sources targetDirectory =
    itemResults fs sources (move fs overwrite targetDirectory)
