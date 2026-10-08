module Marbas.Adapters.DirectoryWatch

open System
open System.IO
open System.IO.Abstractions
open Terminal.Gui.App
open Marbas.Domain
open Marbas.FileSystem

[<Literal>]
let QuietMilliseconds = 200.0

[<Literal>]
let PollMilliseconds = 2000.0

let quiet = TimeSpan.FromMilliseconds QuietMilliseconds

let poll = TimeSpan.FromMilliseconds PollMilliseconds

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

let standing (fs: IFileSystem) paths =
    paths
    |> List.filter (fun path -> FileSystemPath.kind fs path <> NoEntry)
    |> Set.ofList

let collapsing (now: unit -> DateTimeOffset) (quiet: TimeSpan) (arm: (unit -> bool) -> unit) ready report =
    let last = ref (now ())
    let armed = ref false

    let settled () =
        if now () - last.Value < quiet || not (ready ()) then
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

let private startedWatcher (fs: IFileSystem) touched failed target =
    let directory, name = filter target

    try
        let watcher = fs.FileSystemWatcher.New(directory, name)
        watcher.NotifyFilter <- reported
        watcher.IncludeSubdirectories <- false
        watcher.Created.Add(fun _ -> touched ())
        watcher.Deleted.Add(fun _ -> touched ())
        watcher.Changed.Add(fun _ -> touched ())
        watcher.Renamed.Add(fun _ -> touched ())
        watcher.Error.Add(fun _ -> failed ())
        watcher.EnableRaisingEvents <- true
        Some watcher
    with _ ->
        None

let watching (app: IApplication) (fs: IFileSystem) =
    let watchers = ref List.empty<IFileSystemWatcher>
    let paths = ref List.empty<FileSystemPath>
    let seen = ref Set.empty
    let broken = ref false
    let collapse = ref ignore

    let panelsInFront () = Seq.length app.SessionStack <= 1

    let scheduling settled =
        app.AddTimeout(quiet, fun () -> settled ()) |> ignore

    let touched () = app.Invoke(fun () -> collapse.Value())

    let failed () =
        app.Invoke(fun () -> broken.Value <- true)

    let rewatch () =
        watchers.Value |> List.iter _.Dispose()

        watchers.Value <-
            paths.Value
            |> List.map (target fs)
            |> List.choose (startedWatcher fs touched failed)

        seen.Value <- standing fs paths.Value

    let looking () =
        if broken.Value || standing fs paths.Value <> seen.Value then
            broken.Value <- false
            rewatch ()
            touched ()

        true

    app.AddTimeout(poll, looking) |> ignore

    fun (wanted: FileSystemPath list) report ->
        collapse.Value <- collapsing (fun () -> DateTimeOffset.Now) quiet scheduling panelsInFront report
        paths.Value <- wanted
        broken.Value <- false
        rewatch ()
