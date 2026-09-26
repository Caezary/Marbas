module Marbas.Tests.State.SettingsTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Collections
open Marbas.Domain
open Marbas.State
open Marbas.State.AppState
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private en = EntryName.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddDirectory(at "backup")
    fs

let private standingIn location (state: AppState) =
    { state with
        Tabs = state.Tabs |> Zipper.update (fun tab -> { tab with Tab.Left.Path = location }) }

let private storedTab: TabSettings =
    { Left =
        { Path = fp (at "backup")
          Sort = SortOrder.create BySize Descending
          Hidden = Shown
          Cursor = None }
      Right =
        { Path = fp (at "start")
          Sort = SortOrder.create ByDate Ascending
          Hidden = Concealed
          Cursor = None }
      Active = Left }

let private stored: Settings =
    { Tabs = Zipper.single storedTab
      LastCreatedDirectory = Some(en "work")
      Programs =
        { DefaultEditor = ProgramPath.tryCreate "code.exe"
          Associations =
            [ { Extension = Expect.some (FileExtension.tryCreate "txt")
                Role = ForEditing
                Program = Expect.some (ProgramPath.tryCreate "notepad.exe") } ] } }

[<Fact>]
let ``a state built from settings gives each panel its own path, sort and hidden`` () =
    let state = restore stored

    Assert.Equal(OnDisk(fp (at "backup")), state.Left.Path)
    Assert.Equal(SortOrder.create BySize Descending, state.Left.Sort)
    Assert.Equal(Shown, state.Left.Hidden)
    Assert.Equal(OnDisk(fp (at "start")), state.Right.Path)
    Assert.Equal(SortOrder.create ByDate Ascending, state.Right.Sort)
    Assert.Equal(Concealed, state.Right.Hidden)
    Assert.Equal(Left, state.Active)

[<Fact>]
let ``a state built from settings has one tab for every stored tab`` () =
    let second =
        { storedTab with
            Left = { storedTab.Left with Path = fp (at "start") }
            Active = Right }

    let state =
        restore
            { stored with
                Tabs = Zipper.ofNonEmpty storedTab [ second ] |> Zipper.refocus 1 }

    Assert.Equal(2, Zipper.count state.Tabs)
    Assert.Equal(1, Zipper.position state.Tabs)
    Assert.Equal(OnDisk(fp (at "start")), state.Left.Path)
    Assert.Equal(Right, state.Active)

[<Fact>]
let ``the settings of a state name every tab it holds`` () =
    let fs = mockFs ()

    let state =
        create (fp (at "start"))
        |> loadPanel fs Left (fp (at "start"))
        |> Expect.ok
        |> openTab fs
        |> loadPanel fs Left (fp (at "backup"))
        |> Expect.ok

    let written = settings fs state

    Assert.Equal<string list>(
        [ at "start"; at "backup" ],
        written.Tabs |> Zipper.items |> List.map (fun tab -> FileSystemPath.value tab.Left.Path)
    )

    Assert.Equal(1, Zipper.position written.Tabs)

[<Fact>]
let ``an initial state is the default settings for the start path`` () =
    let state = create (fp (at "start"))

    Assert.Equal(restore (Settings.forPath (fp (at "start"))), state)

[<Fact>]
let ``the settings of a state are what its panels are set to`` () =
    let fs = mockFs ()

    let state =
        restore stored
        |> loadPanel fs Left (fp (at "backup"))
        |> Expect.ok
        |> loadPanel fs Right (fp (at "start"))
        |> Expect.ok

    Assert.Equal(stored, settings fs state)

[<Fact>]
let ``a sorted panel says so in the settings`` () =
    let fs = mockFs ()
    let state = create (fp (at "start")) |> loadPanel fs Left (fp (at "start")) |> Expect.ok

    let sorted = sortBy BySize Left state

    Assert.Equal(SortOrder.create BySize Ascending, (settings fs sorted).Left.Sort)
    Assert.Equal(SortOrder.byName, (settings fs sorted).Right.Sort)

[<Fact>]
let ``a panel showing its hidden entries says so in the settings`` () =
    let fs = mockFs ()
    let state = create (fp (at "start")) |> loadPanel fs Right (fp (at "start")) |> Expect.ok

    let toggled = toggleHidden fs Right state |> Expect.ok

    Assert.Equal(Shown, (settings fs toggled).Right.Hidden)
    Assert.Equal(Concealed, (settings fs toggled).Left.Hidden)

[<Fact>]
let ``a panel that has been navigated says where it is in the settings`` () =
    let fs = mockFs ()
    let state = create (fp (at "start")) |> loadPanel fs Left (fp (at "start/docs")) |> Expect.ok

    let current = settings fs state

    Assert.Equal(fp (at "start/docs"), current.Left.Path)
    Assert.Equal(fp (at "start"), current.Right.Path)

