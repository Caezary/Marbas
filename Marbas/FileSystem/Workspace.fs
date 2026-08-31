module Marbas.FileSystem.Workspace

open System
open System.IO.Abstractions
open Marbas.FileSystem.Operations

[<Literal>]
let DirectoryName = "Marbas"

let location (fs: IFileSystem) (temporaryRoot: string) (session: string) =
    let root =
        if String.IsNullOrWhiteSpace temporaryRoot then
            fs.Path.GetTempPath()
        else
            temporaryRoot

    fs.Path.Combine(root, DirectoryName, session) |> FileSystemPath.create

let ensure (fs: IFileSystem) (path: FileSystemPath) =
    match FileSystemPath.kind fs path with
    | DirectoryEntry -> Ok path
    | _ ->
        attempting path (fun () -> fs.Directory.CreateDirectory(FileSystemPath.value path))
        |> Result.map (fun _ -> path)

let release (fs: IFileSystem) (path: FileSystemPath) =
    match FileSystemPath.kind fs path with
    | DirectoryEntry -> delete fs path |> ignore
    | _ -> ()
