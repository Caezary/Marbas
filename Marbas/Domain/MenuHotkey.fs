namespace Marbas.Domain

open System

type MenuHotkey = private MenuHotkey of char

module MenuHotkey =

    let tryCreate (raw: string) =
        match raw.Trim() with
        | single when single.Length = 1 && Char.IsLetterOrDigit single[0] ->
            Some(MenuHotkey(Char.ToUpperInvariant single[0]))
        | _ -> None

    let value (MenuHotkey key) = key
