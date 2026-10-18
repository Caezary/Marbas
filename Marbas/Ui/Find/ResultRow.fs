module Marbas.Ui.Find.ResultRow

open System.IO.Abstractions
open Marbas.FileSystem
open Marbas.Find.FileSearch
open Marbas.Ui.Format

[<Literal>]
let private Indent = "  "

type ResultRow =
    | GroupHeader of PanelLocation
    | Hit of FindHit

let following (lastLocation: PanelLocation option) (hits: FindHit seq) =
    hits
    |> Seq.fold
        (fun (newestFirst, standing) hit ->
            if standing = Some hit.Location then
                Hit hit :: newestFirst, standing
            else
                Hit hit :: GroupHeader hit.Location :: newestFirst, Some hit.Location)
        ([], lastLocation)
    |> fst
    |> List.rev

let describe (fs: IFileSystem) width row =
    match row with
    | GroupHeader location -> PanelLocation.value fs location
    | Hit hit -> Indent + EntryFormat.format (width - Indent.Length) hit.Entry
