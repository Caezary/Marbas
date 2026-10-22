module Marbas.Tests.State.Update.UpdateTabTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Collections
open Marbas.Domain
open Marbas.Messages
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.Update.UpdateFixture

let private at = Root.at

[<Fact>]
let ``opening a tab adds one to the session and writes it`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let result = update fs TabOpened state

    Assert.Equal(2, Zipper.count result.State.Tabs)
    Assert.Equal(1, Zipper.position result.State.Tabs)
    Assert.Equal<Effect list>([ SaveSettings(settings fs result.State) ], result.Effects)

[<Fact>]
let ``the tab that was opened stands where the one it came from stands`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let result = update fs TabOpened state

    Assert.Equal(state.Left.Path, result.State.Left.Path)
    Assert.Equal(state.Right.Path, result.State.Right.Path)

[<Fact>]
let ``closing a tab takes it out of the session and writes what is left`` () =
    let fs = mockFs ()
    let state = (update fs TabOpened (loadedBoth fs)).State

    let result = update fs TabClosed state

    Assert.Equal(1, Zipper.count result.State.Tabs)
    Assert.Equal<Effect list>([ SaveSettings(settings fs result.State) ], result.Effects)

[<Fact>]
let ``the last tab stays whatever the key says`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let result = update fs TabClosed state

    Assert.Equal(1, Zipper.count result.State.Tabs)
    Assert.Equal(withHelpAt TabClosed state, result.State)

[<Fact>]
let ``asking for a save writes the session and changes nothing`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> sortBy SortKey.BySize Left

    let result = update fs SessionSaveRequested state

    Assert.Equal(withHelpAt SessionSaveRequested state, result.State)
    Assert.Equal<Effect list>([ SaveSettings(settings fs state) ], result.Effects)

[<Fact>]
let ``the tabs key asks the popup which tab to stand on`` () =
    let fs = mockFs ()
    let state = (update fs TabOpened (loadedBoth fs)).State

    let result = update fs TabsRequested state

    Assert.Equal(withHelpAt TabsRequested state, result.State)
    Assert.Equal<Effect list>([ ShowTabs state.Tabs ], result.Effects)

[<Fact>]
let ``switching to a tab stands on it and writes the session`` () =
    let fs = mockFs ()
    let state = (update fs TabOpened (loadedBoth fs)).State

    let result = update fs (TabSwitched 0) state

    Assert.Equal(0, Zipper.position result.State.Tabs)
    Assert.Equal<Effect list>([ SaveSettings(settings fs result.State) ], result.Effects)

[<Fact>]
let ``the tab that was switched to is read again`` () =
    let fs = mockFs ()
    let state = (update fs TabOpened (loadedBoth fs)).State
    fs.AddFile(at "start/added.txt", MockFileData "new")

    let result = update fs (TabSwitched 0) state

    Assert.Contains("added.txt", names result.State.Left.Entries)

[<Fact>]
let ``cycling walks to the next tab and writes the session`` () =
    let fs = mockFs ()
    let state = (update fs TabOpened (loadedBoth fs)).State

    let result = update fs (TabCycled Forward) state

    Assert.Equal(0, Zipper.position result.State.Tabs)
    Assert.Equal<Effect list>([ SaveSettings(settings fs result.State) ], result.Effects)

[<Fact>]
let ``a tab whose directory is gone is stood on all the same, and the loss reported`` () =
    let fs = mockFs ()
    let state = (update fs TabOpened (loadedBoth fs)).State
    fs.Directory.Delete(at "start", true)

    let result = update fs (TabSwitched 0) state

    Assert.Equal(0, Zipper.position result.State.Tabs)
    Assert.Equal(2, List.length result.Effects)
    Assert.Contains(result.Effects, fun effect -> effect = SaveSettings(settings fs result.State))

    Assert.Contains(
        result.Effects,
        (fun effect ->
            match effect with
            | Report(Problem.Listing _) -> true
            | _ -> false)
    )
