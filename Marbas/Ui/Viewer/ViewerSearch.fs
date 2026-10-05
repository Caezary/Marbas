namespace Marbas.Ui.Viewer

open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.Messages

type SearchOffer =
    { Remembered: EnteredText list
      Last: WindowSearch.Query option }

type TypedAs =
    | AsText
    | AsExpression

type SearchAnswer =
    { Typed: string
      Case: LetterCase
      Words: WordMatch
      TypedAs: TypedAs
      Direction: SearchDirection }

type SearchHistory =
    { Texts: unit -> EnteredText list
      Remember: EnteredText -> unit }

type SearchReport =
    | TextNotFound of WindowSearch.Wanted
    | SearchFailed of ContentError
    | PatternRefused of PatternError

module ViewerSearch =

    [<Literal>]
    let Title = "Search"

    let progress length reached =
        $"Searching… %d{ViewerScroll.percent reached length}%%"

    let describe report =
        match report with
        | TextNotFound wanted -> $"'{WindowSearch.spelled wanted}' was not found."
        | SearchFailed error -> ContentErrorMessage.describe error
        | PatternRefused(PatternDoesNotParse reason) -> $"The regular expression does not parse: {reason}"
        | PatternRefused(PatternNotSupported reason) -> $"The regular expression cannot be searched for: {reason}"

    let private wantedFor (answer: SearchAnswer) text =
        match answer.TypedAs with
        | AsText -> Ok(WindowSearch.Literal(text, answer.Words))
        | AsExpression -> SearchPattern.tryCreate (FindText.value text) |> Result.map WindowSearch.Pattern

    let query (answer: SearchAnswer) =
        FindText.tryCreate answer.Typed
        |> Option.map (
            wantedFor answer
            >> Result.map (fun wanted ->
                ({ Wanted = wanted
                   Case = answer.Case
                   Direction = answer.Direction }
                : WindowSearch.Query))
        )
