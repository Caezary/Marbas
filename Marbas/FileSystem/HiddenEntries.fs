namespace Marbas.FileSystem

open System.IO

type EntryVisibility =
    | Plain
    | Hidden

type HiddenEntries =
    | Shown
    | Concealed

module EntryVisibility =

    let private marked (attributes: FileAttributes) =
        attributes.HasFlag FileAttributes.Hidden || attributes.HasFlag FileAttributes.System

    let classify (name: EntryName) (attributes: FileAttributes) =
        if (EntryName.value name).StartsWith '.' || marked attributes then
            Hidden
        else
            Plain

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
