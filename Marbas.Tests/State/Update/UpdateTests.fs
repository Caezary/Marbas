module Marbas.Tests.State.Update.UpdateTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Locations
open Marbas.FileSystem.Operations
open Marbas.Domain
open Marbas.Messages
open Marbas.State
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.Update.UpdateFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private en = EntryName.create

[<Fact>]
let ``cursor moved updates the cursor without effects`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = update fs (CursorMoved(cursor Left 2)) state

    Assert.Equal(2, result.State.Left.Cursor)
    Assert.Empty result.Effects

[<Fact>]
let ``a sort request reorders the panel it names and keeps the cursor entry`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2

    let result = update fs (SortRequested SortKey.ByExtension) state

    Assert.Equal<string list>([ ".."; "docs"; "setup.fs"; "notes.txt" ], names result.State.Left.Entries)
    Assert.Equal(3, result.State.Left.Cursor)
    Assert.Empty result.Effects

[<Fact>]
let ``a typed character searches the active panel without effects`` () =
    let fs = mockFs ()
    let state = loaded fs |> activate Left

    let result = update fs (SearchTyped 'n') state

    Assert.Equal(Some "n", result.State.Left.Search |> Option.map SearchText.value)
    Assert.Equal(2, result.State.Left.Cursor)
    Assert.Empty result.Effects

[<Fact>]
let ``a backspace shortens the search of the active panel`` () =
    let fs = mockFs ()
    let state = update fs (SearchTyped 'n') (loaded fs) |> _.State

    let result = update fs SearchBackspaced state

    Assert.Equal<SearchText option>(None, result.State.Left.Search)
    Assert.Empty result.Effects

[<Fact>]
let ``a cancelled search closes it on the active panel`` () =
    let fs = mockFs ()
    let state = update fs (SearchTyped 'n') (loaded fs) |> _.State

    let result = update fs SearchCancelled state

    Assert.Equal<SearchText option>(None, result.State.Left.Search)
    Assert.Equal(2, result.State.Left.Cursor)
    Assert.Empty result.Effects

[<Fact>]
let ``panel focused activates the side`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = update fs (PanelFocused Right) state

    Assert.Equal(Right, result.State.Active)

[<Fact>]
let ``make directory requested asks for a name`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = update fs MakeDirectoryRequested state

    Assert.Equal<Effect list>([ PromptForName ], result.Effects)

[<Fact>]
let ``a valid name creates the directory on the active side`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = update fs (NameEntered(en "work")) state

    Assert.True(fs.Directory.Exists(at "start/work"))

    Assert.Equal(
        Some "work",
        PanelState.cursorEntry result.State.Left
        |> Option.map (_.Name >> EntryName.value)
    )

    Assert.Equal<Effect list>(
        [ ChangeHistory(Remember(Typed(NewDirectory, Expect.some (EnteredText.tryCreate "work")))) ],
        result.Effects
    )

[<Fact>]
let ``a duplicate name reports an operation error and keeps state`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = update fs (NameEntered(en "docs")) state

    Assert.Equal(state, result.State)
    Assert.Equal<Effect list>([ Report(Problem.Operation(TargetExists(fp (at "start/docs")))) ], result.Effects)

[<Fact>]
let ``pattern requested defers to a pattern prompt`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = update fs (PatternRequested Select) state

    Assert.Equal<Effect list>([ PromptForPattern Select ], result.Effects)

[<Fact>]
let ``an entered pattern marks the matching entries`` () =
    let fs = mockFs ()
    let state = loaded fs
    let pattern = NamePattern.tryCreate "*.fs" |> Expect.some

    let result = update fs (PatternEntered(Select, pattern)) state

    Assert.Equal<string list>([ "setup.fs" ], PanelState.selectedEntries result.State.Left |> names)

[<Fact>]
let ``drive requested defers to a drive prompt`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = update fs (RootRequested Right) state

    Assert.Equal<Effect list>([ PromptForRoot Right ], result.Effects)

[<Fact>]
let ``opening a path loads it into the given side`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = update fs (PathOpened(Right, fp (at "start"))) state

    Assert.Equal(OnDisk(fp (at "start")), result.State.Right.Path)
    Assert.Empty result.Effects

[<Fact>]
let ``opening a missing path reports a listing error`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = update fs (PathOpened(Right, fp (at "gone"))) state

    Assert.Equal(state, result.State)

    Assert.Equal<Effect list>(
        [ Report(Problem.Listing(DirectoryListing.DirectoryNotFound(fp (at "gone")))) ],
        result.Effects
    )

[<Fact>]
let ``entering a directory navigates into it`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 1

    let result = update fs EntryOpened state

    Assert.Equal(OnDisk(fp (at "start/docs")), result.State.Left.Path)

[<Fact>]
let ``toggling selection marks the entry under the cursor and advances`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2

    let result = update fs (SelectionToggled Downward) state

    Assert.Equal<string list>([ "notes.txt" ], PanelState.selectedEntries result.State.Left |> names)
    Assert.Equal(3, result.State.Left.Cursor)

[<Fact>]
let ``toggling selection upward marks the entry under the cursor and steps back`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2

    let result = update fs (SelectionToggled Upward) state

    Assert.Equal<string list>([ "notes.txt" ], PanelState.selectedEntries result.State.Left |> names)
    Assert.Equal(1, result.State.Left.Cursor)

[<Fact>]
let ``a hidden toggle re-reads the active panel with its hidden entries`` () =
    let fs = mockFs ()
    fs.AddFile(at "start/.profile", MockFileData "hi")
    let state = loadedBoth fs |> activate Left

    let result = update fs HiddenToggled state

    Assert.Contains(".profile", names result.State.Left.Entries)
    Assert.DoesNotContain(".profile", names (update fs HiddenToggled result.State).State.Left.Entries)
    Assert.Empty result.Effects

[<Fact>]
let ``a hidden toggle of a vanished directory is reported`` () =
    let fs = mockFs ()
    let state = loaded fs |> loadPanel fs Left (fp (at "start/docs")) |> Expect.ok
    fs.Directory.Delete(at "start/docs")

    let result = update fs HiddenToggled state

    Assert.Equal(state, result.State)

    Assert.Equal<Effect list>(
        [ Report(Problem.Listing(DirectoryListing.DirectoryNotFound(fp (at "start/docs")))) ],
        result.Effects
    )

[<Fact>]
let ``quit requested saves what the panels are set to before it quits`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> sortBy SortKey.BySize Left

    let result = update fs QuitRequested state

    Assert.Equal<Effect list>([ SaveSettings(settings fs state); Quit ], result.Effects)

[<Fact>]
let ``step pairs the new state with its effects`` () =
    let fs = mockFs ()
    let state = loaded fs

    let nextState, effects = step fs QuitRequested state

    Assert.Equal(state, nextState)
    Assert.Equal<Effect list>([ SaveSettings(settings fs state); Quit ], effects)
