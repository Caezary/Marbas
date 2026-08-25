module Marbas.Ui.App.EditorCommand

type Platform =
    | Windows
    | Unix

[<Literal>]
let WindowsDefault = "notepad"

[<Literal>]
let UnixDefault = "vi"

let private fallback platform =
    match platform with
    | Windows -> WindowsDefault
    | Unix -> UnixDefault

let choose (configured: string option) platform =
    match configured |> Option.map _.Trim() with
    | Some "" | None -> fallback platform
    | Some editor -> editor
