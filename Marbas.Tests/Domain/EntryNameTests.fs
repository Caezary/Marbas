module Marbas.Tests.Domain.EntryNameTests

open Xunit
open Marbas.Domain

let private en = EntryName.create

[<Fact>]
let ``the extension is what follows the last dot`` () =
    let extension = EntryName.extension (en "archive.tar.gz")

    Assert.Equal("gz", extension)

[<Fact>]
let ``a name without a dot has no extension`` () =
    let extension = EntryName.extension (en "readme")

    Assert.Equal("", extension)

[<Fact>]
let ``a name that is only a dotted suffix has no extension`` () =
    let extension = EntryName.extension (en ".gitignore")

    Assert.Equal("", extension)

[<Fact>]
let ``a name ending in a dot has no extension`` () =
    let extension = EntryName.extension (en "odd.")

    Assert.Equal("", extension)

[<Theory>]
[<InlineData("archive.tar.gz", "archive.tar")>]
[<InlineData("notes.TXT", "notes")>]
[<InlineData("readme", "readme")>]
[<InlineData(".gitignore", ".gitignore")>]
[<InlineData("odd.", "odd.")>]
let ``the stem is the name without its extension`` (name: string) (expected: string) =
    let stem = EntryName.stem (en name)

    Assert.Equal(expected, stem)
