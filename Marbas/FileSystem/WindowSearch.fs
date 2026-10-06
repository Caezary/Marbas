module Marbas.FileSystem.WindowSearch

open System.Text.RegularExpressions
open Marbas.Domain

type Control =
    { Reached: int64 -> unit
      IsCancelled: unit -> bool }

type Wanted =
    | Literal of FindText * WordMatch
    | Pattern of SearchPattern

type Query =
    { Wanted: Wanted
      Case: LetterCase
      Direction: SearchDirection }

type Place = { LineStart: int64; Column: int }

type Match =
    { LineStart: int64
      Column: int
      Length: int }

type Answer<'found> =
    | Found of 'found
    | NotFound
    | Stopped
    | Unreadable of ContentError

let spelled wanted =
    match wanted with
    | Literal(text, _) -> FindText.value text
    | Pattern pattern -> SearchPattern.value pattern

let private bounded (text: string) index length =
    let before = index = 0 || not (WordMatch.isWordCharacter text[index - 1])

    let after =
        index + length = text.Length || not (WordMatch.isWordCharacter text[index + length])

    before && after

let private everyStart next (line: string) =
    Seq.unfold (fun from -> if from > line.Length then None else next from) 0

let private literalOccurrences (text: FindText) words case (line: string) =
    let wanted = FindText.value text
    let comparison = LetterCase.comparison case

    let next from =
        match line.IndexOf(wanted, from, comparison) with
        | -1 -> None
        | index -> Some((index, wanted.Length), index + 1)

    let accepted (index, length) =
        match words with
        | Anywhere -> true
        | WholeWords -> bounded line index length

    everyStart next line |> Seq.filter accepted

let private patternOccurrences (regex: Regex) (line: string) =
    let next from =
        let found = regex.Match(line, from)

        if found.Success then
            Some((found.Index, found.Length), found.Index + 1)
        else
            None

    everyStart next line |> Seq.filter (fun (_, length) -> length > 0)

let private occurrencesOf (query: Query) =
    match query.Wanted with
    | Literal(text, words) -> literalOccurrences text words query.Case
    | Pattern pattern -> patternOccurrences (SearchPattern.regex query.Case pattern)

let private firstFrom occurrences column =
    occurrences |> Seq.tryFind (fun (index, _) -> index >= column)

let private lastBefore occurrences column =
    occurrences |> Seq.takeWhile (fun (index, _) -> index < column) |> Seq.tryLast

let private matchIn (occurrencesIn: string -> (int * int) seq) direction (place: Place) (line: LineIndex.Line) =
    let occurrences = occurrencesIn line.Text
    let onStartingLine = line.Start = place.LineStart

    let found =
        match direction, onStartingLine with
        | Forwards, true -> firstFrom occurrences place.Column
        | Forwards, false -> firstFrom occurrences 0
        | Backwards, true -> lastBefore occurrences place.Column
        | Backwards, false -> lastBefore occurrences line.Text.Length

    found
    |> Option.map (fun (column, length) ->
        { LineStart = line.Start
          Column = column
          Length = length })

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

let private stepsFrom (source: FileWindow.Source) encoding direction (place: Place) =
    let startingLine = LineIndex.walkFrom source encoding place.LineStart

    match direction with
    | Forwards -> startingLine
    | Backwards -> Seq.append (throughFirstLine startingLine) (LineIndex.walkBackFrom source encoding place.LineStart)

let private answerAt (control: Control) matching step =
    match step with
    | LineIndex.Reading _ when control.IsCancelled() -> Some Stopped
    | LineIndex.Reading offset ->
        control.Reached offset
        None
    | LineIndex.LineRead line -> matching line |> Option.map Found

let run (control: Control) (source: FileWindow.Source) encoding (query: Query) (place: Place) =
    let matching = matchIn (occurrencesOf query) query.Direction place

    try
        stepsFrom source encoding query.Direction place
        |> Seq.tryPick (answerAt control matching)
        |> Option.defaultValue NotFound
    with ex ->
        Unreadable(ContentError.mapException source.Path ex)
