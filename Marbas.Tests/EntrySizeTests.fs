module Marbas.Tests.EntrySizeTests

open System
open System.Globalization
open Xunit
open Marbas.FileSystem.DirectoryListing
open Marbas.Ui

let private modified = DateTime(2026, 7, 1, 12, 30, 0)

let private entry kind size =
    { Name = "entry"
      Kind = kind
      Size = size
      Modified = modified }

[<Fact>]
let ``a file size is rendered by the given format`` () =
    let grouped (size: int64) = size.ToString("N0", CultureInfo.InvariantCulture)

    Assert.Equal("12345", EntrySize.describe string (entry File (Some 12345L)))
    Assert.Equal("12,345", EntrySize.describe grouped (entry File (Some 12345L)))

[<Fact>]
let ``a directory shows the DIR marker whatever the format`` () =
    Assert.Equal(EntrySize.DirectoryMark, EntrySize.describe string (entry Directory None))

[<Fact>]
let ``a file of unknown size shows nothing`` () =
    Assert.Equal("", EntrySize.describe string (entry File None))
