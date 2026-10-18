module Marbas.Adapters.LaunchProcess

open System
open System.ComponentModel
open System.Diagnostics
open System.IO
open System.IO.Abstractions
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Programs

[<Literal>]
let DesktopOpener = "xdg-open"

[<Literal>]
let ElevatedVerb = "runas"

[<Literal>]
let WindowsShell = "cmd.exe"

[<Literal>]
let ElevationUnavailable =
    "a terminal application cannot ask this system for administrator rights"

[<Literal>]
let private FileNotFound = 2

[<Literal>]
let private NoAssociation = 1155

[<Literal>]
let private PromptCancelled = 1223

type DrainedProgram =
    | WindowImage of image: FileSystemPath
    | BatchScript of script: FileSystemPath

let private argued (command: string) arguments =
    let start = ProcessStartInfo command
    arguments |> List.iter start.ArgumentList.Add
    start

let private drainedStreams (start: ProcessStartInfo) =
    start.UseShellExecute <- false
    start.RedirectStandardOutput <- true
    start.RedirectStandardError <- true
    start

let detached command arguments =
    argued command arguments |> drainedStreams

let private trailingBackslashes (word: string) = word.Length - word.TrimEnd('\\').Length

let private quotedForCmd (word: string) =
    "\"" + word + String('\\', trailingBackslashes word) + "\""

let private scriptLine (script: FileSystemPath) arguments =
    FileSystemPath.value script :: arguments
    |> List.map quotedForCmd
    |> String.concat " "

let private throughCmdWithoutWindow (script: FileSystemPath) arguments =
    let start =
        ProcessStartInfo(WindowsShell, $"/d /s /c \"{scriptLine script arguments}\"")
        |> drainedStreams

    start.RedirectStandardInput <- true
    start.CreateNoWindow <- true
    start

let detachedFrom (program: DrainedProgram) arguments =
    match program with
    | WindowImage image -> detached (FileSystemPath.value image) arguments
    | BatchScript script -> throughCmdWithoutWindow script arguments

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

let private drainedUnlessOpenedItself (request: LaunchRequest) (resolved: DrainedProgram) =
    match resolved, request.Program with
    | BatchScript _, None -> None
    | _ -> Some resolved

let private onWindows drainedProgram (request: LaunchRequest) =
    let program, arguments = programAndArguments request

    let drained =
        drainedProgram request.WorkingDirectory program
        |> Option.bind (drainedUnlessOpenedItself request)

    match request.Elevation, drained with
    | AsUser, Some resolved -> detachedFrom resolved arguments
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

let describe (fs: IFileSystem) platform drainedProgram (request: LaunchRequest) =
    match platform, request.Elevation with
    | Unix, AsAdministrator -> Error(LaunchRefused(request.Target, ElevationUnavailable))
    | Windows, _ -> Ok(onWindows drainedProgram request)
    | Unix, AsUser -> Ok(ofItsOwn fs request)

let drained (child: Process) =
    if child.StartInfo.RedirectStandardInput then
        child.StandardInput.Close()

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

let private describedOrRefused (fs: IFileSystem) platform drainedProgram (request: LaunchRequest) =
    try
        describe fs platform drainedProgram request
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

let start (fs: IFileSystem) platform drainedProgram (request: LaunchRequest) =
    describedOrRefused fs platform drainedProgram request
    |> Result.bind (run request)
