module Marbas.Tests.Guide.UserGuideTests

open Xunit
open Marbas.Guide
open Marbas.Tests

[<Fact>]
let ``the guide is the contents and sixteen chapters`` () =
    let embedded = EmbeddedGuide.names () |> List.map ChapterName.value

    Assert.Equal<string list>(UserGuide.names, embedded)

[<Theory>]
[<InlineData("README.md")>]
[<InlineData("01-getting-started.md")>]
[<InlineData("02-panels.md")>]
[<InlineData("03-marking.md")>]
[<InlineData("04-tabs.md")>]
[<InlineData("05-file-operations.md")>]
[<InlineData("06-directories-and-favourites.md")>]
[<InlineData("07-archives.md")>]
[<InlineData("08-clipboard.md")>]
[<InlineData("09-finding-files.md")>]
[<InlineData("10-running-files.md")>]
[<InlineData("11-viewer.md")>]
[<InlineData("12-settings.md")>]
[<InlineData("13-history.md")>]
[<InlineData("14-command-line-and-environment.md")>]
[<InlineData("15-files-marbas-keeps.md")>]
[<InlineData("16-platforms.md")>]
let ``a chapter is written in the subset`` (name: string) =
    let chapter = ChapterName.tryCreate name |> Expect.some

    let read = EmbeddedGuide.read chapter

    Assert.True(Result.isOk read, $"%A{read}")

[<Fact>]
let ``every link of the guide leads to a chapter and a heading it holds`` () =
    let guide = UserGuide.chapters ()

    let broken = ChapterLinks.broken guide

    Assert.Empty broken
