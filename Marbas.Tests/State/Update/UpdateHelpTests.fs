module Marbas.Tests.State.Update.UpdateHelpTests

open Xunit
open Marbas.Domain
open Marbas.Guide
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.Update.UpdateFixture

let private sorting: GuidePlace =
    { Chapter = ChapterName.tryCreate "02-panels.md" |> Expect.some
      Heading = Some(Anchor.create "sorting") }

let private refreshing: GuidePlace =
    { Chapter = ChapterName.tryCreate "02-panels.md" |> Expect.some
      Heading = Some(Anchor.create "reading-the-panel-again") }

[<Fact>]
let ``reading the panel again has its own section`` () =
    let place = helpPlaceOf RefreshRequested

    Assert.Equal(Some refreshing, place)

[<Fact>]
let ``F1 itself is no command the help opens at`` () =
    let place = helpPlaceOf HelpRequested

    Assert.Equal(None, place)

[<Fact>]
let ``moving the cursor is no command the help opens at`` () =
    let place = helpPlaceOf (CursorMoved(cursor Left 1))

    Assert.Equal(None, place)

[<Fact>]
let ``a quick search is no command the help opens at`` () =
    let place = helpPlaceOf (SearchTyped 'n')

    Assert.Equal(None, place)

[<Fact>]
let ``before the first command the help opens at the contents`` () =
    let fs = mockFs ()

    let state = loaded fs

    Assert.Equal(GuidePlace.contents, state.HelpPlace)

[<Fact>]
let ``F1 asks for the help at the place of the last command`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = update fs HelpRequested state

    Assert.Equal<Effect list>([ ShowHelp GuidePlace.contents ], result.Effects)

[<Fact>]
let ``a command keeps its place in the guide`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = update fs (SortRequested SortKey.ByName) state

    Assert.Equal(sorting, result.State.HelpPlace)

[<Fact>]
let ``a later command takes the place of the earlier one`` () =
    let fs = mockFs ()
    let state = (update fs (SortRequested SortKey.ByName) (loaded fs)).State

    let result = update fs RefreshRequested state

    Assert.Equal(refreshing, result.State.HelpPlace)

[<Fact>]
let ``moving the cursor keeps the place of the last command`` () =
    let fs = mockFs ()
    let state = (update fs (SortRequested SortKey.ByName) (loaded fs)).State

    let result = update fs (CursorMoved(cursor Left 1)) state

    Assert.Equal(sorting, result.State.HelpPlace)

[<Fact>]
let ``a quick search keeps the place of the last command`` () =
    let fs = mockFs ()
    let state = (update fs (SortRequested SortKey.ByName) (loaded fs)).State

    let result = update fs (SearchTyped 'n') state

    Assert.Equal(sorting, result.State.HelpPlace)

[<Fact>]
let ``F1 after a command asks for the help at that command's place`` () =
    let fs = mockFs ()
    let state = (update fs (SortRequested SortKey.ByName) (loaded fs)).State

    let result = update fs HelpRequested state

    Assert.Equal<Effect list>([ ShowHelp sorting ], result.Effects)
