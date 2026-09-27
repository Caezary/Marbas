module Marbas.Tests.Ui.App.CursorInfoTests

open System
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.Ui.App

let private modified = DateTimeOffset(DateTime(2026, 7, 20, 14, 5, 0))

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

let private directory name =
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

[<Fact>]
let ``a link says what it points at after its name`` () =
    let link =
        { directory "twin" with
            LinkTarget = Some(FileSystemPath.create "/data/docs") }

    let text = CursorInfo.describe (Some link)

    Assert.StartsWith("twin -> /data/docs   <DIR>", text)

[<Fact>]
let ``an entry that is no link says nothing about a target`` () =
    let text = CursorInfo.describe (Some(file "readme.txt" 1L))

    Assert.DoesNotContain("->", text)

[<Fact>]
let ``a measured directory is described by what it holds`` () =
    let measured =
        { directory "docs" with
            Measured = Some(Exact 12345L) }

    let text = CursorInfo.describe (Some measured)

    Assert.Contains("12,345 bytes", text)
    Assert.DoesNotContain("<DIR>", text)

[<Fact>]
let ``a directory measured only in part says so`` () =
    let measured =
        { directory "docs" with
            Measured = Some(AtLeast 12345L) }

    let text = CursorInfo.describe (Some measured)

    Assert.Contains("≥12,345 bytes", text)
