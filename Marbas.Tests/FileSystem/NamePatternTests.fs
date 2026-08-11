module Marbas.Tests.FileSystem.NamePatternTests

open Xunit
open Marbas.FileSystem
open Marbas.Tests

let private pattern raw = NamePattern.create raw |> Expect.some

[<Theory>]
[<InlineData("*.fs", "AppState.fs", true)>]
[<InlineData("*.fs", "AppState.fsx", false)>]
[<InlineData("*.fs", "AppState.FS", true)>]
[<InlineData("*", "anything.txt", true)>]
[<InlineData("*.*", "docs", false)>]
[<InlineData("a?c.txt", "abc.txt", true)>]
[<InlineData("a?c.txt", "ac.txt", false)>]
[<InlineData("readme.txt", "readme.txt", true)>]
let ``wildcard patterns match names case-insensitively`` raw name expected =
    let matched = NamePattern.matches (pattern raw) (EntryName.create name)

    Assert.Equal(expected, matched)

[<Fact>]
let ``an empty answer is no pattern at all`` () =
    let created = NamePattern.create ""

    Assert.Equal<NamePattern option>(None, created)

[<Fact>]
let ``a pattern of blanks is a pattern`` () =
    let created = NamePattern.create " "

    Assert.True created.IsSome
