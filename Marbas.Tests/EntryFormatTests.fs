module Marbas.Tests.EntryFormatTests

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

let private dir name =
    { Name = name
      Kind = Directory
      Size = None
      Modified = modified }

[<Fact>]
let ``file row has name, right-aligned size and date columns`` () =
    Assert.Equal(
        "notes.txt" + String(' ', 13) + "5 2026-07-01 12:30",
        EntryFormat.format 40 (file "notes.txt" 5L)
    )

[<Fact>]
let ``directory row shows DIR marker instead of size`` () =
    Assert.Equal(
        "docs" + String(' ', 14) + "<DIR> 2026-07-01 12:30",
        EntryFormat.format 40 (dir "docs")
    )

[<Fact>]
let ``every row is exactly as wide as requested`` () =
    let entries =
        [ file "a.txt" 12345L
          file "very_long_file_name_that_overflows.txt" 1L
          dir ".." ]

    for entry in entries do
        Assert.Equal(40, (EntryFormat.format 40 entry).Length)

[<Fact>]
let ``too long names are truncated with ellipsis`` () =
    let row = EntryFormat.format 40 (file "very_long_file_name_that_overflows.txt" 1L)
    Assert.StartsWith("very_long_f…", row)

[<Fact>]
let ``narrow width falls back to name only`` () =
    Assert.Equal("abc.txt   ", EntryFormat.format 10 (file "abc.txt" 5L))
    Assert.Equal("very_long…", EntryFormat.format 10 (file "very_long_name.txt" 5L))
