module Marbas.Ui.EntryFormat

open System.Globalization
open Marbas.FileSystem.DirectoryListing

[<Literal>]
let private SizeWidth = 10

[<Literal>]
let private DateWidth = 16

[<Literal>]
let private MinNameWidth = 8

let private fitName width (name: string) =
    if name.Length <= width then
        name.PadRight width
    else
        name.Substring(0, width - 1) + "…"

let private sizeText entry =
    match entry.Kind with
    | Directory -> "<DIR>"
    | File -> entry.Size |> Option.map string |> Option.defaultValue ""

let format (width: int) (entry: Entry) =
    let nameWidth = width - SizeWidth - DateWidth - 2

    if nameWidth < MinNameWidth then
        fitName (max width 2) entry.Name
    else
        let date =
            entry.Modified.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)

        $"{fitName nameWidth entry.Name} {(sizeText entry).PadLeft SizeWidth} {date}"
