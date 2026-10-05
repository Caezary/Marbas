module Marbas.FileSystem.WindowSearch

open Marbas.Domain

type Control =
    { Reached: int64 -> unit
      IsCancelled: unit -> bool }

type Query =
    { Text: FindText
      Case: LetterCase
      Words: WordMatch
      Direction: SearchDirection }

type Place = { LineStart: int64; Column: int }

type Match =
    { LineStart: int64
      Column: int
      Length: int }

type Answer =
    | Found of Match
    | NotFound
    | Stopped
    | Unreadable of ContentError

let private bounded (text: string) index length =
    let before = index = 0 || not (WordMatch.isWordCharacter text[index - 1])

    let after =
        index + length = text.Length || not (WordMatch.isWordCharacter text[index + length])

    before && after

let private occurrences (query: Query) (text: string) =
    let wanted = FindText.value query.Text
    let comparison = LetterCase.comparison query.Case

    let next from =
        match text.IndexOf(wanted, from, comparison) with
        | -1 -> None
        | index -> Some(index, index + 1)

    let accepted index =
        match query.Words with
        | Anywhere -> true
        | WholeWords -> bounded text index wanted.Length

    Seq.unfold (fun from -> if from > text.Length then None else next from) 0
    |> Seq.filter accepted

let private firstFrom query (line: LineIndex.Line) column =
    occurrences query line.Text |> Seq.tryFind (fun index -> index >= column)

let private lastBefore query (line: LineIndex.Line) column =
    occurrences query line.Text
    |> Seq.takeWhile (fun index -> index < column)
    |> Seq.tryLast

let private matchIn (query: Query) (place: Place) (line: LineIndex.Line) =
    let onStartingLine = line.Start = place.LineStart

    let found =
        match query.Direction, onStartingLine with
        | Forwards, true -> firstFrom query line place.Column
        | Forwards, false -> firstFrom query line 0
        | Backwards, true -> lastBefore query line place.Column
        | Backwards, false -> lastBefore query line line.Text.Length

    found
    |> Option.map (fun column ->
        { LineStart = line.Start
          Column = column
          Length = (FindText.value query.Text).Length })

let private throughFirstLine (steps: LineIndex.Step seq) =
    seq {
        use walked = steps.GetEnumerator()
        let reading = ref true

        while reading.Value && walked.MoveNext() do
            yield walked.Current

            match walked.Current with
            | LineIndex.LineRead _ -> reading.Value <- false
            | LineIndex.Reading _ -> ()
    }

let private stepsFrom (source: FileWindow.Source) encoding (query: Query) (place: Place) =
    let startingLine = LineIndex.walkFrom source encoding place.LineStart

    match query.Direction with
    | Forwards -> startingLine
    | Backwards -> Seq.append (throughFirstLine startingLine) (LineIndex.walkBackFrom source encoding place.LineStart)

let private answerAt (control: Control) query place step =
    match step with
    | LineIndex.Reading _ when control.IsCancelled() -> Some Stopped
    | LineIndex.Reading offset ->
        control.Reached offset
        None
    | LineIndex.LineRead line -> matchIn query place line |> Option.map Found

let run (control: Control) (source: FileWindow.Source) encoding (query: Query) (place: Place) =
    try
        stepsFrom source encoding query place
        |> Seq.tryPick (answerAt control query place)
        |> Option.defaultValue NotFound
    with ex ->
        Unreadable(ContentError.mapException source.Path ex)
