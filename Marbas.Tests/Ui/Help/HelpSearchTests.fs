module Marbas.Tests.Ui.Help.HelpSearchTests

open System.Drawing
open Xunit
open Marbas.Domain
open Marbas.Guide
open Marbas.Viewing
open Marbas.Ui.Viewer
open Marbas.Ui.Help
open Marbas.Tests

let private name raw =
    ChapterName.tryCreate raw |> Expect.some

let private items count =
    List.init count (fun index -> $"- item {index + 1}\n") |> String.concat ""

let private chapterOf raw text : Chapter =
    { Name = name raw
      Contents = ChapterMarkdown.parse text |> Expect.ok }

let private searched =
    chapterOf "05-search.md" ("# Search\n\n- needle one\n" + items 20 + "- needle two\n" + items 20)

let private linked =
    chapterOf
        "README.md"
        ("# Linked\n\n- needle here\n- [Search](05-search.md)\n"
         + items 20
         + "- needle last\n")

let private read chapterName =
    [ searched; linked ]
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

let private plain raw : WindowSearch.Query =
    { Wanted = WindowSearch.Literal(FindText.tryCreate raw |> Expect.some, Anywhere)
      Case = IgnoringCase
      Direction = Forwards }

let private back (query: WindowSearch.Query) = { query with Direction = Backwards }

let private fromScreen query state =
    HelpState.findFromScreen query state |> Expect.some

let private afterMatch query state =
    HelpState.findAfterMatch query state |> Expect.some

let private span line from until : FoundSpan =
    { Line = line
      From = from
      Until = until }

[<Fact>]
let ``a search finds the first match on screen`` () =
    let state = opened searched

    let found = HelpState.findFromScreen (plain "needle") state

    Assert.Equal(Some(span 3 2 8), found |> Option.bind _.Page.Found)

[<Fact>]
let ``a match on screen leaves the screen where it is`` () =
    let state = opened searched

    let found = fromScreen (plain "one") state

    Assert.Equal(0, found.Page.Top)

[<Fact>]
let ``a match below the screen is scrolled to the top`` () =
    let state = opened searched

    let found = fromScreen (plain "two") state

    Assert.Equal(24, found.Page.Top)

[<Fact>]
let ``a search from the screen passes over what lies above it`` () =
    let state =
        opened searched
        |> after [ HelpKeys.LineDown; HelpKeys.LineDown; HelpKeys.LineDown; HelpKeys.LineDown ]

    let found = fromScreen (plain "needle") state

    Assert.Equal(Some(span 24 2 8), found.Page.Found)

[<Fact>]
let ``a search again finds the next match`` () =
    let state = opened searched |> fromScreen (plain "needle")

    let found = afterMatch (plain "needle") state

    Assert.Equal(Some(span 24 2 8), found.Page.Found)

[<Fact>]
let ``a search again without a match searches from the screen`` () =
    let state = opened searched

    let found = afterMatch (plain "needle") state

    Assert.Equal(Some(span 3 2 8), found.Page.Found)

[<Fact>]
let ``a search past the last match of the chapter finds nothing`` () =
    let state = opened searched |> fromScreen (plain "two")

    let found = HelpState.findAfterMatch (plain "needle") state

    Assert.Equal(None, found)

[<Fact>]
let ``a text the chapter does not hold is not found`` () =
    let state = opened searched

    let found = HelpState.findFromScreen (plain "haystack") state

    Assert.Equal(None, found)

[<Fact>]
let ``a search back from the screen starts at its last line`` () =
    let state = opened searched |> after [ HelpKeys.ToEnd ]

    let found = fromScreen (back (plain "needle")) state

    Assert.Equal(Some(span 24 2 8), found.Page.Found)
    Assert.Equal(24, found.Page.Top)

[<Fact>]
let ``a search back again finds the match before`` () =
    let state =
        opened searched
        |> after [ HelpKeys.ToEnd ]
        |> fromScreen (back (plain "needle"))

    let found = afterMatch (back (plain "needle")) state

    Assert.Equal(Some(span 3 2 8), found.Page.Found)
    Assert.Equal(3, found.Page.Top)

