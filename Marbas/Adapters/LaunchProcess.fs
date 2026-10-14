module Marbas.Adapters.LaunchProcess

open System.ComponentModel
open System.Diagnostics
open System.IO
open System.IO.Abstractions
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

let detached command arguments =
    let start = argued command arguments
    start.UseShellExecute <- false
    start.RedirectStandardOutput <- true
    start.RedirectStandardError <- true
    start

let private programAndArguments (request: LaunchRequest) =
    let target = FileSystemPath.value request.Target

    match request.Program with
    | Some program -> ProgramPath.value program, [ target ]
    | None -> target, []

let private throughShell (request: LaunchRequest) =
    let program, arguments = programAndArguments request
    let start = argued program arguments
    start.UseShellExecute <- true

    match request.Elevation with
    | AsAdministrator -> start.Verb <- ElevatedVerb
    | AsUser -> ()

    start

let private onWindows windowProgram (request: LaunchRequest) =
    let program, arguments = programAndArguments request

    match request.Elevation, windowProgram request.WorkingDirectory program with
    | AsUser, Some resolved -> detached (FileSystemPath.value resolved) arguments
    | AsUser, None
    | AsAdministrator, _ -> throughShell request

let private ofItsOwn (fs: IFileSystem) (request: LaunchRequest) =
    let target = FileSystemPath.value request.Target

    match request.Program with
    | Some program -> detached (ProgramPath.value program) [ target ]
    | None when fs.Directory.Exists target -> detached DesktopOpener [ target ]
    | None ->
        match EntryAttributes.executability (fs.File.GetUnixFileMode target) with
        | Executable -> detached target []
        | NotExecutable -> detached DesktopOpener [ target ]

let describe (fs: IFileSystem) platform windowProgram (request: LaunchRequest) =
    match platform, request.Elevation with
    | Unix, AsAdministrator -> Error(LaunchRefused(request.Target, ElevationUnavailable))
    | Windows, _ -> Ok(onWindows windowProgram request)
    | Unix, AsUser -> Ok(ofItsOwn fs request)

let drained (child: Process) =
    child.StandardOutput.BaseStream.CopyToAsync Stream.Null |> ignore
    child.StandardError.BaseStream.CopyToAsync Stream.Null |> ignore

let private openerOf (described: ProcessStartInfo) =
    if described.FileName = DesktopOpener then
        ProgramPath.tryCreate DesktopOpener
    else
        None

let failure (described: ProcessStartInfo) (request: LaunchRequest) (ex: exn) =
    match ex with
    | :? Win32Exception as refusal ->
        match refusal.NativeErrorCode, request.Program, openerOf described with
        | NoAssociation, _, _ -> NothingAssociated request.Target
        | PromptCancelled, _, _ -> ElevationDeclined request.Target
        | FileNotFound, Some program, _ -> ProgramNotFound program
        | FileNotFound, None, Some opener -> OpenerNotInstalled opener
        | _ -> LaunchRefused(request.Target, refusal.Message)
    | _ -> LaunchRefused(request.Target, ex.Message)

let private describedOrRefused (fs: IFileSystem) platform windowProgram (request: LaunchRequest) =
    try
        describe fs platform windowProgram request
    with ex ->
        Error(LaunchRefused(request.Target, ex.Message))

let private run (request: LaunchRequest) (described: ProcessStartInfo) =
    try
        described.WorkingDirectory <- FileSystemPath.value request.WorkingDirectory

        match Process.Start described with
        | null -> ()
        | child ->
            if described.RedirectStandardOutput then
                drained child

        Ok()
    with ex ->
        Error(failure described request ex)

let start (fs: IFileSystem) platform windowProgram (request: LaunchRequest) =
    describedOrRefused fs platform windowProgram request
    |> Result.bind (run request)
