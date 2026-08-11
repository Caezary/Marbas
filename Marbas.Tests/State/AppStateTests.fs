module Marbas.Tests.State.AppStateTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.State.AppState
open Marbas.Tests

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start\docs"
    fs.AddFile(@"C:\start\readme.txt", MockFileData "hi")
    fs

[<Fact>]
let ``initial state points both panels at the start path with left active`` () =
    let state = create (fp @"C:\start")

    Assert.Equal(fp @"C:\start", state.Left.Path)
    Assert.Equal(fp @"C:\start", state.Right.Path)
    Assert.Empty state.Left.Entries
    Assert.Empty state.Right.Entries
    Assert.Equal(Left, state.Active)

[<Fact>]
let ``toggling the active panel flips between sides`` () =
    let state = create (fp @"C:\start")

    let once = toggleActive state
    let twice = toggleActive once

    Assert.Equal(Right, once.Active)
    Assert.Equal(Left, twice.Active)

[<Fact>]
let ``activate selects the given side`` () =
    let state = create (fp @"C:\start")

    let activated = activate Right state

    Assert.Equal(Right, activated.Active)
    Assert.Equal(activated.Right, activePanel activated)

[<Fact>]
let ``loading a panel fills its entries and leaves the other side untouched`` () =
    let fs = mockFs ()
    let state = create (fp @"C:\start")

    let loaded = loadPanel fs Right (fp @"C:\start") state |> Expect.ok

    Assert.Equal<string list>([ ".."; "docs"; "readme.txt" ], loaded.Right.Entries |> List.map (_.Name >> EntryName.value))
    Assert.Empty loaded.Left.Entries

[<Fact>]
let ``the cursor entry is the one at the cursor index`` () =
    let fs = mockFs ()
    let state = create (fp @"C:\start") |> loadPanel fs Left (fp @"C:\start") |> Expect.ok

    let atStart = cursorEntry state.Left
    let atThirdRow = state |> setCursor Left 2 |> activePanel |> cursorEntry

    Assert.Equal(Some "..", atStart |> Option.map (_.Name >> EntryName.value))
    Assert.Equal(Some "readme.txt", atThirdRow |> Option.map (_.Name >> EntryName.value))

[<Fact>]
let ``an empty panel has no cursor entry`` () =
    let state = create (fp @"C:\start")

    let entry = cursorEntry state.Left

    Assert.Equal(None, entry)

[<Fact>]
let ``loading a missing directory returns the error and keeps state intact`` () =
    let fs = MockFileSystem()
    let state = create (fp @"C:\start")

    let result = loadPanel fs Left (fp @"C:\nope") state

    Assert.Equal(Error(DirectoryNotFound(fp @"C:\nope")), result)
