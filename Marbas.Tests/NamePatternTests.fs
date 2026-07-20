module Marbas.Tests.NamePatternTests

open Xunit
open Marbas.FileSystem

[<Theory>]
[<InlineData("*.fs", "AppState.fs", true)>]
[<InlineData("*.fs", "AppState.fsx", false)>]
[<InlineData("*.fs", "AppState.FS", true)>]
[<InlineData("*", "anything.txt", true)>]
[<InlineData("*.*", "docs", false)>]
[<InlineData("a?c.txt", "abc.txt", true)>]
[<InlineData("a?c.txt", "ac.txt", false)>]
[<InlineData("readme.txt", "readme.txt", true)>]
let ``wildcard patterns match names case-insensitively`` pattern name expected =
    Assert.Equal(expected, NamePattern.matches pattern name)

[<Fact>]
let ``an empty pattern matches nothing`` () =
    Assert.False(NamePattern.matches "" "readme.txt")
