module Marbas.Tests.State.UpdateTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests

let private fp = FileSystemPath.create

let private en = EntryName.create

let private cursor side index = { Side = side; Index = index }

let private names (entries: DirectoryListing.Entry list) =
    entries |> List.map (_.Name >> EntryName.value)

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start\docs"
    fs.AddFile(@"C:\start\notes.txt", MockFileData "hi")
    fs.AddFile(@"C:\start\setup.fs", MockFileData "hi")
    fs.AddDirectory @"C:\backup"
    fs

let private loaded (fs: IFileSystem) =
    create (fp @"C:\start") |> loadPanel fs Left (fp @"C:\start") |> Expect.ok

let private loadedBoth (fs: IFileSystem) =
    loaded fs |> loadPanel fs Right (fp @"C:\backup") |> Expect.ok

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

    Assert.True(fs.Directory.Exists @"C:\start\work")
    Assert.Equal(Some "work", cursorEntry result.State.Left |> Option.map (_.Name >> EntryName.value))
    Assert.Empty result.Effects

[<Fact>]
let ``a duplicate name reports an operation error and keeps state`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = update fs (NameEntered(en "docs")) state

    Assert.Equal(state, result.State)
    Assert.Equal<Effect list>([ ReportOperationError(TargetExists(fp @"C:\start\docs")) ], result.Effects)

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

    Assert.Equal<string list>([ "setup.fs" ], selectedEntries result.State.Left |> names)

[<Fact>]
let ``drive requested defers to a drive prompt`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = update fs (DriveRequested Right) state

    Assert.Equal<Effect list>([ PromptForDrive Right ], result.Effects)

[<Fact>]
let ``opening a path loads it into the given side`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = update fs (PathOpened(Right, fp @"C:\start")) state

    Assert.Equal(fp @"C:\start", result.State.Right.Path)
    Assert.Empty result.Effects

[<Fact>]
let ``opening a missing path reports a listing error`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = update fs (PathOpened(Right, fp @"C:\gone")) state

    Assert.Equal(state, result.State)
    Assert.Equal<Effect list>([ ReportListingError(DirectoryListing.DirectoryNotFound(fp @"C:\gone")) ], result.Effects)

[<Fact>]
let ``entering a directory navigates into it`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 1

    let result = update fs EntryOpened state

    Assert.Equal(fp @"C:\start\docs", result.State.Left.Path)

[<Fact>]
let ``toggling selection marks the entry under the cursor and advances`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2

    let result = update fs SelectionToggled state

    Assert.Equal<string list>([ "notes.txt" ], selectedEntries result.State.Left |> names)
    Assert.Equal(3, result.State.Left.Cursor)

[<Fact>]
let ``delete requested asks to confirm the cursor entry`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2

    let result = update fs DeleteRequested state

    Assert.Equal<Effect list>(
        [ ConfirmDelete
              { Targets = [ en "notes.txt" ]
                Scope = NamedEntriesOnly } ],
        result.Effects
    )

[<Fact>]
let ``delete requested warns when a target directory is not empty`` () =
    let fs = mockFs ()
    fs.AddFile(@"C:\start\docs\inner.txt", MockFileData "x")
    let state = loaded fs |> setCursor Left 1

    let result = update fs DeleteRequested state

    Assert.Equal<Effect list>(
        [ ConfirmDelete
              { Targets = [ en "docs" ]
                Scope = WithDirectoryContents } ],
        result.Effects
    )

[<Fact>]
let ``delete requested on the parent entry asks nothing`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = update fs DeleteRequested state

    Assert.Equal(state, result.State)
    Assert.Empty result.Effects

