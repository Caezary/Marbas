module Marbas.FileSystem.WindowSearch

open System
open Marbas.Domain

type Control =
    { Reached: int64 -> unit
      IsCancelled: unit -> bool }

type Match =
    { LineStart: int64
      Column: int
      Length: int }

type Answer =
    | Found of Match
    | NotFound
    | Stopped
    | Unreadable of ContentError

let private occurrence (text: FindText) (line: LineIndex.Line) =
    let wanted = FindText.value text

    match line.Text.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) with
    | -1 -> None
    | column ->
        Some
            { LineStart = line.Start
              Column = column
              Length = wanted.Length }

let private answerAt (control: Control) text step =
    match step with
    | LineIndex.Reading _ when control.IsCancelled() -> Some Stopped
    | LineIndex.Reading offset ->
        control.Reached offset
        None
    | LineIndex.LineRead line -> occurrence text line |> Option.map Found

let forward (control: Control) (source: FileWindow.Source) encoding (text: FindText) (from: int64) =
    try
        LineIndex.walkFrom source encoding from
        |> Seq.tryPick (answerAt control text)
        |> Option.defaultValue NotFound
    with ex ->
        Unreadable(ContentError.mapException source.Path ex)
