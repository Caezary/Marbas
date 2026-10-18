module Marbas.Ui.Format.QueryText

open System.IO.Abstractions
open Marbas.Domain
open Marbas.Locations

let private sense search =
    match search.Sense with
    | Containing -> "containing"
    | NotContaining -> "not containing"

let describe (query: FindQuery) =
    let masks = NameMasks.spelled (FindQuery.masks query)

    match FindQuery.text query with
    | None -> masks
    | Some search -> $"%s{masks} %s{sense search} \"%s{FindText.value search.Text}\""

let where (fs: IFileSystem) (ground: FindGround) =
    ground.Roots |> List.map (PanelLocation.value fs) |> String.concat ", "
