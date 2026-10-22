module Marbas.Tests.Ui.Help.HelpStateTests

open System.Drawing
open Xunit
open Marbas.Guide
open Marbas.Ui.Help
open Marbas.Tests

let private name raw =
    ChapterName.tryCreate raw |> Expect.some

let private items count =
    List.init count (fun index -> $"- item {index + 1}\n") |> String.concat ""

let private chapterOf raw text : Chapter =
    { Name = name raw
      Contents = ChapterMarkdown.parse text |> Expect.ok }

let private contents =
    chapterOf
        "README.md"
        ("# Guide\n\n"
         + "- [Panels](02-panels.md)\n- [Sorting](02-panels.md#sorting)\n- [Notes](#notes)\n"
         + items 30
         + "\n## Notes\n\nSee [Panels](02-panels.md) again.\n\n"
         + items 20)

let private panels =
    chapterOf "02-panels.md" ("# Panels\n\n" + items 20 + "\n## Sorting\n\n" + items 20)

let private lost = chapterOf "03-lost.md" "# Lost\n\nSee [gone](99-gone.md)."

let private long =
    let paragraph = List.replicate 100 "word" |> String.concat " "
    chapterOf "04-long.md" ("# Long\n\n" + paragraph + "\n\n## End\n\n" + items 20)

let private read chapterName =
    [ contents; panels; lost ]
    |> List.tryFind (fun chapter -> chapter.Name = chapterName)
    |> Option.map Ok
    |> Option.defaultValue (Error(Absent chapterName))

let private screen = Size(80, 10)

let private opened chapter =
    HelpState.start chapter |> HelpState.fitted screen

let private after commands state =
    commands |> List.fold (fun state command -> HelpState.run command state) state

let private followed state =
    HelpState.follow read state |> Expect.ok

let private texts state =
    HelpState.shown state
    |> List.map (fun line -> line.Pieces |> List.map _.Text |> String.concat "")

[<Fact>]
let ``the help opens at the top of its chapter`` () =
    let state = HelpState.start contents

    let fitted = HelpState.fitted screen state

    Assert.Equal<string list>(
        [ "Guide"
          "═════"
          ""
          "• Panels"
          "• Sorting"
          "• Notes"
          "• item 1"
          "• item 2"
          "• item 3"
          "• item 4" ],
        texts fitted
    )

[<Fact>]
let ``the down arrow moves on by a line`` () =
    let state = opened contents

    let moved = HelpState.run HelpKeys.LineDown state

    Assert.Equal(1, moved.Page.Top)

[<Fact>]
let ``the up arrow stops at the top`` () =
    let state = opened contents

    let moved = HelpState.run HelpKeys.LineUp state

    Assert.Equal(0, moved.Page.Top)

[<Fact>]
let ``a page moves on by a screenful`` () =
    let state = opened contents

    let moved = HelpState.run HelpKeys.PageDown state

    Assert.Equal(10, moved.Page.Top)

[<Fact>]
let ``a page back returns where it came from`` () =
    let state = opened contents |> after [ HelpKeys.PageDown ]

    let moved = HelpState.run HelpKeys.PageUp state

    Assert.Equal(0, moved.Page.Top)

[<Fact>]
let ``the end key shows the last line at the bottom`` () =
    let state = opened contents

    let moved = HelpState.run HelpKeys.ToEnd state

    Assert.Equal(52, moved.Page.Top)
    Assert.Equal("• item 20", List.last (texts moved))

[<Fact>]
let ``the down arrow stops at the end`` () =
    let state = opened contents |> after [ HelpKeys.ToEnd ]

    let moved = HelpState.run HelpKeys.LineDown state

    Assert.Equal(52, moved.Page.Top)

[<Fact>]
let ``the home key returns to the top`` () =
    let state = opened contents |> after [ HelpKeys.ToEnd ]

    let moved = HelpState.run HelpKeys.ToStart state

    Assert.Equal(0, moved.Page.Top)

