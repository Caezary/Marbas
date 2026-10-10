namespace Marbas.Ui.Viewer

open Marbas.Domain
open Marbas.FileSystem
open Marbas.State.Messages

type LastSearch =
    | LastText of WindowSearch.Query option
    | LastBytes of ByteSearch.Query option

type SearchOffer =
    { Remembered: EnteredText list
      Last: LastSearch }

type TypedAs =
    | AsText
    | AsExpression
    | AsBytes

type SearchAnswer =
    { Typed: string
      Case: LetterCase
      Words: WordMatch
      TypedAs: TypedAs
      Direction: SearchDirection }

type Asked =
    | TextAsked of WindowSearch.Query
    | BytesAsked of ByteSearch.Query

type SearchRefusal =
    | BrokenPattern of PatternError
    | BrokenBytes of BytesError

type SearchReport =
    | TextNotFound of WindowSearch.Wanted
    | BytesNotFound of WantedBytes
    | SearchFailed of ContentError
    | Refused of SearchRefusal

module ViewerSearch =

    [<Literal>]
    let Title = "Search"

    let progress length reached =
        $"Searching… %d{ViewerScroll.percent reached length}%%"

    let private refusal refused =
        match refused with
        | BrokenPattern(PatternDoesNotParse reason) -> $"The regular expression does not parse: {reason}"
        | BrokenPattern(PatternNotSupported reason) -> $"The regular expression cannot be searched for: {reason}"
        | BrokenBytes(NotAByte token) ->
            $"'{token}' is not a byte. Type the bytes as hexadecimal digits, such as 4D 5A or 4D5A."
        | BrokenBytes NoBytes -> "Type the bytes to find as hexadecimal digits, such as 4D 5A."

    let describe report =
        match report with
        | TextNotFound wanted -> $"'{WindowSearch.spelled wanted}' was not found."
        | BytesNotFound bytes -> $"'{WantedBytes.spelled bytes}' was not found."
        | SearchFailed error -> ContentErrorMessage.describe error
        | Refused refused -> refusal refused

    let private textQuery (answer: SearchAnswer) wanted : WindowSearch.Query =
        { Wanted = wanted
          Case = answer.Case
          Direction = answer.Direction }

    let private bytesQuery (answer: SearchAnswer) bytes : ByteSearch.Query =
        { Bytes = bytes
          Direction = answer.Direction }

    let private askedFor (answer: SearchAnswer) text =
        match answer.TypedAs with
        | AsText -> Ok(TextAsked(textQuery answer (WindowSearch.Literal(text, answer.Words))))
        | AsExpression ->
            SearchPattern.tryCreate (FindText.value text)
            |> Result.map (WindowSearch.Pattern >> textQuery answer >> TextAsked)
            |> Result.mapError BrokenPattern
        | AsBytes ->
            WantedBytes.tryParse (FindText.value text)
            |> Result.map (bytesQuery answer >> BytesAsked)
            |> Result.mapError BrokenBytes

    let query (answer: SearchAnswer) =
        FindText.tryCreate answer.Typed |> Option.map (askedFor answer)
