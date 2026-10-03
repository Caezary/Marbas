namespace Marbas.Domain

open System

type ClipboardText = private ClipboardText of lines: string list

module ClipboardText =

    let tryCreate (lines: string list) =
        match lines with
        | [] -> None
        | _ -> Some(ClipboardText lines)

    let value (ClipboardText lines) = lines

    let joined (lineBreak: string) (ClipboardText lines) = String.Join(lineBreak, lines)
