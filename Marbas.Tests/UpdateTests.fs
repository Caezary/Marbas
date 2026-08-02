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
    fs

let private loaded (fs: IFileSystem) =
    match create (fp @"C:\start") |> loadPanel fs Left (fp @"C:\start") with
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
let ``quit requested raises the quit effect`` () =
    let result = update (mockFs ()) QuitRequested (loaded (mockFs ()))

    Assert.Equal<Effect list>([ Quit ], result.Effects)
