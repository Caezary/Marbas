module Marbas.Tests.Ui.SelectionSummaryTests

open System
open Xunit
open Marbas.FileSystem.DirectoryListing
open Marbas.Ui

let private modified = DateTime(2026, 7, 1, 12, 30, 0)

let private file name size =
    { Name = name
      Kind = File
      Size = Some size
      Modified = modified }

let private directory name =
    { Name = name
      Kind = Directory
      Size = None
      Modified = modified }

[<Fact>]
let ``nothing selected is summarised as empty text`` () =
    let text = SelectionSummary.describe []

    Assert.Equal("", text)

[<Fact>]
let ``the summary counts the entries and sums their sizes`` () =
    let selected = [ file "a.txt" 1000L; file "b.txt" 234L ]

    let text = SelectionSummary.describe selected

    Assert.Contains("2 selected", text)
    Assert.Contains("1,234 bytes", text)

[<Fact>]
let ``selected directories are counted but add no size`` () =
    let selected = [ directory "docs"; file "a.txt" 5L ]

    let text = SelectionSummary.describe selected

    Assert.Contains("2 selected", text)
    Assert.Contains("5 bytes", text)