[<Fact>]
let ``Tab chooses the first link on screen`` () =
    let state = opened contents

    let moved = HelpState.run HelpKeys.NextLink state

    Assert.Equal(Some 0, moved.Page.Link)

[<Fact>]
let ``Tab again chooses the next link`` () =
    let state = opened contents |> after [ HelpKeys.NextLink ]

    let moved = HelpState.run HelpKeys.NextLink state

    Assert.Equal(Some 1, moved.Page.Link)

[<Fact>]
let ``Shift+Tab chooses the previous link`` () =
    let state = opened contents |> after [ HelpKeys.NextLink; HelpKeys.NextLink ]

    let moved = HelpState.run HelpKeys.PreviousLink state

    Assert.Equal(Some 0, moved.Page.Link)

[<Fact>]
let ``Shift+Tab with no link chosen chooses the last link on screen`` () =
    let state = opened contents

    let moved = HelpState.run HelpKeys.PreviousLink state

    Assert.Equal(Some 2, moved.Page.Link)

[<Fact>]
let ``Tab past the last link on screen scrolls the next one into view`` () =
    let state =
        opened contents
        |> after [ HelpKeys.NextLink; HelpKeys.NextLink; HelpKeys.NextLink ]

    let moved = HelpState.run HelpKeys.NextLink state

    Assert.Equal(Some 3, moved.Page.Link)
    Assert.Equal(31, moved.Page.Top)

[<Fact>]
let ``Tab with no link on screen chooses the first one below it`` () =
    let state = opened contents |> after [ HelpKeys.PageDown ]

    let moved = HelpState.run HelpKeys.NextLink state

    Assert.Equal(Some 3, moved.Page.Link)
    Assert.Equal(31, moved.Page.Top)

[<Fact>]
let ``Tab on the last link of the chapter keeps it`` () =
    let state = opened contents |> after [ HelpKeys.ToEnd; HelpKeys.PreviousLink ]

    let moved = HelpState.run HelpKeys.NextLink state

    Assert.Equal(Some 3, moved.Page.Link)

[<Fact>]
let ``a link scrolled off the screen is no longer chosen`` () =
    let state = opened contents |> after [ HelpKeys.NextLink ]

    let moved = HelpState.run HelpKeys.PageDown state

    Assert.Equal(None, moved.Page.Link)

[<Fact>]
let ``Enter with no link chosen stays`` () =
    let state = opened contents

    let followed = HelpState.follow read state

    Assert.Equal(Ok state, followed)

[<Fact>]
let ``following a link to a chapter opens it at its top`` () =
    let state = opened contents |> after [ HelpKeys.NextLink ]

    let moved = followed state

    Assert.Equal(name "02-panels.md", moved.Page.Chapter.Name)
    Assert.Equal(0, moved.Page.Top)
    Assert.Equal(None, moved.Page.Link)

[<Fact>]
let ``following a link to a heading of another chapter opens it at the heading`` () =
    let state = opened contents |> after [ HelpKeys.NextLink; HelpKeys.NextLink ]

    let moved = followed state

    Assert.Equal(name "02-panels.md", moved.Page.Chapter.Name)
    Assert.Equal("Sorting", List.head (texts moved))

[<Fact>]
let ``following a link to a heading of its own chapter scrolls to it`` () =
    let state =
        opened contents
        |> after [ HelpKeys.NextLink; HelpKeys.NextLink; HelpKeys.NextLink ]

    let moved = followed state

    Assert.Equal(name "README.md", moved.Page.Chapter.Name)
    Assert.Equal("Notes", List.head (texts moved))

[<Fact>]
let ``a chapter that cannot be read is the fault of the link`` () =
    let state = opened lost |> after [ HelpKeys.NextLink ]

    let moved = HelpState.follow read state

    Assert.Equal(Error(Absent(name "99-gone.md")), moved)

