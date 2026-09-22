module Marbas.Ui.Find.ResultRow

open System.IO.Abstractions
open Marbas.FileSystem
open Marbas.FileSystem.FileSearch
open Marbas.Ui.Format

[<Literal>]
let private Indent = "  "

type ResultRow =
    | GroupHeader of PanelLocation
    | Hit of FindHit

let following (opened: PanelLocation option) (hits: FindHit seq) =
    let rows = ResizeArray<ResultRow>()
    let mutable standing = opened

    for hit in hits do
        if standing <> Some hit.Location then
            rows.Add(GroupHeader hit.Location)
            standing <- Some hit.Location

        rows.Add(Hit hit)

    List.ofSeq rows

let describe (fs: IFileSystem) width row =
    match row with
    | GroupHeader location -> PanelLocation.value fs location
    | Hit hit -> Indent + EntryFormat.format (width - Indent.Length) hit.Entry
