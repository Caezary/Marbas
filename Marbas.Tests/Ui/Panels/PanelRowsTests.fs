module Marbas.Tests.Ui.Panels.PanelRowsTests

open System
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.State.AppState
open Marbas.Ui.Panels
open Marbas.Tests

let private modified = DateTimeOffset(DateTime(2026, 7, 1, 12, 30, 0))

let private file name =
    { Name = EntryName.create name
      Kind = File
      Size = Some 10L
      Measured = None
      Modified = modified
      Visibility = Plain
      Executability = NotExecutable
      LinkTarget = None }

let private panelOf entries =
    { Path = OnDisk(FileSystemPath.create (Root.at "start"))
      Entries = entries
      Cursor = 0
      Remembered = None
      Selected = Set.empty
      Sort = SortOrder.byName
      Hidden = Concealed
      Search = None }

let private panel = panelOf [| file "a.txt"; file "b.txt" |]

[<Fact>]
let ``a painting carries the panel's own entries and marks`` () =
    let marked = { panel with Selected = Set.ofList [ EntryName.create "a.txt" ] }

    let painting = PanelRows.painting 40 marked

    Assert.Same(marked.Entries, painting.Entries)
    Assert.Same(marked.Selected, painting.Selected)
    Assert.Equal(40, painting.Width)

[<Fact>]
let ``rows are kept when the same entries, marks and width are painted again`` () =
    let painted = PanelRows.painting 40 panel

    let rows = PanelRows.rows (Some painted) (PanelRows.painting 40 panel)

    Assert.Equal(PanelRows.Kept, rows)

[<Fact>]
let ``rows are rebuilt when nothing has been painted yet`` () =
    let painting = PanelRows.painting 40 panel

    let rows = PanelRows.rows None painting

    Assert.Equal(PanelRows.Rebuilt, rows)

[<Fact>]
let ``rows are rebuilt when the entries are another array of the same names`` () =
    let painted = PanelRows.painting 40 panel
    let reloaded = panelOf [| file "a.txt"; file "b.txt" |]

    let rows = PanelRows.rows (Some painted) (PanelRows.painting 40 reloaded)

    Assert.Equal(PanelRows.Rebuilt, rows)

[<Fact>]
let ``rows are rebuilt when a mark was added`` () =
    let painted = PanelRows.painting 40 panel
    let marked = { panel with Selected = Set.ofList [ EntryName.create "a.txt" ] }

    let rows = PanelRows.rows (Some painted) (PanelRows.painting 40 marked)

    Assert.Equal(PanelRows.Rebuilt, rows)

[<Fact>]
let ``rows are rebuilt when the panel changed width`` () =
    let painted = PanelRows.painting 40 panel

    let rows = PanelRows.rows (Some painted) (PanelRows.painting 60 panel)

    Assert.Equal(PanelRows.Rebuilt, rows)

[<Fact>]
let ``rows are kept when only the cursor moved`` () =
    let painted = PanelRows.painting 40 panel

    let rows = PanelRows.rows (Some painted) (PanelRows.painting 40 { panel with Cursor = 1 })

    Assert.Equal(PanelRows.Kept, rows)
