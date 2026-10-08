namespace Marbas.State

open System
open System.IO.Abstractions
open Marbas.FileSystem

type CrashSite =
    | UiThread
    | OtherThread
    | UnobservedTask

type LogEntry = Crashed of site: CrashSite * failure: exn

module LogEntry =

    [<Literal>]
    let private MomentFormat = "o"

    let private siteText site =
        match site with
        | UiThread -> "on the UI thread"
        | OtherThread -> "on another thread"
        | UnobservedTask -> "in a task nobody awaited"

    let private text entry =
        match entry with
        | Crashed(site, failure) -> $"crash {siteText site}: {failure}"

    let lines (moment: DateTimeOffset) entry =
        let stamp = moment.ToString MomentFormat

        (text entry).ReplaceLineEndings("\n").Split '\n'
        |> Array.map (fun line -> $"{stamp} {line}")
        |> List.ofArray

    let write (fs: IFileSystem) (path: FileSystemPath) (moment: DateTimeOffset) entry =
        lines moment entry |> LogFile.append fs path
