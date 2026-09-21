module Marbas.Tests.State.TabTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Collections
open Marbas.Domain
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddDirectory(at "notes")
    fs.AddFile(at "start/readme.txt", MockFileData "hi")
    fs :> IFileSystem

let private opened side path fs state = loadPanel fs side path state |> Expect.ok

let private started (fs: IFileSystem) =
    create (fp (at "start"))
    |> opened Left (fp (at "start")) fs
    |> opened Right (fp (at "start/docs")) fs

let private threeTabs (fs: IFileSystem) =
    started fs
    |> openTab fs
    |> opened Left (fp (at "start/docs")) fs
    |> openTab fs
    |> opened Left (fp (at "notes")) fs

let private leftPath state = (panel Left state).Path

[<Fact>]
let ``a new state holds one tab`` () =
    let state = create (fp (at "start"))

    Assert.Equal(1, Zipper.count state.Tabs)
    Assert.Equal(0, Zipper.position state.Tabs)

[<Fact>]
let ``opening a tab clones the panels of the current one and makes it current`` () =
    let fs = mockFs ()
    let state = started fs

    let twoTabs = openTab fs state

    Assert.Equal(2, Zipper.count twoTabs.Tabs)
    Assert.Equal(1, Zipper.position twoTabs.Tabs)
    Assert.Equal(state.Left.Path, twoTabs.Left.Path)
    Assert.Equal(state.Right.Path, twoTabs.Right.Path)
    Assert.Equal<string list>(
        state.Left.Entries |> Seq.map (_.Name >> EntryName.value) |> List.ofSeq,
        twoTabs.Left.Entries |> Seq.map (_.Name >> EntryName.value) |> List.ofSeq
    )

[<Fact>]
let ``the new tab starts with nothing marked`` () =
    let fs = mockFs ()
    let state = started fs |> setCursor Left 1 |> toggleSelection Left

    let twoTabs = openTab fs state

    Assert.Empty twoTabs.Left.Selected

[<Fact>]
let ``the tab left behind keeps what was marked in it`` () =
    let fs = mockFs ()
    let state = started fs |> setCursor Left 1 |> toggleSelection Left

    let back = state |> openTab fs |> switchTab 0

    Assert.Equal<Set<EntryName>>(state.Left.Selected, back.Left.Selected)

[<Fact>]
let ``closing the current tab moves to the one after it`` () =
    let fs = mockFs ()
    let state = threeTabs fs |> switchTab 1

    let closed = closeTab state

    Assert.Equal(2, Zipper.count closed.Tabs)
    Assert.Equal(1, Zipper.position closed.Tabs)
    Assert.Equal(OnDisk(fp (at "notes")), leftPath closed)

[<Fact>]
let ``closing the last tab falls back to the one before it`` () =
    let fs = mockFs ()
    let state = threeTabs fs

    let closed = closeTab state

    Assert.Equal(2, Zipper.count closed.Tabs)
    Assert.Equal(OnDisk(fp (at "start/docs")), leftPath closed)

[<Fact>]
let ``the only tab cannot be closed`` () =
    let fs = mockFs ()
    let state = started fs

    let closed = closeTab state

    Assert.Equal(1, Zipper.count closed.Tabs)
    Assert.Equal(state.Left.Path, closed.Left.Path)

[<Theory>]
[<InlineData(0, "start")>]
[<InlineData(1, "start/docs")>]
[<InlineData(2, "notes")>]
let ``switching to a tab brings its panels back`` (position: int) (expected: string) =
    let fs = mockFs ()
    let state = threeTabs fs

    let switched = switchTab position state

    Assert.Equal(OnDisk(fp (at expected)), leftPath switched)

[<Fact>]
let ``a panel loaded in one tab leaves the other tab standing where it was`` () =
    let fs = mockFs ()
    let state = threeTabs fs

    let first = switchTab 0 state

    Assert.Equal(OnDisk(fp (at "start")), leftPath first)
    Assert.Equal(OnDisk(fp (at "start/docs")), (panel Right first).Path)

[<Fact>]
let ``which side is active belongs to the tab`` () =
    let fs = mockFs ()
    let state = threeTabs fs |> activate Right

    let first = switchTab 0 state

    Assert.Equal(Left, first.Active)
    Assert.Equal(Right, (switchTab 2 first).Active)

[<Fact>]
let ``switching away closes a search left open`` () =
    let fs = mockFs ()
    let state = threeTabs fs |> switchTab 0 |> typeIntoSearch Left 'r'

    let back = state |> switchTab 1 |> switchTab 0

    Assert.Equal(None, back.Left.Search)

[<Theory>]
[<InlineData(0, "start/docs")>]
[<InlineData(1, "notes")>]
[<InlineData(2, "start")>]
let ``cycling forward walks on and wraps round to the first`` (position: int) (expected: string) =
    let fs = mockFs ()
    let state = threeTabs fs |> switchTab position

    let cycled = cycleTab Forward state

    Assert.Equal(OnDisk(fp (at expected)), leftPath cycled)

[<Theory>]
[<InlineData(0, "notes")>]
[<InlineData(1, "start")>]
[<InlineData(2, "start/docs")>]
let ``cycling back walks back and wraps round to the last`` (position: int) (expected: string) =
    let fs = mockFs ()
    let state = threeTabs fs |> switchTab position

    let cycled = cycleTab Backward state

    Assert.Equal(OnDisk(fp (at expected)), leftPath cycled)

[<Fact>]
let ``cycling away closes a search left open`` () =
    let fs = mockFs ()
    let state = threeTabs fs |> typeIntoSearch Left 'n'

    let back = state |> cycleTab Forward |> cycleTab Backward

    Assert.Equal(None, back.Left.Search)
