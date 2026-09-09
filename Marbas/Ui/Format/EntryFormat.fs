module Marbas.Ui.Format.EntryFormat

open System.Globalization
open Marbas.Domain
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

let private sizeText = EntrySize.describe EntrySize.plain

let dateText entry =
    entry.Modified.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)

let format (width: int) (entry: Entry) =
    let nameWidth = width - SizeWidth - DateWidth - 2
    let name = EntryName.value entry.Name

    if nameWidth < MinNameWidth then
        fitName (max width 2) name
    else
        $"{fitName nameWidth name} {(sizeText entry).PadLeft SizeWidth} {dateText entry}"
