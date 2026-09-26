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
[<InlineData("*.jpg", "photo.jpg")>]
[<InlineData("*.jpg", "PHOTO.JPG")>]
[<InlineData("*.jpg;*.png", "photo.png")>]
[<InlineData(" *.jpg ; *.png ", "photo.png")>]
[<InlineData("a?c.txt", "abc.txt")>]
let ``a mask matches the names it names`` raw name =
    let matched = NameMasks.matches (NameMasks.read raw) (EntryName.create name)

    Assert.True matched

[<Theory>]
[<InlineData("*.jpg", "photo.png")>]
[<InlineData("*.jpg;*.png", "photo.gif")>]
[<InlineData("a?c.txt", "ac.txt")>]
let ``a mask passes over the names it does not name`` raw name =
    let matched = NameMasks.matches (NameMasks.read raw) (EntryName.create name)

    Assert.False matched

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
[<InlineData("report", "annual report 2026.txt")>]
[<InlineData("report", "REPORT.md")>]
[<InlineData("notes;draft", "second draft.md")>]
let ``a mask with no wildcard matches a name containing it`` raw name =
    let matched = NameMasks.matches (NameMasks.read raw) (EntryName.create name)

    Assert.True matched

[<Theory>]
[<InlineData("report", "summary.txt")>]
[<InlineData("notes;draft", "final.md")>]
let ``a mask with no wildcard passes over a name without it`` raw name =
    let matched = NameMasks.matches (NameMasks.read raw) (EntryName.create name)

    Assert.False matched

[<Fact>]
let ``a query without text keeps the targets it was given`` () =
    let query = queryFor (FindWhat.create (NameMasks.read "*.fs") None FilesAndDirectories)

    let targets = FindQuery.targets query

    Assert.Equal<FindTargets>(FilesAndDirectories, targets)

[<Fact>]
let ``a query with text finds files alone`` () =
    let query = queryFor (FindWhat.create (NameMasks.read "*.fs") (Some(searchFor "let")) FilesAndDirectories)

    let targets = FindQuery.targets query

    Assert.Equal<FindTargets>(FilesOnly, targets)

[<Fact>]
let ``a query without text carries no text options`` () =
    let query = queryFor (FindWhat.create (NameMasks.read "*.fs") None FilesOnly)

    let text = FindQuery.text query

    Assert.Equal<TextSearch option>(None, text)

[<Fact>]
let ``a query with text carries the search it was given`` () =
    let search = searchFor "let"
    let query = queryFor (FindWhat.create (NameMasks.read "*.fs") (Some search) FilesOnly)

    let text = FindQuery.text query

    Assert.Equal<TextSearch option>(Some search, text)

[<Fact>]
let ``the masks survive a query with text`` () =
    let query = queryFor (FindWhat.create (NameMasks.read "*.fs") (Some(searchFor "let")) FilesOnly)

    let matched = NameMasks.matches (FindQuery.masks query) (EntryName.create "AppState.fs")

    Assert.True matched

[<Fact>]
let ``an empty text is no text at all`` () =
    let created = FindText.tryCreate ""

    Assert.Equal<FindText option>(None, created)
