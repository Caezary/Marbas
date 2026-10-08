namespace Marbas.Domain

type EntryVisibility =
    | Plain
    | Hidden

type HiddenEntries =
    | Shown
    | Concealed

module EntryVisibility =

    let ofName (name: EntryName) =
        if (EntryName.value name).StartsWith '.' then
            Hidden
        else
            Plain

    let classify name marked =
        match ofName name with
        | Hidden -> Hidden
        | Plain -> marked

module HiddenEntries =

    let toggle hidden =
        match hidden with
        | Shown -> Concealed
        | Concealed -> Shown

    let admits hidden visibility =
        match hidden, visibility with
        | Shown, _ -> true
        | Concealed, Plain -> true
        | Concealed, Hidden -> false
