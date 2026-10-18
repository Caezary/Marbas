module Marbas.Tests.State.AppStateTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Locations
open Marbas.FileSystem.DirectoryListing
open Marbas.Domain
open Marbas.State
open Marbas.State.AppState
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/readme.txt", MockFileData "hi")
    fs

[<Fact>]
let ``initial state points both panels at the start path with left active`` () =
    let state = create (fp (at "start"))

    Assert.Equal(OnDisk(fp (at "start")), state.Left.Path)
    Assert.Equal(OnDisk(fp (at "start")), state.Right.Path)
    Assert.Empty state.Left.Entries
    Assert.Empty state.Right.Entries
    Assert.Equal(Left, state.Active)

[<Fact>]
let ``toggling the active panel flips between sides`` () =
    let state = create (fp (at "start"))

    let once = toggleActive state
    let twice = toggleActive once

    Assert.Equal(Right, once.Active)
    Assert.Equal(Left, twice.Active)

[<Fact>]
let ``activate selects the given side`` () =
    let state = create (fp (at "start"))

    let activated = activate Right state

    Assert.Equal(Right, activated.Active)
    Assert.Equal(activated.Right, activePanel activated)

[<Fact>]
let ``loading a panel fills its entries and leaves the other side untouched`` () =
    let fs = mockFs ()
    let state = create (fp (at "start"))

    let loaded = loadPanel fs Right (fp (at "start")) state |> Expect.ok

    Assert.Equal<string list>(
        [ ".."; "docs"; "readme.txt" ],
        loaded.Right.Entries |> Seq.map (_.Name >> EntryName.value) |> List.ofSeq
    )

    Assert.Empty loaded.Left.Entries

[<Fact>]
let ``the cursor entry is the one at the cursor index`` () =
    let fs = mockFs ()

    let state =
        create (fp (at "start")) |> loadPanel fs Left (fp (at "start")) |> Expect.ok

    let atStart = PanelState.cursorEntry state.Left
    let atThirdRow = state |> setCursor Left 2 |> activePanel |> PanelState.cursorEntry

    Assert.Equal(Some "..", atStart |> Option.map (_.Name >> EntryName.value))
    Assert.Equal(Some "readme.txt", atThirdRow |> Option.map (_.Name >> EntryName.value))

[<Fact>]
let ``an empty panel has no cursor entry`` () =
    let state = create (fp (at "start"))

    let entry = PanelState.cursorEntry state.Left

    Assert.Equal(None, entry)

[<Fact>]
let ``loading a missing directory returns the error and keeps state intact`` () =
    let fs = MockFileSystem()
    let state = create (fp (at "start"))

    let result = loadPanel fs Left (fp (at "nope")) state

    Assert.Equal(Error(DirectoryNotFound(fp (at "nope"))), result)
