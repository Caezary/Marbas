module Marbas.Tests.State.AutoRefreshTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private en = EntryName.create

let private names (entries: DirectoryListing.Entry seq) =
    entries |> Seq.map (_.Name >> EntryName.value) |> List.ofSeq

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "start/setup.fs", MockFileData "hi")
    fs.AddDirectory(at "backup")
    fs

let private loadedBoth (fs: IFileSystem) =
    create (fp (at "start"))
    |> loadPanel fs Left (fp (at "start"))
    |> Expect.ok
    |> loadPanel fs Right (fp (at "backup"))
    |> Expect.ok

[<Fact>]
let ``an external change reads both panels of the tab in front`` () =
    let fs = mockFs ()
    let state = loadedBoth fs
    fs.AddFile(at "start/added.txt", MockFileData "new")
    fs.AddFile(at "backup/kept.txt", MockFileData "new")

    let result = update fs ExternalChangeDetected state

    Assert.Contains("added.txt", names result.State.Left.Entries)
    Assert.Contains("kept.txt", names result.State.Right.Entries)

[<Fact>]
let ``an external change asks for nothing to be shown`` () =
    let fs = mockFs ()
    let state = loadedBoth fs
    fs.AddFile(at "start/added.txt", MockFileData "new")

    let result = update fs ExternalChangeDetected state

    Assert.Empty result.Effects

[<Fact>]
let ``an external change keeps the cursor on the entry it stood on`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2
    fs.AddFile(at "start/added.txt", MockFileData "new")

    let result = update fs ExternalChangeDetected state

    Assert.Equal<string list>(
        [ ".."; "docs"; "added.txt"; "notes.txt"; "setup.fs" ],
        names result.State.Left.Entries
    )

    Assert.Equal(3, result.State.Left.Cursor)

[<Fact>]
let ``an external change keeps the marks that survived it`` () =
    let fs = mockFs ()

    let state =
        loadedBoth fs
        |> setCursor Left 2
        |> toggleSelection Left
        |> setCursor Left 3
        |> toggleSelection Left

    fs.File.Delete(at "start/setup.fs")

    let result = update fs ExternalChangeDetected state

    Assert.Equal<Set<EntryName>>(Set.singleton (en "notes.txt"), result.State.Left.Selected)

[<Fact>]
let ``a panel whose directory vanished stands in the nearest one still there`` () =
    let fs = mockFs ()
    let state = loadedBoth fs
    fs.Directory.Delete(at "start", true)

    let result = update fs ExternalChangeDetected state

    Assert.Equal(OnDisk(fp (at "")), result.State.Left.Path)
    Assert.Equal(0, result.State.Left.Cursor)

[<Fact>]
let ``a panel walked out of a vanished directory reports nothing and moves the watch`` () =
    let fs = mockFs ()
    let state = loadedBoth fs
    fs.Directory.Delete(at "start", true)

    let result = update fs ExternalChangeDetected state

    Assert.Equal<Effect list>([ WatchLocations [ fp (at ""); fp (at "backup") ] ], result.Effects)

[<WindowsFact>]
let ``a panel whose drive is gone stands on a root that is there`` () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start"
    let state = create (fp @"D:\work")

    let result = update fs ExternalChangeDetected state

    Assert.Equal(OnDisk(fp @"C:\"), result.State.Left.Path)
    Assert.Equal(OnDisk(fp @"C:\"), result.State.Right.Path)

[<Fact>]
let ``a panel whose directory vanished does not stop the other from being read`` () =
    let fs = mockFs ()
    let state = loadedBoth fs
    fs.Directory.Delete(at "start", true)
    fs.AddFile(at "backup/kept.txt", MockFileData "new")

    let result = update fs ExternalChangeDetected state

    Assert.Contains("kept.txt", names result.State.Right.Entries)

[<Fact>]
let ``an external change leaves the tabs in the background alone`` () =
    let fs = mockFs ()
    let state = (update fs TabOpened (loadedBoth fs)).State
    fs.AddFile(at "start/added.txt", MockFileData "new")

    let result = update fs ExternalChangeDetected state

    let background = result.State.Tabs.Before |> List.head

    Assert.Contains("added.txt", names result.State.Left.Entries)
    Assert.DoesNotContain("added.txt", names background.Left.Entries)
