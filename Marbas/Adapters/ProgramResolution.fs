module Marbas.Adapters.ProgramResolution

open System
open System.IO
open System.IO.Abstractions
open Microsoft.Win32
open Marbas.FileSystem

[<Literal>]
let private AppPaths = @"Software\Microsoft\Windows\CurrentVersion\App Paths"

[<Literal>]
let private ExecutableExtension = ".exe"

type ProgramSearch =
    { Directories: FileSystemPath list
      Extensions: string list
      AppPath: string -> string option }

let private spellings (fs: IFileSystem) (search: ProgramSearch) (name: string) =
    if fs.Path.HasExtension name then
        [ name ]
    else
        search.Extensions |> List.map (fun extension -> name + extension)

let private existing (fs: IFileSystem) (paths: FileSystemPath list) =
    paths |> List.tryFind (fun path -> fs.File.Exists(FileSystemPath.value path))

let private asWritten (fs: IFileSystem) search workingDirectory name =
    spellings fs search name
    |> List.map (FileSystemPath.resolvedFrom fs workingDirectory)
    |> existing fs

let private onSearchPath (fs: IFileSystem) search name =
    search.Directories
    |> List.collect (fun directory -> spellings fs search name |> List.map (FileSystemPath.combine fs directory))
    |> existing fs

let private registeredKey (fs: IFileSystem) (name: string) =
    if fs.Path.HasExtension name then
        name
    else
        name + ExecutableExtension

let private registered (fs: IFileSystem) search name =
    search.AppPath(registeredKey fs name)
    |> Option.map (fun raw -> raw.Trim().Trim('"') |> FileSystemPath.create)
    |> Option.toList
    |> existing fs

let private found (fs: IFileSystem) search workingDirectory name =
    match asWritten fs search workingDirectory name with
    | Some program -> Some program
    | None when FileSystemPath.isBareName fs name ->
        onSearchPath fs search name
        |> Option.orElseWith (fun () -> registered fs search name)
    | None -> None

let resolve (fs: IFileSystem) (search: ProgramSearch) (workingDirectory: FileSystemPath) (name: string) =
    try
        found fs search workingDirectory name
    with
    | :? ArgumentException
    | :? NotSupportedException
    | :? IOException
    | :? UnauthorizedAccessException -> None

let windowProgram (fs: IFileSystem) (search: ProgramSearch) (workingDirectory: FileSystemPath) (name: string) =
    resolve fs search workingDirectory name
    |> Option.filter (fun program -> ProgramImage.subsystem fs program = WindowSubsystem)

let registeredAppPath (key: string) =
    [ Registry.CurrentUser; Registry.LocalMachine ]
    |> List.tryPick (fun root -> RegistryHandlers.defaultValueIn root $@"{AppPaths}\{key}")
