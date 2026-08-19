module Marbas.Tests.Ui.PanelFooterTests

open System
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.State.AppState
open Marbas.Ui
open Marbas.Tests

let private modified = DateTime(2026, 7, 1, 12, 30, 0)

let private file name size =
    { Name = EntryName.create name
      Kind = File
      Size = Some size
      Modified = modified
      Visibility = Plain
      Executability = NotExecutable }

let private directory name =
    { Name = EntryName.create name
      Kind = Directory
      Size = None
      Modified = modified
      Visibility = Plain
      Executability = NotExecutable }

let private entries =
    [ directory ".."; directory "docs"; file "a.txt" 1000L; file "b.txt" 234L ]

let private panel =
    { Path = FileSystemPath.create (Root.at "start")
      Entries = entries
      Cursor = 0
      Selected = Set.empty
      Sort = SortOrder.byName
      Hidden = Shown
      Search = None }

[<Fact>]
let ``with nothing marked the footer counts the files of the directory`` () =
    let text = PanelFooter.describe panel

    Assert.Contains("2 files", text)
    Assert.Contains("1,234 bytes", text)

[<Fact>]
let ``directories are left out of the count and of the total`` () =
    let text = PanelFooter.describe { panel with Entries = [ directory ".."; directory "docs" ] }

    Assert.Contains("0 files", text)
    Assert.Contains("0 bytes", text)

[<Fact>]
let ``marked entries take the footer over`` () =
    let marked =
        { panel with Selected = Set.ofList [ EntryName.create "a.txt" ] }

    let text = PanelFooter.describe marked

    Assert.Contains("1 selected", text)
    Assert.Contains("1,000 bytes", text)

[<Fact>]
let ``an open search takes the footer over from everything else`` () =
    let searching =
        { panel with
            Selected = Set.ofList [ EntryName.create "a.txt" ]
            Search = Some(SearchText.ofChar 'a' |> SearchText.append '.') }

    let text = PanelFooter.describe searching

    Assert.Equal("Search: a.", text)
