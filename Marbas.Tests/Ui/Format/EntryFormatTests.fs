module Marbas.Tests.Ui.Format.EntryFormatTests

open System
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.Ui.Format

let private modified = DateTimeOffset(DateTime(2026, 7, 1, 12, 30, 0))

let private file name size =
    { Name = EntryName.create name
      Kind = File
      Size = Some size
      Measured = None
      Modified = modified
      Visibility = Plain
      Executability = NotExecutable
      LinkTarget = None
      Verification = Verified }

let private dir name =
    { Name = EntryName.create name
      Kind = Directory
      Size = None
      Measured = None
      Modified = modified
      Visibility = Plain
      Executability = NotExecutable
      LinkTarget = None
      Verification = Verified }

[<Fact>]
let ``file row has name, right-aligned size and date columns`` () =
    let row = EntryFormat.format 40 (file "notes.txt" 5L)

    Assert.Equal("notes.txt" + String(' ', 11) + "5 B 2026-07-01 12:30", row)

[<Fact>]
let ``directory row shows DIR marker instead of size`` () =
    let row = EntryFormat.format 40 (dir "docs")

    Assert.Equal("docs" + String(' ', 14) + "<DIR> 2026-07-01 12:30", row)

[<Fact>]
let ``every row is exactly as wide as requested`` () =
    let entries =
        [ file "a.txt" 12345L
          file "huge.iso" 10000000000L
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

let private measured name size =
    { dir name with Measured = Some size }

[<Theory>]
[<InlineData(512L, "512 B")>]
[<InlineData(1536L, "1.5 KB")>]
[<InlineData(5767168L, "5.5 MB")>]
[<InlineData(10000000000L, "9.3 GB")>]
[<InlineData(2199023255552L, "2.0 TB")>]
let ``a file's size is spelled compactly in its column`` (bytes: int64) (expected: string) =
    let row = EntryFormat.format 40 (file "a.bin" bytes)

    Assert.EndsWith($" {expected} 2026-07-01 12:30", row)

[<Fact>]
let ``a measured directory shows its size compactly`` () =
    let row = EntryFormat.format 40 (measured "docs" (Exact 3221225472L))

    Assert.EndsWith(" 3.0 GB 2026-07-01 12:30", row)

[<Fact>]
let ``a directory measured only in part keeps its at-least mark`` () =
    let row = EntryFormat.format 40 (measured "docs" (AtLeast 1536L))

    Assert.EndsWith(" ≥1.5 KB 2026-07-01 12:30", row)

[<Fact>]
let ``the longest compact size fits its column whole`` () =
    let row = EntryFormat.format 40 (measured "docs" (AtLeast 1047551L))

    Assert.Equal(40, row.Length)
    Assert.Contains(" ≥1023.0 KB ", row)