[<Fact>]
let ``a panel inside an archive says the directory holding it in the settings`` () =
    let fs = mockFs ()
    let state = create (fp (at "start")) |> loadPanel fs Left (fp (at "start")) |> Expect.ok

    let inside = state |> standingIn (InArchive(fp (at "start/backup.zip"), ArchivePath.root))

    Assert.Equal(fp (at "start"), (settings fs inside).Left.Path)

[<Fact>]
let ``a state built from settings remembers the name the last directory was given`` () =
    let state = restore stored

    Assert.Equal(Some(en "work"), state.LastCreatedDirectory)

[<Fact>]
let ``a state built from settings carries the programs it names`` () =
    let state = restore stored

    Assert.Equal(stored.Programs, state.Programs)

[<Fact>]
let ``programs that have been changed go into the settings`` () =
    let fs = mockFs ()
    let state = create (fp (at "start"))

    let changed = withPrograms stored.Programs state

    Assert.Equal(stored.Programs, (settings fs changed).Programs)
    Assert.Equal(ProgramSettings.none, (settings fs state).Programs)

[<Fact>]
let ``the editor a name is opened with comes from the state`` () =
    let state = restore stored

    Assert.Equal(ProgramPath.tryCreate "notepad.exe", programFor ForEditing (en "notes.txt") state)
    Assert.Equal(ProgramPath.tryCreate "code.exe", programFor ForEditing (en "Program.fs") state)

[<Fact>]
let ``the name a new directory was given goes into the settings`` () =
    let fs = mockFs ()
    let state = create (fp (at "start")) |> loadPanel fs Left (fp (at "start")) |> Expect.ok

    let made = makeDirectory fs Left (fp (at "start")) (en "reports") state |> Expect.ok

    Assert.Equal(Some(en "reports"), (settings fs made).LastCreatedDirectory)
    Assert.Equal(None, (settings fs state).LastCreatedDirectory)

[<Fact>]
let ``the entry under each cursor goes into the settings`` () =
    let fs = mockFs ()

    let state =
        create (fp (at "start"))
        |> loadPanel fs Left (fp (at "start"))
        |> Expect.ok
        |> setCursor Left 2

    let written = settings fs state

    Assert.Equal(Some(en "notes.txt"), written.Left.Cursor)
    Assert.Equal(None, written.Right.Cursor)

[<Fact>]
let ``a tab that was never read keeps the cursor the file remembered`` () =
    let fs = mockFs ()

    let remembered =
        { stored with
            Tabs =
                Zipper.ofNonEmpty
                    { storedTab with
                        TabSettings.Left.Cursor = Some(en "notes.txt") }
                    [ { storedTab with
                          TabSettings.Left.Cursor = Some(en "docs") } ] }

    let written = restore remembered |> settings fs

    Assert.Equal<EntryName option list>(
        [ Some(en "notes.txt"); Some(en "docs") ],
        written.Tabs |> Zipper.items |> List.map _.Left.Cursor
    )

[<Fact>]
let ``a panel opens on the entry the session remembered`` () =
    let fs = mockFs ()

    let state =
        restore
            { stored with
                Tabs =
                    stored.Tabs
                    |> Zipper.map (fun tab ->
                        { tab with
                            Left = { tab.Left with Path = fp (at "start"); Cursor = Some(en "notes.txt") } }) }
        |> loadPanel fs Left (fp (at "start"))
        |> Expect.ok

    Assert.Equal(Some(en "notes.txt"), PanelState.cursorEntry state.Left |> Option.map _.Name)

[<Fact>]
let ``a remembered entry that is gone leaves the cursor at the top`` () =
    let fs = mockFs ()

    let state =
        restore
            { stored with
                Tabs =
                    stored.Tabs
                    |> Zipper.map (fun tab ->
                        { tab with
                            Left = { tab.Left with Path = fp (at "start"); Cursor = Some(en "renamed.txt") } }) }
        |> loadPanel fs Left (fp (at "start"))
        |> Expect.ok

    Assert.Equal(0, state.Left.Cursor)

[<Fact>]
let ``once a panel has been read it remembers its own cursor`` () =
    let fs = mockFs ()

    let state =
        restore
            { stored with
                Tabs =
                    stored.Tabs
                    |> Zipper.map (fun tab ->
                        { tab with
                            Left = { tab.Left with Path = fp (at "start"); Cursor = Some(en "notes.txt") } }) }
        |> loadPanel fs Left (fp (at "start"))
        |> Expect.ok
        |> setCursor Left 1

    Assert.Equal(Some(en "docs"), (settings fs state).Left.Cursor)

[<Fact>]
let ``a cursor resting on the way out remembers nothing`` () =
    let fs = mockFs ()

    let state = create (fp (at "start")) |> loadPanel fs Left (fp (at "start")) |> Expect.ok

    Assert.Equal(Some "..", PanelState.cursorEntry state.Left |> Option.map (_.Name >> EntryName.value))
    Assert.Equal(None, (settings fs state).Left.Cursor)
