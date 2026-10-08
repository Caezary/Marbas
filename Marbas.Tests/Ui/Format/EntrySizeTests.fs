module Marbas.Tests.Ui.Format.EntrySizeTests

open System
open System.Globalization
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.Ui.Format

let private modified = DateTimeOffset(DateTime(2026, 7, 1, 12, 30, 0))

let private entry kind size =
    { Name = EntryName.create "entry"
      Kind = kind
      Size = size
      Measured = None
      Modified = modified
      Visibility = Plain
      Executability = NotExecutable
      LinkTarget = None
      Verification = Verified }

let private measured size =
    { entry Directory None with
        Measured = Some size }

[<Fact>]
let ``a file size is rendered by the given format`` () =
    let grouped (size: int64) =
        size.ToString("N0", CultureInfo.InvariantCulture)

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

[<Fact>]
let ``a measured directory shows what it holds instead of the marker`` () =
    let text = EntrySize.describe string (measured (Exact 12345L))

    Assert.Equal("12345", text)

[<Fact>]
let ``a directory measured only in part shows a lower bound`` () =
    let text = EntrySize.describe string (measured (AtLeast 12345L))

    Assert.Equal(EntrySize.AtLeastMark + "12345", text)
