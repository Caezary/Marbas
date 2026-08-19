module Marbas.Tests.Ui.EntrySizeTests

open System
open System.Globalization
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.Ui

let private modified = DateTime(2026, 7, 1, 12, 30, 0)

let private entry kind size =
    { Name = EntryName.create "entry"
      Kind = kind
      Size = size
      Modified = modified
      Visibility = Plain
      Executability = NotExecutable }

[<Fact>]
let ``a file size is rendered by the given format`` () =
    let grouped (size: int64) = size.ToString("N0", CultureInfo.InvariantCulture)

    let plain = EntrySize.describe string (entry File (Some 12345L))
    let separated = EntrySize.describe grouped (entry File (Some 12345L))

    Assert.Equal("12345", plain)
    Assert.Equal("12,345", separated)

[<Fact>]
let ``a directory shows the DIR marker whatever the format`` () =
    let text = EntrySize.describe string (entry Directory None)

    Assert.Equal(EntrySize.DirectoryMark, text)

[<Fact>]
let ``a file of unknown size shows nothing`` () =
    let text = EntrySize.describe string (entry File None)

    Assert.Equal("", text)
