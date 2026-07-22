module Marbas.FileSystem.Operations

open System
open System.IO.Abstractions

type OperationError =
    | SourceNotFound of path: string
    | TargetExists of path: string
    | AccessDenied of path: string
    | OperationFailed of path: string * message: string

type ItemResult =
    { Name: string
      Outcome: Result<unit, OperationError> }

let mapException path (ex: exn) =
    match ex with
    | :? UnauthorizedAccessException -> AccessDenied path
    | _ -> OperationFailed(path, ex.Message)

let private attempt path action =
    try
        action ()
        Ok()
    with ex ->
        Error(mapException path ex)

let private exists (fs: IFileSystem) path =
    fs.File.Exists path || fs.Directory.Exists path

let makeDirectory (fs: IFileSystem) (parent: string) (name: string) =
    let path = fs.Path.Combine(parent, name)

    if exists fs path then
        Error(TargetExists path)
    else
        attempt path (fun () -> fs.Directory.CreateDirectory path |> ignore)

let delete (fs: IFileSystem) (path: string) =
    if fs.Directory.Exists path then
        attempt path (fun () -> fs.Directory.Delete(path, true))
    elif fs.File.Exists path then
        attempt path (fun () -> fs.File.Delete path)
    else
        Error(SourceNotFound path)

let rec private copyTo (fs: IFileSystem) overwrite (source: string) (target: string) =
    if fs.Directory.Exists source then
        attempt target (fun () -> fs.Directory.CreateDirectory target |> ignore)
        |> Result.bind (fun () ->
            fs.Directory.EnumerateFileSystemEntries source
            |> Seq.fold
                (fun acc child ->
                    acc
                    |> Result.bind (fun () ->
                        copyTo fs overwrite child (fs.Path.Combine(target, fs.Path.GetFileName child))))
                (Ok()))
    elif not overwrite && exists fs target then
        Error(TargetExists target)
    else
        attempt source (fun () -> fs.File.Copy(source, target, overwrite))

let private inDirectory (fs: IFileSystem) (targetDirectory: string) (source: string) =
    fs.Path.Combine(targetDirectory, fs.Path.GetFileName source)

let copy (fs: IFileSystem) overwrite (source: string) (targetDirectory: string) =
    if not (exists fs source) then
        Error(SourceNotFound source)
    else
        copyTo fs overwrite source (inDirectory fs targetDirectory source)

let private moveTo (fs: IFileSystem) overwrite (source: string) (target: string) =
    if not (exists fs source) then
        Error(SourceNotFound source)
    elif not overwrite && exists fs target then
        Error(TargetExists target)
    elif fs.Directory.Exists source then
        copyTo fs overwrite source target |> Result.bind (fun () -> delete fs source)
    else
        attempt source (fun () -> fs.File.Move(source, target, overwrite))

let move (fs: IFileSystem) overwrite (source: string) (targetDirectory: string) =
    moveTo fs overwrite source (inDirectory fs targetDirectory source)

let rename (fs: IFileSystem) (source: string) (newName: string) =
    moveTo fs false source (fs.Path.Combine(fs.Path.GetDirectoryName source, newName))

let private itemResults (fs: IFileSystem) sources outcome =
    sources
    |> List.map (fun source ->
        { Name = fs.Path.GetFileName(source: string)
          Outcome = outcome source })

let deleteAll (fs: IFileSystem) sources = itemResults fs sources (delete fs)

let copyAll (fs: IFileSystem) overwrite sources targetDirectory =
    itemResults fs sources (fun source -> copy fs overwrite source targetDirectory)

let moveAll (fs: IFileSystem) overwrite sources targetDirectory =
    itemResults fs sources (fun source -> move fs overwrite source targetDirectory)
