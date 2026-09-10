module Marbas.Adapters.DirectoryWatch

open System
open System.IO
open System.IO.Abstractions
open Terminal.Gui.App
open Marbas.Domain
open Marbas.FileSystem

[<Literal>]
let QuietMilliseconds = 200.0

let quiet = TimeSpan.FromMilliseconds QuietMilliseconds

type Target =
    | EveryEntryIn of directory: FileSystemPath
    | OneEntryIn of directory: FileSystemPath * name: EntryName

let private reported =
    NotifyFilters.FileName
    ||| NotifyFilters.DirectoryName
    ||| NotifyFilters.LastWrite
    ||| NotifyFilters.Size
    ||| NotifyFilters.Attributes

let target (fs: IFileSystem) (path: FileSystemPath) =
    match FileSystemPath.kind fs path with
    | DirectoryEntry -> EveryEntryIn path
    | FileEntry
    | NoEntry -> OneEntryIn(FileSystemPath.directory fs path, FileSystemPath.name fs path)

let collapsing (now: unit -> DateTimeOffset) (quiet: TimeSpan) (arm: (unit -> bool) -> unit) report =
    let last = ref (now ())
    let armed = ref false

    let settled () =
        if now () - last.Value < quiet then
            true
        else
            armed.Value <- false
            report ()
            false

    fun () ->
        last.Value <- now ()

        if not armed.Value then
            armed.Value <- true
            arm settled

let private filter target =
    match target with
    | EveryEntryIn directory -> FileSystemPath.value directory, "*"
    | OneEntryIn(directory, name) -> FileSystemPath.value directory, EntryName.value name

let private opened (fs: IFileSystem) touched target =
    let directory, name = filter target

    try
        let watcher = fs.FileSystemWatcher.New(directory, name)
        watcher.NotifyFilter <- reported
        watcher.IncludeSubdirectories <- false
        watcher.Created.Add(fun _ -> touched ())
        watcher.Deleted.Add(fun _ -> touched ())
        watcher.Changed.Add(fun _ -> touched ())
        watcher.Renamed.Add(fun _ -> touched ())
        watcher.EnableRaisingEvents <- true
        Some watcher
    with _ ->
        None

let watching (app: IApplication) (fs: IFileSystem) =
    let current = ref List.empty<IFileSystemWatcher>

    let arm (app: IApplication) settled =
        app.AddTimeout(quiet, fun () -> settled ()) |> ignore

    fun (paths: FileSystemPath list) report ->
        current.Value |> List.iter _.Dispose()

        let collapse = collapsing (fun () -> DateTimeOffset.Now) quiet (arm app) report
        let touched () = app.Invoke(fun () -> collapse ())

        current.Value <- paths |> List.map (target fs) |> List.choose (opened fs touched)
