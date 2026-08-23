module Marbas.Tests.Ui.EntryFormatTests

open System
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.Ui

let private modified = DateTimeOffset(DateTime(2026, 7, 1, 12, 30, 0))

let private file name size =
    { Name = EntryName.create name
      Kind = File
      Size = Some size
      Modified = modified
      Visibility = Plain
      Executability = NotExecutable }

let private dir name =
    { Name = EntryName.create name
      Kind = Directory
      Size = None
      Modified = modified
      Visibility = Plain
      Executability = NotExecutable }

[<Fact>]
let ``file row has name, right-aligned size and date columns`` () =
    let row = EntryFormat.format 40 (file "notes.txt" 5L)

    Assert.Equal("notes.txt" + String(' ', 13) + "5 2026-07-01 12:30", row)

[<Fact>]
let ``directory row shows DIR marker instead of size`` () =
    let row = EntryFormat.format 40 (dir "docs")

    Assert.Equal("docs" + String(' ', 14) + "<DIR> 2026-07-01 12:30", row)

[<Fact>]
let ``every row is exactly as wide as requested`` () =
    let entries =
        [ file "a.txt" 12345L
          file "very_long_file_name_that_overflows.txt" 1L
          dir ".." ]

    let rows = entries |> List.map (EntryFormat.format 40)

    Assert.All(rows, fun row -> Assert.Equal(40, row.Length))

[<Fact>]
let ``too long names are truncated with ellipsis`` () =
    let row = EntryFormat.format 40 (file "very_long_file_name_that_overflows.txt" 1L)

    Assert.StartsWith("very_long_f…", row)

[<Fact>]
let ``narrow width falls back to name only`` () =
    let shortName = EntryFormat.format 10 (file "abc.txt" 5L)
    let longName = EntryFormat.format 10 (file "very_long_name.txt" 5L)

    Assert.Equal("abc.txt   ", shortName)
    Assert.Equal("very_long…", longName)
