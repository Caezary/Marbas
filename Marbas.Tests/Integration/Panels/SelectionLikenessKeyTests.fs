module Marbas.Tests.Integration.Panels.SelectionLikenessKeyTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Views
open Marbas.Ui.Keys
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "start/notes.md", MockFileData "hi")
    fs.AddFile(at "start/readme.TXT", MockFileData "hi")
    fs

let private window () =
    focusedWindow (Fakes.dependencies (mockFs ()) (at "start"))

let private markedRows (list: ListView) =
    let source = Expect.notNull list.Source

    [ for i in 0 .. source.Count - 1 do
          if source.IsMarked i then
              (string (source.ToList()[i])).Split(' ').[0] ]

let private standingOn (list: ListView) (name: string) =
    let source = Expect.notNull list.Source

    list.SelectedItem <-
        [ 0 .. source.Count - 1 ]
        |> List.findIndex (fun i -> (string (source.ToList()[i])).StartsWith(name, StringComparison.Ordinal))

[<Fact>]
let ``Alt+E marks the entries with the extension under the cursor`` () =
    use window = window ()
    let list = leftList window
    standingOn list "notes.txt"

    press list KeyMap.markExtensionKey

    Assert.Equal<string list>([ "notes.txt"; "readme.TXT" ], markedRows list)

[<Fact>]
let ``Alt+Shift+N unmarks the entries with the name under the cursor`` () =
    use window = window ()
    let list = leftList window
    standingOn list "notes.txt"
    press list KeyMap.markExtensionKey
    standingOn list "notes.md"
    press list KeyMap.markStemKey

    press list KeyMap.unmarkStemKey

    Assert.Equal<string list>([ "readme.TXT" ], markedRows list)
