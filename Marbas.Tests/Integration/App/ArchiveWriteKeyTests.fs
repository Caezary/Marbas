module Marbas.Tests.Integration.App.ArchiveWriteKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.Archives
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private fp = FileSystemPath.create

let private archive = fp (at "start/backup.zip")

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start")
    fs.AddFile(at "work/notes.txt", MockFileData "notes")
    fs.AddFile(at "work/docs/inner.txt", MockFileData "inner")

    Archive.write
        fs
        Operations.unattended
        { Path = archive
          Format = ArchiveFormat.tryCreate Zip |> Expect.some
          Compression = Normal
          Password = None }
        [ fp (at "work/notes.txt"); fp (at "work/docs") ]
    |> ignore

    fs.AddFile(at "start/loose.txt", MockFileData "loose")
    fs

let private windowOn (fs: MockFileSystem) =
    focusedWindow
        { Fakes.dependencies fs (at "start") with
            Prompts.AskOverwrite = fun _ -> Operations.Yes
            Prompts.Confirm = fun _ -> true
            Prompts.AskName = fun _ -> Some "fresh" }

let private rows (list: ListView) =
    let source = (Expect.notNull list.Source).ToList()
    [ for i in 0 .. source.Count - 1 -> string source[i] ]

let private named (list: ListView) =
    rows list |> List.map (fun row -> row.Split(' ') |> Array.head)

let private entries (fs: MockFileSystem) =
    ArchiveListing.read fs archive ArchivePath.root
    |> Expect.ok
    |> List.map (_.Name >> EntryName.value)
    |> List.sort

let private advanceFocus (window: Window) =
    window.AdvanceFocus(NavigationDirection.Forward, System.Nullable TabBehavior.TabStop)
    |> ignore

let private rightInsideArchive window =
    advanceFocus window
    let right = rightList window
    right.SelectedItem <- 1
    press right Key.Enter
    advanceFocus window

[<Fact>]
let ``F5 into a panel inside an archive puts the file in the archive`` () =
    let fs = mockFs ()
    use window = windowOn fs
    rightInsideArchive window
    let left = leftList window
    left.SelectedItem <- 2

    press left Key.F5

    Assert.Equal<string list>([ "docs"; "loose.txt"; "notes.txt" ], entries fs)
    Assert.Equal<string list>([ ".."; "docs"; "loose.txt"; "notes.txt" ], named (rightList window))

[<Fact>]
let ``F6 into a panel inside an archive leaves the source in one place only`` () =
    let fs = mockFs ()
    use window = windowOn fs
    rightInsideArchive window
    let left = leftList window
    left.SelectedItem <- 2

    press left Key.F6

    Assert.Contains("loose.txt", entries fs)
    Assert.False(fs.File.Exists(at "start/loose.txt"))
    Assert.Equal<string list>([ ".."; "backup.zip" ], named left)

[<Fact>]
let ``F6 out of a panel inside an archive leaves the entry in one place only`` () =
    let fs = mockFs ()
    use window = windowOn fs
    let left = leftList window
    left.SelectedItem <- 1
    press left Key.Enter
    left.SelectedItem <- 2

    press left Key.F6

    Assert.Equal("notes", fs.File.ReadAllText(at "start/notes.txt"))
    Assert.Equal<string list>([ "docs" ], entries fs)
    Assert.Equal<string list>([ ".."; "docs" ], named left)

[<Fact>]
let ``F8 inside an archive removes the entry from it`` () =
    let fs = mockFs ()
    use window = windowOn fs
    let left = leftList window
    left.SelectedItem <- 1
    press left Key.Enter
    left.SelectedItem <- 2

    press left Key.F8

    Assert.Equal<string list>([ "docs" ], entries fs)
    Assert.Equal<string list>([ ".."; "docs" ], named left)

[<Fact>]
let ``F7 inside an archive makes a directory entry in it`` () =
    let fs = mockFs ()
    use window = windowOn fs
    let left = leftList window
    left.SelectedItem <- 1
    press left Key.Enter

    press left Key.F7

    Assert.Equal<string list>([ "docs"; "fresh"; "notes.txt" ], entries fs)
    Assert.Equal<string list>([ ".."; "docs"; "fresh"; "notes.txt" ], named left)

let private other = fp (at "start/other.zip")

let private twoArchives () =
    let fs = mockFs ()

    Archive.write
        fs
        Operations.unattended
        { Path = other
          Format = ArchiveFormat.tryCreate Zip |> Expect.some
          Compression = Normal
          Password = None }
        [ fp (at "start/loose.txt") ]
    |> ignore

    fs

let private otherEntries (fs: MockFileSystem) =
    ArchiveListing.read fs other ArchivePath.root
    |> Expect.ok
    |> List.map (_.Name >> EntryName.value)
    |> List.sort

let private bothInsideArchives window =
    advanceFocus window
    let right = rightList window
    right.SelectedItem <- 3
    press right Key.Enter
    advanceFocus window
    let left = leftList window
    left.SelectedItem <- 1
    press left Key.Enter
    left.SelectedItem <- 2
    left

[<Fact>]
let ``F5 between two panels inside archives carries the entry across`` () =
    let fs = twoArchives ()
    use window = windowOn fs
    let left = bothInsideArchives window

    press left Key.F5

    Assert.Equal<string list>([ "loose.txt"; "notes.txt" ], otherEntries fs)
    Assert.Equal<string list>([ "docs"; "notes.txt" ], entries fs)
    Assert.Equal<string list>([ ".."; "loose.txt"; "notes.txt" ], named (rightList window))

[<Fact>]
let ``F6 between two panels inside archives leaves the entry in one place only`` () =
    let fs = twoArchives ()
    use window = windowOn fs
    let left = bothInsideArchives window

    press left Key.F6

    Assert.Equal<string list>([ "loose.txt"; "notes.txt" ], otherEntries fs)
    Assert.Equal<string list>([ "docs" ], entries fs)
    Assert.Equal<string list>([ ".."; "docs" ], named left)