[<Fact>]
let ``a confirmed delete removes the target and raises no effect`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2

    let result = update fs DeleteConfirmed state

    Assert.False(fs.File.Exists @"C:\start\notes.txt")
    Assert.DoesNotContain("notes.txt", names result.State.Left.Entries)
    Assert.Empty result.Effects

[<Fact>]
let ``a delete that fails is reported item by item`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2
    fs.File.Delete @"C:\start\notes.txt"

    let result = update fs DeleteConfirmed state

    Assert.Equal<Effect list>(
        [ ReportItemFailures
              [ { Name = EntryName.create "notes.txt"
                  Outcome = Error(SourceNotFound(fp @"C:\start\notes.txt")) } ] ],
        result.Effects
    )

[<Fact>]
let ``copy requested aims at the other panel's directory`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2

    let result = update fs CopyRequested state

    Assert.Equal<Effect list>([ CopyTargets([ fp @"C:\start\notes.txt" ], fp @"C:\backup") ], result.Effects)

[<Fact>]
let ``copy requested on the parent entry raises no effect`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let result = update fs CopyRequested state

    Assert.Equal(state, result.State)
    Assert.Empty result.Effects

[<Fact>]
let ``a finished copy shows the new entry in the other panel`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2
    let results = copyAll fs unattended [ fp @"C:\start\notes.txt" ] (fp @"C:\backup")

    let result = update fs (CopyFinished results) state

    Assert.Contains("notes.txt", names result.State.Right.Entries)
    Assert.Empty result.Effects

[<Fact>]
let ``a finished copy reports the items that failed`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let failed =
        [ { Name = EntryName.create "notes.txt"
            Outcome = Error(AccessDenied(fp @"C:\backup\notes.txt")) } ]

    let result = update fs (CopyFinished failed) state

    Assert.Equal<Effect list>([ ReportItemFailures failed ], result.Effects)

[<Fact>]
let ``move requested offers the other panel's path`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2

    let result = update fs MoveRequested state

    Assert.Equal<Effect list>([ PromptForTarget(fp @"C:\backup") ], result.Effects)

[<Fact>]
let ``move requested on the parent entry raises no effect`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let result = update fs MoveRequested state

    Assert.Equal(state, result.State)
    Assert.Empty result.Effects

[<Fact>]
let ``an entered path moves the targets into it`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2

    let result = update fs (TargetEntered @"C:\backup") state

    Assert.Equal<Effect list>([ MoveTargets([ fp @"C:\start\notes.txt" ], fp @"C:\backup") ], result.Effects)

[<Fact>]
let ``a bare name renames the only target`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2

    let result = update fs (TargetEntered "readme.txt") state

    Assert.Equal<Effect list>([ RenameTarget(fp @"C:\start\notes.txt", en "readme.txt") ], result.Effects)

[<Fact>]
let ``a bare name with several targets names a directory beside them`` () =
    let fs = mockFs ()

    let state =
        loadedBoth fs
        |> setCursor Left 2
        |> toggleSelectionAndAdvance Left
        |> toggleSelectionAndAdvance Left

    let result = update fs (TargetEntered "archive") state

    Assert.Equal<Effect list>(
        [ MoveTargets([ fp @"C:\start\notes.txt"; fp @"C:\start\setup.fs" ], fp @"C:\start\archive") ],
        result.Effects
    )

[<Fact>]
let ``a blank target changes nothing`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2

    let result = update fs (TargetEntered "   ") state

    Assert.Equal(state, result.State)
    Assert.Empty result.Effects

[<Fact>]
let ``a finished move shows the entry in the other panel`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2
    let results = moveAll fs unattended [ fp @"C:\start\notes.txt" ] (fp @"C:\backup")

    let result = update fs (MoveFinished results) state

    Assert.Contains("notes.txt", names result.State.Right.Entries)
    Assert.DoesNotContain("notes.txt", names result.State.Left.Entries)
    Assert.Empty result.Effects

[<Fact>]
let ``a finished rename puts the cursor on the new name`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2
    rename fs (fp @"C:\start\notes.txt") (en "zzz.txt") |> ignore

    let result = update fs (RenameFinished(en "zzz.txt", Ok())) state

    Assert.Equal(Some "zzz.txt", cursorEntry result.State.Left |> Option.map (_.Name >> EntryName.value))
    Assert.Empty result.Effects

