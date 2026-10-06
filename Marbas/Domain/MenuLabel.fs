namespace Marbas.Domain

open System

type MenuLabel = private MenuLabel of string

module MenuLabel =

    let private breaksALine (raw: string) = raw.IndexOfAny [| '\n'; '\r' |] >= 0

    let tryCreate (raw: string) =
        if String.IsNullOrWhiteSpace raw || breaksALine raw then
            None
        else
            Some(MenuLabel(raw.Trim()))

    let value (MenuLabel text) = text
