module Marbas.Tests.Domain.FindQueryTests

open Xunit
open Marbas.Domain
open Marbas.Tests

let private searchFor raw =
    { Text = FindText.tryCreate raw |> Expect.some
      Case = IgnoringCase
      Words = Anywhere
      Sense = Containing
      Binary = Skipped }

let private queryFor what =
    { What = what
      Scope = FromDirectory
      Hidden = Concealed
      Links = FollowLinks
      Archives = SkipArchives }

[<Theory>]
[<InlineData("*.jpg", "photo.jpg", true)>]
[<InlineData("*.jpg", "photo.png", false)>]
[<InlineData("*.jpg", "PHOTO.JPG", true)>]
[<InlineData("*.jpg;*.png", "photo.png", true)>]
[<InlineData("*.jpg;*.png", "photo.gif", false)>]
[<InlineData(" *.jpg ; *.png ", "photo.png", true)>]
[<InlineData("a?c.txt", "abc.txt", true)>]
[<InlineData("a?c.txt", "ac.txt", false)>]
let ``a mask matches the names it names`` raw name expected =
    let matched = NameMasks.matches (NameMasks.read raw) (EntryName.create name)

    Assert.Equal(expected, matched)

[<Theory>]
[<InlineData("*.*", "photo.jpg")>]
[<InlineData("*.*", "Makefile")>]
[<InlineData("*", "Makefile")>]
[<InlineData("", "Makefile")>]
[<InlineData("   ", "Makefile")>]
[<InlineData(";;", "Makefile")>]
[<InlineData("*.jpg;*.*", "Makefile")>]
let ``everything means everything, dot or no dot`` raw name =
    let matched = NameMasks.matches (NameMasks.read raw) (EntryName.create name)

    Assert.True matched

[<Theory>]
[<InlineData("report", "annual report 2026.txt", true)>]
[<InlineData("report", "REPORT.md", true)>]
[<InlineData("report", "summary.txt", false)>]
[<InlineData("notes;draft", "second draft.md", true)>]
let ``a mask with no wildcard is a fragment`` raw name expected =
    let matched = NameMasks.matches (NameMasks.read raw) (EntryName.create name)

    Assert.Equal(expected, matched)

[<Fact>]
let ``a query without text keeps the targets it was given`` () =
    let query = queryFor (FindWhat.create (NameMasks.read "*.fs") None FilesAndDirectories)

    Assert.Equal<FindTargets>(FilesAndDirectories, FindQuery.targets query)

[<Fact>]
let ``a query with text finds files alone`` () =
    let query = queryFor (FindWhat.create (NameMasks.read "*.fs") (Some(searchFor "let")) FilesAndDirectories)

    Assert.Equal<FindTargets>(FilesOnly, FindQuery.targets query)

[<Fact>]
let ``a query without text carries no text options`` () =
    let query = queryFor (FindWhat.create (NameMasks.read "*.fs") None FilesOnly)

    Assert.Equal<TextSearch option>(None, FindQuery.text query)

[<Fact>]
let ``a query with text carries the search it was given`` () =
    let search = searchFor "let"
    let query = queryFor (FindWhat.create (NameMasks.read "*.fs") (Some search) FilesOnly)

    Assert.Equal<TextSearch option>(Some search, FindQuery.text query)

[<Fact>]
let ``the masks survive a query with text`` () =
    let query = queryFor (FindWhat.create (NameMasks.read "*.fs") (Some(searchFor "let")) FilesOnly)

    Assert.True(NameMasks.matches (FindQuery.masks query) (EntryName.create "AppState.fs"))

[<Fact>]
let ``an empty text is no text at all`` () =
    let created = FindText.tryCreate ""

    Assert.Equal<FindText option>(None, created)