[<Fact>]
let ``a failed rename is reported and leaves the panel alone`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2
    let taken = TargetExists(fp @"C:\start\docs")

    let result = update fs (RenameFinished(en "docs", Error taken)) state

    Assert.Equal(state, result.State)
    Assert.Equal<Effect list>([ ReportOperationError taken ], result.Effects)

[<Fact>]
let ``view requested aims at the file under the cursor`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2

    let result = update fs ViewRequested state

    Assert.Equal<Effect list>([ ViewFile(fp @"C:\start\notes.txt") ], result.Effects)

[<Fact>]
let ``view requested on a directory raises no effect`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 1

    let result = update fs ViewRequested state

    Assert.Equal(state, result.State)
    Assert.Empty result.Effects

[<Fact>]
let ``view requested on the parent entry raises no effect`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = update fs ViewRequested state

    Assert.Equal(state, result.State)
    Assert.Empty result.Effects

[<Fact>]
let ``view requested ignores what is marked and takes the cursor entry`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 3 |> toggleSelection Left |> setCursor Left 2

    let result = update fs ViewRequested state

    Assert.Equal<Effect list>([ ViewFile(fp @"C:\start\notes.txt") ], result.Effects)

[<Fact>]
let ``edit requested aims at the file under the cursor`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2

    let result = update fs EditRequested state

    Assert.Equal<Effect list>([ EditFile(fp @"C:\start\notes.txt") ], result.Effects)

[<Fact>]
let ``edit requested on a directory raises no effect`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 1

    let result = update fs EditRequested state

    Assert.Equal(state, result.State)
    Assert.Empty result.Effects

[<Fact>]
let ``a finished edit re-reads the panel it was started from`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2
    fs.AddFile(@"C:\start\notes.txt.bak", MockFileData "hi")

    let result = update fs (EditFinished(Ok())) state

    Assert.Contains("notes.txt.bak", names result.State.Left.Entries)
    Assert.Equal(Some "notes.txt", cursorEntry result.State.Left |> Option.map (_.Name >> EntryName.value))
    Assert.Empty result.Effects

[<Fact>]
let ``an editor that could not be started is reported`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2
    let failure = OperationFailed(fp @"C:\start\notes.txt", "no editor")

    let result = update fs (EditFinished(Error failure)) state

    Assert.Equal(state, result.State)
    Assert.Equal<Effect list>([ ReportOperationError failure ], result.Effects)

[<Fact>]
let ``a hidden toggle re-reads the active panel with its hidden entries`` () =
    let fs = mockFs ()
    fs.AddFile(@"C:\start\.profile", MockFileData "hi")
    let state = loadedBoth fs |> activate Left

    let result = update fs HiddenToggled state

    Assert.Contains(".profile", names result.State.Left.Entries)
    Assert.DoesNotContain(".profile", names (update fs HiddenToggled result.State).State.Left.Entries)
    Assert.Empty result.Effects

[<Fact>]
let ``a hidden toggle of a vanished directory is reported`` () =
    let fs = mockFs ()
    let state = loaded fs |> loadPanel fs Left (fp @"C:\start\docs") |> Expect.ok
    fs.Directory.Delete @"C:\start\docs"

    let result = update fs HiddenToggled state

    Assert.Equal(state, result.State)
    Assert.Equal<Effect list>([ ReportListingError(DirectoryListing.DirectoryNotFound(fp @"C:\start\docs")) ], result.Effects)

[<Fact>]
let ``quit requested saves what the panels are set to before it quits`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> sortBy SortKey.BySize Left

    let result = update fs QuitRequested state

    Assert.Equal<Effect list>([ SaveSettings(settings state); Quit ], result.Effects)

[<Fact>]
let ``step pairs the new state with its effects`` () =
    let fs = mockFs ()
    let state = loaded fs

    let nextState, effects = step fs QuitRequested state

    Assert.Equal(state, nextState)
    Assert.Equal<Effect list>([ SaveSettings(settings state); Quit ], effects)
