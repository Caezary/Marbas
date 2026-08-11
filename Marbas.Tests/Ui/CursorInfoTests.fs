module Marbas.Tests.Ui.CursorInfoTests

open System
open Xunit
open Marbas.FileSystem.DirectoryListing
open Marbas.Ui

let private modified = DateTime(2026, 7, 20, 14, 5, 0)

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
let ``a file is described by name size and date`` () =
    let text = CursorInfo.describe (Some(file "readme.txt" 1234L))

    Assert.Contains("readme.txt", text)
    Assert.Contains("1,234", text)
    Assert.Contains("2026-07-20 14:05", text)

[<Fact>]
let ``a directory is described with DIR instead of a size`` () =
    let text = CursorInfo.describe (Some(directory "docs"))

    Assert.Contains("docs", text)
    Assert.Contains("<DIR>", text)

[<Fact>]
let ``long names are not truncated`` () =
    let name = String('a', 120)

    let text = CursorInfo.describe (Some(file name 1L))

    Assert.Contains(name, text)

[<Fact>]
let ``no entry under the cursor is described as empty text`` () =
    let text = CursorInfo.describe None

    Assert.Equal("", text)
