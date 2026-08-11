module Marbas.Tests.State.MkdirTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.State.AppState
open Marbas.Tests

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start\docs"
    fs.AddFile(@"C:\start\notes.txt", MockFileData "hi")
    fs

let private loaded (fs: IFileSystem) =
    create (fp @"C:\start") |> loadPanel fs Left (fp @"C:\start") |> Expect.ok

let private names panel = panel.Entries |> List.map _.Name

let private selectedNames panel = selectedEntries panel |> List.map _.Name

[<Fact>]
let ``making a directory adds it and lands the cursor on it`` () =
    let fs = mockFs ()
    let state = loaded fs

    let updated = makeDirectory fs Left "work" state |> Expect.ok

    Assert.True(fs.Directory.Exists @"C:\start\work")
    Assert.Contains("work", names updated.Left)
    Assert.Equal(Some "work", cursorEntry updated.Left |> Option.map _.Name)

[<Fact>]
let ``making a directory keeps the existing selection`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2 |> toggleSelectionAndAdvance Left

    let updated = makeDirectory fs Left "work" state |> Expect.ok

    Assert.Equal<string list>([ "notes.txt" ], selectedNames state.Left)
    Assert.Equal<string list>([ "notes.txt" ], selectedNames updated.Left)

[<Fact>]
let ``an existing name is refused and leaves the panel unchanged`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = makeDirectory fs Left "docs" state

    Assert.Equal(Error(TargetExists(fp @"C:\start\docs")), result)
