module Marbas.Tests.UpdateTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.State.AppState
open Marbas.State.Update

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start\docs"
    fs.AddFile(@"C:\start\notes.txt", MockFileData "hi")
    fs.AddFile(@"C:\start\setup.fs", MockFileData "hi")
    fs.AddDirectory @"C:\backup"
    fs

let private loaded (fs: IFileSystem) =
    match create (fp @"C:\start") |> loadPanel fs Left (fp @"C:\start") with
    | Ok state -> state
    | Error err -> failwith $"setup failed: %A{err}"

let private loadedBoth (fs: IFileSystem) =
    match loaded fs |> loadPanel fs Right (fp @"C:\backup") with
    | Ok state -> state
    | Error err -> failwith $"setup failed: %A{err}"

[<Fact>]
let ``cursor moved updates the cursor without effects`` () =
    let state = loaded (mockFs ())

    let result = update (mockFs ()) (CursorMoved(Left, 2)) state

    Assert.Equal(2, result.State.Left.Cursor)
    Assert.Empty result.Effects

[<Fact>]
let ``panel focused activates the side`` () =
    let result = update (mockFs ()) (PanelFocused Right) (loaded (mockFs ()))

    Assert.Equal(Right, result.State.Active)

[<Fact>]
let ``make directory requested asks for a name`` () =
    let result = update (mockFs ()) MakeDirectoryRequested (loaded (mockFs ()))

    Assert.Equal<Effect list>([ PromptForName ], result.Effects)

[<Fact>]
let ``a valid name creates the directory on the active side`` () =
    let fs = mockFs ()

    let result = update fs (NameEntered "work") (loaded fs)

    Assert.True(fs.Directory.Exists @"C:\start\work")
    Assert.Equal(Some "work", cursorEntry result.State.Left |> Option.map _.Name)
    Assert.Empty result.Effects

[<Fact>]
let ``a blank name changes nothing and raises no effect`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = update fs (NameEntered "   ") state

    Assert.Equal(state, result.State)
    Assert.Empty result.Effects

[<Fact>]
let ``a duplicate name reports an operation error and keeps state`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = update fs (NameEntered "docs") state

    Assert.Equal(state, result.State)
    Assert.Equal<Effect list>([ ReportOperationError(TargetExists(fp @"C:\start\docs")) ], result.Effects)

[<Fact>]
let ``pattern requested defers to a pattern prompt`` () =
    let result = update (mockFs ()) (PatternRequested(Left, 0, true)) (loaded (mockFs ()))

    Assert.Equal<Effect list>([ PromptForPattern(Left, 0, true) ], result.Effects)

[<Fact>]
let ``an entered pattern marks the matching entries`` () =
    let result = update (mockFs ()) (PatternEntered(Left, 0, true, "*.fs")) (loaded (mockFs ()))

    Assert.Equal<string list>([ "setup.fs" ], selectedEntries result.State.Left |> List.map _.Name)

[<Fact>]
let ``drive requested defers to a drive prompt`` () =
    let result = update (mockFs ()) (DriveRequested Right) (loaded (mockFs ()))

    Assert.Equal<Effect list>([ PromptForDrive Right ], result.Effects)

[<Fact>]
let ``opening a path loads it into the given side`` () =
    let fs = mockFs ()

    let result = update fs (PathOpened(Right, @"C:\start")) (loaded fs)

    Assert.Equal(fp @"C:\start", result.State.Right.Path)
    Assert.Empty result.Effects

[<Fact>]
let ``opening a missing path reports a listing error`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = update fs (PathOpened(Right, @"C:\gone")) state

    Assert.Equal(state, result.State)
    Assert.Equal<Effect list>([ ReportListingError(DirectoryListing.DirectoryNotFound(fp @"C:\gone")) ], result.Effects)

[<Fact>]
let ``entering a directory navigates into it`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = update fs (EntryOpened(Left, 1)) state

    Assert.Equal(fp @"C:\start\docs", result.State.Left.Path)

