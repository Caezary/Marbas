module Marbas.Tests.Integration.Panels.SelectionInversionKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Terminal.Gui.Views
open Marbas.Ui.Keys
open Marbas.Ui.Panels
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "start/readme.txt", MockFileData "hi")
    fs

let private window () = focusedWindow (Fakes.dependencies (mockFs ()) (at "start"))

let private markedRows (list: ListView) =
    [ for i in 0 .. list.Source.Count - 1 do
          if list.Source.IsMarked i then
              (string (list.Source.ToList()[i])).Split(' ').[0] ]

let private footerOf window = (FilePanel.footer (panel FilePanel.LeftId window)).Text

[<Fact>]
let ``a bare star inverts the marks instead of opening the search`` () =
    use window = window ()
    let list = leftList window
    list.SelectedItem <- 2
    press list KeyMap.insertKey

    press list (Key '*')

    Assert.Equal<string list>([ "docs"; "readme.txt" ], markedRows list)
    Assert.DoesNotContain("Search", footerOf window)

[<Fact>]
let ``Alt+A marks the files and Alt+Shift+A takes them off again`` () =
    use window = window ()
    let list = leftList window
    press list KeyMap.markFilesKey
    let marked = markedRows list

    press list KeyMap.unmarkFilesKey

    Assert.Equal<string list>([ "notes.txt"; "readme.txt" ], marked)
    Assert.Empty(markedRows list)
