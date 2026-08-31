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

let scratch (fs: IFileSystem) (workspace: FileSystemPath) =
    let rec free root ordinal =
        let path = FileSystemPath.combine fs root (string (ordinal: int))

        if FileSystemPath.kind fs path = NoEntry then path else free root (ordinal + 1)

    ensure fs workspace
    |> Result.bind (fun root ->
        let path = free root 1

        attempting path (fun () -> fs.Directory.CreateDirectory(FileSystemPath.value path))
        |> Result.map (fun _ -> path))

let release (fs: IFileSystem) (path: FileSystemPath) =
    match FileSystemPath.kind fs path with
    | DirectoryEntry -> delete fs path |> ignore
    | _ -> ()
