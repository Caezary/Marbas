module Marbas.Ui.Viewer.PrintableText

open System
open System.Text

[<Literal>]
let TabWidth = 8

[<Literal>]
let Placeholder = '.'

let describe (text: string) =
    let shown = StringBuilder(text.Length)

    for character in text do
        if character = '\t' then
            shown.Append(' ', TabWidth - shown.Length % TabWidth) |> ignore
        elif Char.IsControl character then
            shown.Append Placeholder |> ignore
        else
            shown.Append character |> ignore

    shown.ToString()
