module Marbas.Tests.Guide.ChapterLinksTests

open Xunit
open Marbas.Guide
open Marbas.Tests

let private chapter name =
    ChapterName.tryCreate name |> Expect.some

let private chapterOf (name, text) : Chapter =
    { Name = chapter name
      Contents = ChapterMarkdown.parse text |> Expect.ok }

let private brokenIn chapters =
    chapters |> List.map chapterOf |> ChapterLinks.broken

let private leadingTo name target : ChapterLinks.BrokenLink =
    { Name = chapter name; Target = target }

[<Fact>]
let ``a link to a heading of its own chapter leads somewhere`` () =
    let chapters =
        [ "02-panels.md", "# Panels\n\n## Sorting\n\nSee [sorting](#sorting)." ]

    let broken = brokenIn chapters

    Assert.Empty broken

[<Fact>]
let ``a link to a heading its own chapter lacks leads nowhere`` () =
    let chapters = [ "02-panels.md", "# Panels\n\nSee [sorting](#sorting)." ]

    let broken = brokenIn chapters

    Assert.Equal<ChapterLinks.BrokenLink list>(
        [ leadingTo "02-panels.md" (SameChapter(Anchor.create "sorting")) ],
        broken
    )

[<Fact>]
let ``a link to a chapter of the guide leads somewhere`` () =
    let chapters =
        [ "README.md", "# Contents\n\n- [Marking](03-marking.md)"
          "03-marking.md", "# Marking" ]

    let broken = brokenIn chapters

    Assert.Empty broken

[<Fact>]
let ``a link to a chapter the guide lacks leads nowhere`` () =
    let chapters = [ "README.md", "# Contents\n\n- [Marking](03-marking.md)" ]

    let broken = brokenIn chapters

    Assert.Equal<ChapterLinks.BrokenLink list>(
        [ leadingTo "README.md" (OtherChapter(chapter "03-marking.md", None)) ],
        broken
    )

[<Fact>]
let ``a link to a heading of another chapter leads somewhere`` () =
    let chapters =
        [ "README.md", "# Contents\n\n- [Patterns](03-marking.md#patterns)"
          "03-marking.md", "# Marking\n\n## Patterns" ]

    let broken = brokenIn chapters

    Assert.Empty broken

[<Fact>]
let ``a link to a heading another chapter lacks leads nowhere`` () =
    let chapters =
        [ "README.md", "# Contents\n\n- [Patterns](03-marking.md#patterns)"
          "03-marking.md", "# Marking" ]

    let broken = brokenIn chapters

    Assert.Equal<ChapterLinks.BrokenLink list>(
        [ leadingTo "README.md" (OtherChapter(chapter "03-marking.md", Some(Anchor.create "patterns"))) ],
        broken
    )

[<Fact>]
let ``a heading of one chapter is no heading of another`` () =
    let chapters =
        [ "README.md", "# Contents\n\n## Patterns\n\n- [Patterns](03-marking.md#patterns)"
          "03-marking.md", "# Marking" ]

    let broken = brokenIn chapters

    Assert.Equal<ChapterLinks.BrokenLink list>(
        [ leadingTo "README.md" (OtherChapter(chapter "03-marking.md", Some(Anchor.create "patterns"))) ],
        broken
    )

[<Theory>]
[<InlineData("## See [the rest](#nowhere)")>]
[<InlineData("**See [the rest](#nowhere)**")>]
[<InlineData("See *[the rest](#nowhere)*")>]
[<InlineData("- Marks\n  - See [the rest](#nowhere)")>]
[<InlineData("| Key | Action |\n|-----|--------|\n| `F5` | See [the rest](#nowhere) |")>]
[<InlineData("| See [the rest](#nowhere) | Action |\n|-----|--------|\n| `F5` | Copy |")>]
[<InlineData("> [!NOTE]\n> See [the rest](#nowhere)")>]
[<InlineData("> [!WARNING]\n> - See [the rest](#nowhere)")>]
let ``a link is followed wherever a phrase holds it`` (text: string) =
    let chapters = [ "02-panels.md", text ]

    let broken = brokenIn chapters

    Assert.Equal<ChapterLinks.BrokenLink list>(
        [ leadingTo "02-panels.md" (SameChapter(Anchor.create "nowhere")) ],
        broken
    )

let private placeIn name heading : GuidePlace =
    { Chapter = chapter name
      Heading = heading |> Option.map Anchor.create }

[<Fact>]
let ``a place at a heading the chapter holds is reached`` () =
    let chapters =
        [ "02-panels.md", "# Panels\n\n## Sorting\n\nText." ] |> List.map chapterOf

    let unreached =
        ChapterLinks.unreached chapters [ placeIn "02-panels.md" (Some "sorting") ]

    Assert.Empty unreached

[<Fact>]
let ``a place at the top of a chapter of the guide is reached`` () =
    let chapters = [ "02-panels.md", "# Panels" ] |> List.map chapterOf

    let unreached = ChapterLinks.unreached chapters [ placeIn "02-panels.md" None ]

    Assert.Empty unreached

[<Fact>]
let ``a place at a heading the chapter lacks is not reached`` () =
    let chapters = [ "02-panels.md", "# Panels" ] |> List.map chapterOf
    let place = placeIn "02-panels.md" (Some "sorting")

    let unreached = ChapterLinks.unreached chapters [ place ]

    Assert.Equal<GuidePlace list>([ place ], unreached)

[<Fact>]
let ``a place in a chapter the guide lacks is not reached`` () =
    let chapters = [ "02-panels.md", "# Panels" ] |> List.map chapterOf
    let place = placeIn "03-marking.md" None

    let unreached = ChapterLinks.unreached chapters [ place ]

    Assert.Equal<GuidePlace list>([ place ], unreached)
