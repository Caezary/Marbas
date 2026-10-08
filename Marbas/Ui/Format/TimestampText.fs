module Marbas.Ui.Format.TimestampText

open System
open System.Globalization

[<Literal>]
let Pattern = "yyyy-MM-dd HH:mm:ss"

let format (stamp: DateTimeOffset) =
    stamp.ToString(Pattern, CultureInfo.InvariantCulture)

let tryParse (text: string) =
    match
        DateTimeOffset.TryParseExact(text.Trim(), Pattern, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal)
    with
    | true, stamp -> Some stamp
    | _ -> None
