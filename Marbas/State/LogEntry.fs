namespace Marbas.State

open System
open System.IO.Abstractions
open Microsoft.FSharp.Reflection
open Marbas.Domain
open Marbas.FileSystem
open Marbas.State.Update

type CrashSite =
    | UiThread
    | OtherThread
    | UnobservedTask

type LogEntry =
    | MessageHandled of message: Message * panel: PanelSide
    | EffectInterpreted of effect: Effect
    | BatchStarted of title: string
    | BatchFinished of title: string * items: int
    | BatchCancelled of title: string * items: int
    | ErrorReported of title: string * message: string
    | Crashed of site: CrashSite * failure: exn

module LogEntry =

    [<Literal>]
    let private MomentFormat = "o"

    let private namedPanel message =
        match message with
        | PanelFocused side
        | RootRequested side
        | PathOpened(side, _)
        | DriveChosen(side, _) -> Some side
        | CursorMoved cursor
        | SelectionToggledTo cursor -> Some cursor.Side
        | _ -> None

    let handling message (state: AppState.AppState) =
        MessageHandled(message, namedPanel message |> Option.defaultValue state.Active)

    let private caseName<'union> (value: 'union) =
        let case, _ = FSharpValue.GetUnionFields(value, typeof<'union>)
        case.Name

    let private panelText side =
        match side with
        | Left -> "left"
        | Right -> "right"

    let private siteText site =
        match site with
        | UiThread -> "on the UI thread"
        | OtherThread -> "on another thread"
        | UnobservedTask -> "in a task nobody awaited"

    let private text entry =
        match entry with
        | MessageHandled(message, side) -> $"message {caseName message} on the {panelText side} panel"
        | EffectInterpreted effect -> $"effect {caseName effect}"
        | BatchStarted title -> $"batch started: {title}"
        | BatchFinished(title, items) -> $"batch finished: {title}, {items} items"
        | BatchCancelled(title, items) -> $"batch cancelled: {title}, {items} items"
        | ErrorReported(title, message) -> $"error shown: {title}: {message}"
        | Crashed(site, failure) -> $"crash {siteText site}: {failure}"

    let lines (moment: DateTimeOffset) entry =
        let stamp = moment.ToString MomentFormat

        (text entry).ReplaceLineEndings("\n").Split '\n'
        |> Array.map (fun line -> $"{stamp} {line}")
        |> List.ofArray

    let write (fs: IFileSystem) (path: FileSystemPath) (moment: DateTimeOffset) entry =
        lines moment entry |> LogFile.append fs path
