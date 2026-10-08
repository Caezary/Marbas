module Marbas.Tests.Domain.NamePatternTests

open Xunit
open Marbas.Domain
open Marbas.Tests

let private pattern raw =
    NamePattern.tryCreate raw |> Expect.some

[<Theory>]
[<InlineData("*.fs", "AppState.fs")>]
[<InlineData("*.fs", "AppState.FS")>]
[<InlineData("*", "anything.txt")>]
[<InlineData("a?c.txt", "abc.txt")>]
[<InlineData("readme.txt", "readme.txt")>]
[<InlineData("*.tar.gz", "backup.tar.gz")>]
[<InlineData("a*b*c", "aXXbYYc")>]
[<InlineData("a*b*c", "abc")>]
[<InlineData("*a*", "banana")>]
[<InlineData("??.txt", "ab.txt")>]
[<InlineData(@"\*.txt", "*.txt")>]
[<InlineData(@"what\?", "what?")>]
let ``a wildcard pattern matches the names it describes, whatever the case`` raw name =
    let matched = NamePattern.matches (pattern raw) (EntryName.create name)

    Assert.True matched

[<Theory>]
[<InlineData("*.fs", "AppState.fsx")>]
[<InlineData("*.*", "docs")>]
[<InlineData("a?c.txt", "ac.txt")>]
[<InlineData("a*b*c", "aXXbYY")>]
[<InlineData("??.txt", "abc.txt")>]
[<InlineData("*.tar.gz", "backup.tar")>]
[<InlineData(@"\*.txt", "notes.txt")>]
[<InlineData(@"what\?", "whatn")>]
let ``a wildcard pattern passes over the names it does not describe`` raw name =
    let matched = NamePattern.matches (pattern raw) (EntryName.create name)

    Assert.False matched

[<Fact>]
let ``an empty answer is no pattern at all`` () =
    let created = NamePattern.tryCreate ""

    Assert.Equal<NamePattern option>(None, created)

[<Fact>]
let ``a pattern of blanks is a pattern`` () =
    let created = NamePattern.tryCreate " "

    Assert.True created.IsSome
