module Marbas.Adapters.LaunchProcess

open System.ComponentModel
open System.Diagnostics
open System.IO
open System.IO.Abstractions
open Marbas
open Marbas.Domain
open Marbas.FileSystem

[<Literal>]
let DesktopOpener = "xdg-open"

[<Literal>]
let ElevatedVerb = "runas"

[<Literal>]
let ElevationUnavailable =
    "a terminal application cannot ask this system for administrator rights"

[<Literal>]
let private FileNotFound = 2

[<Literal>]
let private NoAssociation = 1155

[<Literal>]
let private PromptCancelled = 1223

let private argued (command: string) arguments =
    let start = ProcessStartInfo command
    arguments |> List.iter start.ArgumentList.Add
    start

let private throughShell (request: LaunchRequest) =
    let target = FileSystemPath.value request.Target

    let start =
        match request.Program with
        | Some program -> argued (ProgramPath.value program) [ target ]
        | None -> argued target []

    start.UseShellExecute <- true

    match request.Elevation with
    | AsAdministrator -> start.Verb <- ElevatedVerb
    | AsUser -> ()

    start

let private detached command arguments =
    let start = argued command arguments
    start.UseShellExecute <- false
    start.RedirectStandardOutput <- true
    start.RedirectStandardError <- true
    start

let private ofItsOwn (fs: IFileSystem) (request: LaunchRequest) =
    let target = FileSystemPath.value request.Target

    match request.Program with
    | Some program -> detached (ProgramPath.value program) [ target ]
    | None when fs.Directory.Exists target -> detached DesktopOpener [ target ]
    | None ->
        match Executability.ofMode (fs.FileInfo.New target).UnixFileMode with
        | Executable -> detached target []
        | NotExecutable -> detached DesktopOpener [ target ]

let describe (fs: IFileSystem) platform (request: LaunchRequest) =
    match platform, request.Elevation with
    | Unix, AsAdministrator -> Error(LaunchRefused(request.Target, ElevationUnavailable))
    | Windows, _ -> Ok(throughShell request)
    | Unix, AsUser -> Ok(ofItsOwn fs request)

let private drained (child: Process) =
    child.StandardOutput.BaseStream.CopyToAsync Stream.Null |> ignore
    child.StandardError.BaseStream.CopyToAsync Stream.Null |> ignore

let private failure (request: LaunchRequest) (ex: exn) =
    match ex with
    | :? Win32Exception as refusal ->
        match refusal.NativeErrorCode, request.Program with
        | NoAssociation, _ -> NothingAssociated request.Target
        | PromptCancelled, _ -> ElevationDeclined request.Target
        | FileNotFound, Some program -> ProgramNotFound program
        | _ -> LaunchRefused(request.Target, refusal.Message)
    | _ -> LaunchRefused(request.Target, ex.Message)

let start (fs: IFileSystem) platform (request: LaunchRequest) =
    try
        match describe fs platform request with
        | Error refusal -> Error refusal
        | Ok describing ->
            describing.WorkingDirectory <- FileSystemPath.value request.WorkingDirectory

            match Process.Start describing with
            | null -> ()
            | child -> if describing.RedirectStandardOutput then drained child

            Ok()
    with ex ->
        Error(failure request ex)
