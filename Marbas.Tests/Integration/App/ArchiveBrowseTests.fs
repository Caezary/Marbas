module Marbas.Tests.Integration.App.ArchiveBrowseTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Terminal.Gui.Views
open Marbas.FileSystem
open Marbas.Ui.Keys
open Marbas.Ui.Panels
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private fp = FileSystemPath.create

let private request path =
    { Path = fp path
      Format = ArchiveFormat.ofKind Zip |> Expect.some
      Compression = Normal
      Password = None }

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start")
    fs.AddFile(at "work/notes.txt", MockFileData "notes")
    fs.AddFile(at "work/docs/inner.txt", MockFileData "inner")

    Archive.write
        fs
        Operations.unattended
        (request (at "start/backup.zip"))
        [ fp (at "work/notes.txt"); fp (at "work/docs") ]
    |> ignore

    fs

let private rows (list: ListView) =
    let source = list.Source.ToList()
    [ for i in 0 .. source.Count - 1 -> string source[i] ]

let private named (list: ListView) =
    rows list |> List.map (fun row -> row.Split(' ') |> Array.head)

[<Fact>]
let ``Enter on an archive browses it in the panel it was pressed in`` () =
    use window = focusedWindow (Fakes.dependencies (mockFs ()) (at "start"))
    let list = leftList window
    list.SelectedItem <- 1

    press list Key.Enter

    Assert.Equal(at "start/backup.zip", shownPath FilePanel.LeftId window)
    Assert.Equal<string list>([ ".."; "docs"; "notes.txt" ], named list)

[<Fact>]
let ``Enter on the way out of an archive comes back to the directory holding it`` () =
    use window = focusedWindow (Fakes.dependencies (mockFs ()) (at "start"))
    let list = leftList window
    list.SelectedItem <- 1
    press list Key.Enter

    press list Key.Enter

    Assert.Equal(at "start", shownPath FilePanel.LeftId window)
    Assert.Equal(System.Nullable 1, list.SelectedItem)
    Assert.Equal<string list>([ ".."; "backup.zip" ], named list)

[<Fact>]
let ``F5 inside an archive copies the entry under the cursor into the other panel`` () =
    let fs = mockFs ()
    fs.AddDirectory(at "backup")

    use window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                Prompts.ChooseDrive = Fakes.choosingDrive (at "backup")
                Prompts.AskOverwrite = fun _ -> Operations.Yes }

    let list = leftList window
    press list KeyMap.rightRootKey
    list.SelectedItem <- 1
    press list Key.Enter
    list.SelectedItem <- 2

    press list Key.F5

    Assert.Equal("notes", fs.File.ReadAllText(at "backup/notes.txt"))
    Assert.Equal<string list>([ ".."; "docs"; "notes.txt" ], named list)

[<Fact>]
let ``F5 inside an archive takes the tree of a marked directory`` () =
    let fs = mockFs ()
    fs.AddDirectory(at "backup")

    use window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                Prompts.ChooseDrive = Fakes.choosingDrive (at "backup")
                Prompts.AskOverwrite = fun _ -> Operations.Yes }

    let list = leftList window
    press list KeyMap.rightRootKey
    list.SelectedItem <- 1
    press list Key.Enter
    list.SelectedItem <- 1
    press list KeyMap.selectionKeys.Head

    press list Key.F5

    Assert.Equal("inner", fs.File.ReadAllText(at "backup/docs/inner.txt"))
