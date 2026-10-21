module Marbas.Tests.Guide.UserGuideTests

open System
open System.IO
open Xunit
open Marbas.Guide
open Marbas.Tests

let private prefix = "user-guide/"

let private chapters =
    [ "01-getting-started.md"
      "02-panels.md"
      "03-marking.md"
      "04-tabs.md"
      "05-file-operations.md"
      "06-directories-and-favourites.md"
      "07-archives.md"
      "08-clipboard.md"
      "09-finding-files.md"
      "10-running-files.md"
      "11-viewer.md"
      "12-settings.md"
      "13-history.md"
      "14-command-line-and-environment.md"
      "15-files-marbas-keeps.md"
      "16-platforms.md"
      "README.md" ]

let private embeddedChapters () =
    typeof<Chapter>.Assembly.GetManifestResourceNames()
    |> Array.filter _.StartsWith(prefix, StringComparison.Ordinal)
    |> Array.map _.Substring(prefix.Length)
    |> Array.sort
    |> List.ofArray

let private textOf name =
    use stream =
        typeof<Chapter>.Assembly.GetManifestResourceStream(prefix + name)
        |> Expect.notNull

    use reader = new StreamReader(stream)
    reader.ReadToEnd()

let private parsed name =
    ChapterMarkdown.parse (textOf name) |> Expect.ok

let private chapterOf name : Chapter =
    { Name = ChapterName.tryCreate name |> Expect.some
      Contents = parsed name }

let private wholeGuide () = chapters |> List.map chapterOf

[<Fact>]
let ``the guide is the contents and sixteen chapters`` () =
    let embedded = embeddedChapters ()

    Assert.Equal<string list>(chapters, embedded)

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
    let text = textOf name

    let blocks = ChapterMarkdown.parse text |> Expect.ok

    Assert.NotEmpty blocks

[<Fact>]
let ``every link of the guide leads to a chapter and a heading it holds`` () =
    let guide = wholeGuide ()

    let broken = ChapterLinks.broken guide

    Assert.Empty broken
