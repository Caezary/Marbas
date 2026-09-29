namespace Marbas.Domain

open System

type SearchText = private SearchText of string

module SearchText =

    let ofChar (character: char) = SearchText(string character)

    let value (SearchText raw) = raw

    let append (character: char) (SearchText raw) = SearchText(raw + string character)

    let backspace (SearchText raw) =
        if raw.Length <= 1 then
            None
        else
            Some(SearchText(raw.Substring(0, raw.Length - 1)))

    let matches (SearchText raw) name =
        (EntryName.value name).Contains(raw, StringComparison.OrdinalIgnoreCase)

    let foundIn (text: string) (SearchText raw) =
        text.Contains(raw, StringComparison.OrdinalIgnoreCase)