[<Fact>]
let ``toggling selection marks the entry under the cursor and advances`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = update fs (SelectionToggled(Left, 2)) state

    Assert.Equal<string list>([ "notes.txt" ], selectedEntries result.State.Left |> List.map _.Name)
    Assert.Equal(3, result.State.Left.Cursor)

[<Fact>]
let ``delete requested asks to confirm the cursor entry`` () =
    let fs = mockFs ()

    let result = update fs DeleteRequested (loaded fs |> setCursor Left 2)

    Assert.Equal<Effect list>([ ConfirmDelete([ "notes.txt" ], false) ], result.Effects)

[<Fact>]
let ``delete requested warns when a target directory is not empty`` () =
    let fs = mockFs ()
    fs.AddFile(@"C:\start\docs\inner.txt", MockFileData "x")

    let result = update fs DeleteRequested (loaded fs |> setCursor Left 1)

    Assert.Equal<Effect list>([ ConfirmDelete([ "docs" ], true) ], result.Effects)

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

    let result = update fs DeleteConfirmed (loaded fs |> setCursor Left 2)

    Assert.False(fs.File.Exists @"C:\start\notes.txt")
    Assert.DoesNotContain("notes.txt", result.State.Left.Entries |> List.map _.Name)
    Assert.Empty result.Effects

[<Fact>]
let ``a delete that fails is reported item by item`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2
    fs.File.Delete @"C:\start\notes.txt"

    let result = update fs DeleteConfirmed state

    Assert.Equal<Effect list>(
        [ ReportItemFailures
              [ { Name = "notes.txt"
                  Outcome = Error(SourceNotFound(fp @"C:\start\notes.txt")) } ] ],
        result.Effects
    )

[<Fact>]
let ``copy requested aims at the other panel's directory`` () =
    let fs = mockFs ()

    let result = update fs CopyRequested (loadedBoth fs |> setCursor Left 2)

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

    Assert.Contains("notes.txt", result.State.Right.Entries |> List.map _.Name)
    Assert.Empty result.Effects

[<Fact>]
let ``a finished copy reports the items that failed`` () =
    let fs = mockFs ()

    let failed =
        [ { Name = "notes.txt"
            Outcome = Error(AccessDenied(fp @"C:\backup\notes.txt")) } ]

    let result = update fs (CopyFinished failed) (loadedBoth fs)

    Assert.Equal<Effect list>([ ReportItemFailures failed ], result.Effects)

[<Fact>]
let ``move requested offers the other panel's path`` () =
    let fs = mockFs ()

    let result = update fs MoveRequested (loadedBoth fs |> setCursor Left 2)

    Assert.Equal<Effect list>([ PromptForTarget @"C:\backup" ], result.Effects)

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

    let result = update fs (TargetEntered @"C:\backup") (loadedBoth fs |> setCursor Left 2)

    Assert.Equal<Effect list>([ MoveTargets([ fp @"C:\start\notes.txt" ], fp @"C:\backup") ], result.Effects)

[<Fact>]
let ``a bare name renames the only target`` () =
    let fs = mockFs ()

    let result = update fs (TargetEntered "readme.txt") (loadedBoth fs |> setCursor Left 2)

    Assert.Equal<Effect list>([ RenameTarget(fp @"C:\start\notes.txt", "readme.txt") ], result.Effects)

[<Fact>]
let ``a bare name with several targets names a directory beside them`` () =
    let fs = mockFs ()

    let marked =
        loadedBoth fs
        |> setCursor Left 2
        |> toggleSelectionAndAdvance Left
        |> toggleSelectionAndAdvance Left

    let result = update fs (TargetEntered "archive") marked

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

    Assert.Contains("notes.txt", result.State.Right.Entries |> List.map _.Name)
    Assert.DoesNotContain("notes.txt", result.State.Left.Entries |> List.map _.Name)
    Assert.Empty result.Effects

[<Fact>]
let ``a finished rename puts the cursor on the new name`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2
    rename fs (fp @"C:\start\notes.txt") "zzz.txt" |> ignore

    let result = update fs (RenameFinished("zzz.txt", Ok())) state

    Assert.Equal(Some "zzz.txt", cursorEntry result.State.Left |> Option.map _.Name)
    Assert.Empty result.Effects

[<Fact>]
let ``a failed rename is reported and leaves the panel alone`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2
    let taken = TargetExists(fp @"C:\start\docs")

    let result = update fs (RenameFinished("docs", Error taken)) state

    Assert.Equal(state, result.State)
    Assert.Equal<Effect list>([ ReportOperationError taken ], result.Effects)

[<Fact>]
let ``quit requested raises the quit effect`` () =
    let result = update (mockFs ()) QuitRequested (loaded (mockFs ()))

    Assert.Equal<Effect list>([ Quit ], result.Effects)