[<Fact>]
let ``a match scrolled to drops the link it hides`` () =
    let state = opened linked |> after [ HelpKeys.NextLink ]

    let found = fromScreen (plain "last") state

    Assert.Equal(None, found.Page.Link)

[<Fact>]
let ``the match is shown on its row of the screen`` () =
    let state = opened searched |> fromScreen (plain "two")

    let shown = HelpState.foundOnScreen state

    Assert.Equal(Some(0, 9, 3), shown)

[<Fact>]
let ``a match scrolled off the screen is not shown`` () =
    let state =
        opened searched |> fromScreen (plain "one") |> after [ HelpKeys.PageDown ]

    let shown = HelpState.foundOnScreen state

    Assert.Equal(None, shown)

[<Fact>]
let ``a match scrolled off the screen is still where the search goes on from`` () =
    let state =
        opened searched |> fromScreen (plain "needle") |> after [ HelpKeys.ToEnd ]

    let found = afterMatch (plain "needle") state

    Assert.Equal(Some(span 24 2 8), found.Page.Found)

[<Fact>]
let ``the text searched for is kept`` () =
    let state = opened searched

    let asked = HelpState.searchedFor (plain "needle") state

    Assert.Equal(Some(plain "needle"), asked.LastQuery)

[<Fact>]
let ``the text searched for outlasts a link followed`` () =
    let state =
        opened linked
        |> HelpState.searchedFor (plain "needle")
        |> after [ HelpKeys.NextLink ]

    let moved = followed state

    Assert.Equal(Some(plain "needle"), moved.LastQuery)

[<Fact>]
let ``a link followed starts the next search in the new chapter`` () =
    let state =
        opened linked
        |> fromScreen (plain "needle")
        |> after [ HelpKeys.NextLink ]
        |> followed

    let found = afterMatch (plain "needle") state

    Assert.Equal(name "05-search.md", found.Page.Chapter.Name)
    Assert.Equal(Some(span 3 2 8), found.Page.Found)

[<Fact>]
let ``going back brings back the match of the page left`` () =
    let state =
        opened linked
        |> fromScreen (plain "needle")
        |> after [ HelpKeys.NextLink ]
        |> followed

    let moved = HelpState.run HelpKeys.Back state

    Assert.Equal(Some(span 3 2 8), moved.Page.Found)

[<Fact>]
let ``a resize drops the match`` () =
    let state = opened searched |> fromScreen (plain "needle")

    let resized = HelpState.fitted (Size(40, 10)) state

    Assert.Equal(None, resized.Page.Found)

[<Fact>]
let ``a resize keeps the text searched for`` () =
    let state = opened searched |> HelpState.searchedFor (plain "needle")

    let resized = HelpState.fitted (Size(40, 10)) state

    Assert.Equal(Some(plain "needle"), resized.LastQuery)

let private typed raw =
    EnteredText.tryCreate raw |> Expect.some

[<Fact>]
let ``the help opens with nothing typed`` () =
    let state = opened searched

    Assert.Empty state.Typed

[<Fact>]
let ``a text typed is offered first`` () =
    let state = opened searched |> HelpState.typedIn (typed "older")

    let typedAgain = HelpState.typedIn (typed "newer") state

    Assert.Equal<EnteredText list>([ typed "newer"; typed "older" ], typedAgain.Typed)

[<Fact>]
let ``a text typed again moves to the front once`` () =
    let state =
        opened searched
        |> HelpState.typedIn (typed "needle")
        |> HelpState.typedIn (typed "other")

    let typedAgain = HelpState.typedIn (typed "needle") state

    Assert.Equal<EnteredText list>([ typed "needle"; typed "other" ], typedAgain.Typed)

[<Fact>]
let ``what was typed outlasts a link followed`` () =
    let state =
        opened linked
        |> HelpState.typedIn (typed "needle")
        |> after [ HelpKeys.NextLink ]

    let moved = followed state

    Assert.Equal<EnteredText list>([ typed "needle" ], moved.Typed)
