module Marbas.Tests.Domain.LikenessTests

open Xunit
open Marbas.Domain

let private en = EntryName.create

[<Theory>]
[<InlineData("notes.txt", "readme.txt")>]
[<InlineData("notes.txt", "README.TXT")>]
[<InlineData("archive.tar.gz", "data.gz")>]
[<InlineData("readme", "Makefile")>]
[<InlineData("readme", ".gitignore")>]
let ``names with the same extension are alike by extension`` (one: string) (other: string) =
    let alike = Likeness.alike SameExtension (en one) (en other)

    Assert.True alike

[<Theory>]
[<InlineData("notes.txt", "notes.md")>]
[<InlineData("readme", "readme.txt")>]
[<InlineData("archive.tar.gz", "archive.tar")>]
let ``names with another extension are not alike by extension`` (one: string) (other: string) =
    let alike = Likeness.alike SameExtension (en one) (en other)

    Assert.False alike

[<Theory>]
[<InlineData("notes.txt", "notes.md")>]
[<InlineData("notes.txt", "NOTES.TXT")>]
[<InlineData("archive.tar.gz", "archive.tar.bz2")>]
[<InlineData("readme", "readme.txt")>]
let ``names with the same stem are alike by stem`` (one: string) (other: string) =
    let alike = Likeness.alike SameStem (en one) (en other)

    Assert.True alike

[<Theory>]
[<InlineData("archive.tar.gz", "archive.gz")>]
[<InlineData("notes.txt", "note.txt")>]
[<InlineData("readme", "readme.old.txt")>]
let ``names with another stem are not alike by stem`` (one: string) (other: string) =
    let alike = Likeness.alike SameStem (en one) (en other)

    Assert.False alike
