module Marbas.Ui.TimestampText

open System
open System.Globalization

[<Literal>]
let Pattern = "yyyy-MM-dd HH:mm:ss"

let format (stamp: DateTime) =
    stamp.ToString(Pattern, CultureInfo.InvariantCulture)

let tryParse (text: string) =
    match DateTime.TryParseExact(text.Trim(), Pattern, CultureInfo.InvariantCulture, DateTimeStyles.None) with
    | true, stamp -> Some stamp
    | _ -> None
