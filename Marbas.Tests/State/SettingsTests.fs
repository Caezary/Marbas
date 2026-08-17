module Marbas.Tests.State.SettingsTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddDirectory(at "backup")
    fs

let private stored: Settings =
    { Left =
        { Path = fp (at "backup")
          Sort = SortOrder.create BySize Descending
          Hidden = Shown }
      Right =
        { Path = fp (at "start")
          Sort = SortOrder.create ByDate Ascending
          Hidden = Concealed } }

[<Fact>]
let ``a state built from settings gives each panel its own path, sort and hidden`` () =
    let state = fromSettings stored

    Assert.Equal(fp (at "backup"), state.Left.Path)
    Assert.Equal(SortOrder.create BySize Descending, state.Left.Sort)
    Assert.Equal(Shown, state.Left.Hidden)
    Assert.Equal(fp (at "start"), state.Right.Path)
    Assert.Equal(SortOrder.create ByDate Ascending, state.Right.Sort)
    Assert.Equal(Concealed, state.Right.Hidden)
    Assert.Equal(Left, state.Active)

[<Fact>]
let ``an initial state is the default settings for the start path`` () =
    let state = create (fp (at "start"))

    Assert.Equal(fromSettings (Settings.forPath (fp (at "start"))), state)

[<Fact>]
let ``the settings of a state are what its panels are set to`` () =
    let fs = mockFs ()

    let state =
        fromSettings stored
        |> loadPanel fs Left (fp (at "backup"))
        |> Expect.ok
        |> loadPanel fs Right (fp (at "start"))
        |> Expect.ok

    Assert.Equal(stored, settings state)

[<Fact>]
let ``a sorted panel says so in the settings`` () =
    let fs = mockFs ()
    let state = create (fp (at "start")) |> loadPanel fs Left (fp (at "start")) |> Expect.ok

    let sorted = sortBy BySize Left state

    Assert.Equal(SortOrder.create BySize Ascending, (settings sorted).Left.Sort)
    Assert.Equal(SortOrder.byName, (settings sorted).Right.Sort)

[<Fact>]
let ``a panel showing its hidden entries says so in the settings`` () =
    let fs = mockFs ()
    let state = create (fp (at "start")) |> loadPanel fs Right (fp (at "start")) |> Expect.ok

    let toggled = toggleHidden fs Right state |> Expect.ok

    Assert.Equal(Shown, (settings toggled).Right.Hidden)
    Assert.Equal(Concealed, (settings toggled).Left.Hidden)

[<Fact>]
let ``a panel that has been navigated says where it is in the settings`` () =
    let fs = mockFs ()
    let state = create (fp (at "start")) |> loadPanel fs Left (fp (at "start/docs")) |> Expect.ok

    let current = settings state

    Assert.Equal(fp (at "start/docs"), current.Left.Path)
    Assert.Equal(fp (at "start"), current.Right.Path)
