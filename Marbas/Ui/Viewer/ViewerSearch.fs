namespace Marbas.Ui.Viewer

open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.Messages

type SearchOffer =
    { Remembered: EnteredText list
      Last: FindText option }

type SearchHistory =
    { Texts: unit -> EnteredText list
      Remember: EnteredText -> unit }

type SearchReport =
    | TextNotFound of FindText
    | SearchFailed of ContentError

module ViewerSearch =

    [<Literal>]
    let Title = "Search"

    let progress length reached =
        $"Searching… %d{ViewerScroll.percent reached length}%%"

    let describe report =
        match report with
        | TextNotFound text -> $"'{FindText.value text}' was not found."
        | SearchFailed error -> ContentErrorMessage.describe error