[<Fact>]
let ``going back returns to the chapter, the line and the link that was followed`` () =
    let state =
        opened contents
        |> after [ HelpKeys.LineDown; HelpKeys.NextLink; HelpKeys.NextLink ]
        |> followed

    let moved = HelpState.run HelpKeys.Back state

    Assert.Equal(name "README.md", moved.Page.Chapter.Name)
    Assert.Equal(1, moved.Page.Top)
    Assert.Equal(Some 1, moved.Page.Link)

[<Fact>]
let ``going back from the first page stays`` () =
    let state = opened contents |> after [ HelpKeys.LineDown ]

    let moved = HelpState.run HelpKeys.Back state

    Assert.Equal(state, moved)

[<Fact>]
let ``going back twice retraces two links`` () =
    let state =
        opened contents
        |> after [ HelpKeys.NextLink; HelpKeys.NextLink; HelpKeys.NextLink ]
        |> followed
        |> after [ HelpKeys.ToStart; HelpKeys.NextLink ]
        |> followed

    let moved = state |> after [ HelpKeys.Back; HelpKeys.Back ]

    Assert.Equal(0, moved.Page.Top)
    Assert.Equal(Some 2, moved.Page.Link)
    Assert.Empty moved.Back

[<Fact>]
let ``a resize keeps the line on top as far into its block as before`` () =
    let state =
        HelpState.start long
        |> HelpState.fitted (Size(80, 5))
        |> after (List.replicate 5 HelpKeys.LineDown)

    let resized = HelpState.fitted (Size(40, 5)) state

    Assert.Equal(7, resized.Page.Top)
    Assert.Equal(1, (List.head (HelpState.shown resized)).Block)

[<Fact>]
let ``a taller screen at the end keeps the last line at the bottom`` () =
    let state = opened contents |> after [ HelpKeys.ToEnd ]

    let resized = HelpState.fitted (Size(80, 20)) state

    Assert.Equal(42, resized.Page.Top)

[<Fact>]
let ``going back after a resize lays the page out at the new width`` () =
    let state =
        opened contents
        |> after [ HelpKeys.NextLink ]
        |> followed
        |> HelpState.fitted (Size(40, 10))

    let moved = HelpState.run HelpKeys.Back state

    Assert.Equal(40, moved.Page.Width)

[<Fact>]
let ``the title is the chapter's own`` () =
    let state = opened contents

    let title = HelpState.title state

    Assert.Equal("Guide", title)

[<Fact>]
let ``a chapter without a title is named by its file`` () =
    let state = opened (chapterOf "05-plain.md" "Just text.")

    let title = HelpState.title state

    Assert.Equal("05-plain.md", title)

let private place raw heading : GuidePlace =
    { Chapter = name raw
      Heading = heading |> Option.map Anchor.create }

let private openedAt guidePlace =
    HelpState.opening read guidePlace |> Expect.ok |> HelpState.fitted screen

[<Fact>]
let ``the help opened at the contents shows them from the top`` () =
    let state = openedAt GuidePlace.contents

    Assert.Equal(name "README.md", state.Page.Chapter.Name)
    Assert.Equal(0, state.Page.Top)
    Assert.Empty state.Back

[<Fact>]
let ``the help opened at a heading shows it on top`` () =
    let state = openedAt (place "02-panels.md" (Some "sorting"))

    Assert.Equal(name "02-panels.md", state.Page.Chapter.Name)
    Assert.Equal("Sorting", List.head (texts state))

[<Fact>]
let ``the help opened at a chapter shows it from the top`` () =
    let state = openedAt (place "02-panels.md" None)

    Assert.Equal(0, state.Page.Top)

[<Fact>]
let ``going back from the chapter the help opened at shows the contents`` () =
    let state = openedAt (place "02-panels.md" (Some "sorting"))

    let moved = HelpState.run HelpKeys.Back state

    Assert.Equal(name "README.md", moved.Page.Chapter.Name)
    Assert.Equal(0, moved.Page.Top)

[<Fact>]
let ``the help opened at a chapter that cannot be read is the fault of that chapter`` () =
    let opened = HelpState.opening read (place "99-gone.md" None)

    Assert.Equal(Error(Absent(name "99-gone.md")), opened |> Result.map ignore)
