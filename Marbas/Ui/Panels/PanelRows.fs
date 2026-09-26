module Marbas.Ui.Panels.PanelRows

open Marbas.Domain
open Marbas.FileSystem.DirectoryListing
open Marbas.State

type Painting =
    { Entries: Entry array
      Selected: Set<EntryName>
      Width: int }

type Rows =
    | Kept
    | Rebuilt

let painting (width: int) (panel: PanelState) =
    { Entries = panel.Entries
      Selected = panel.Selected
      Width = width }

let private stands (painted: Painting) (current: Painting) =
    painted.Width = current.Width
    && obj.ReferenceEquals(painted.Entries, current.Entries)
    && obj.ReferenceEquals(painted.Selected, current.Selected)

let rows (painted: Painting option) (current: Painting) =
    match painted with
    | Some previous when stands previous current -> Kept
    | _ -> Rebuilt
