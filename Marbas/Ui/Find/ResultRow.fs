module Marbas.Ui.Find.ResultRow

open System.Collections.Generic
open System.IO.Abstractions
open Marbas.FileSystem
open Marbas.FileSystem.FileSearch
open Marbas.Ui.Format

[<Literal>]
let private Indent = "  "

type ResultRow =
    | GroupHeader of PanelLocation
    | Hit of FindHit

let rows (hits: FindHit list) =
    let order = ResizeArray<PanelLocation>()
    let grouped = Dictionary<PanelLocation, ResizeArray<FindHit>>()

    for hit in hits do
        match grouped.TryGetValue hit.Location with
        | true, found -> found.Add hit
        | _ ->
            grouped[hit.Location] <- ResizeArray [ hit ]
            order.Add hit.Location

    [ for location in order do
          yield GroupHeader location

          for hit in grouped[location] do
              yield Hit hit ]

let describe (fs: IFileSystem) width row =
    match row with
    | GroupHeader location -> PanelLocation.value fs location
    | Hit hit -> Indent + EntryFormat.format (width - Indent.Length) hit.Entry
