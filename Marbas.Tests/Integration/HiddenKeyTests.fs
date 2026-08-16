module Marbas.Tests.Integration.HiddenKeyTests

open System.IO
open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Views
open Marbas.Ui
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private hiddenFile content =
    let data = MockFileData(content: string)
    data.Attributes <- FileAttributes.Hidden
    data

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\start\notes.txt", MockFileData "x")
    fs.AddFile(@"C:\start\.config", MockFileData "x")
    fs.AddFile(@"C:\start\vault.dat", hiddenFile "x")
    fs

let private names (list: ListView) =
    let source = list.Source.ToList()

    [ for i in 0 .. source.Count - 1 -> (string source[i]).Split(' ').[0] ]

[<Fact>]
let ``Alt+H brings the hidden entries into the focused panel`` () =
    let fs = mockFs ()
    use window = focusedWindow (Fakes.dependencies fs @"C:\start")
    let list = leftList window
    let before = names list

    press list KeyMap.hiddenKey

    Assert.Equal<string list>([ ".."; "notes.txt" ], before)
    Assert.Equal<string list>([ ".."; ".config"; "notes.txt"; "vault.dat" ], names list)

[<Fact>]
let ``pressing the key again takes them back out`` () =
    let fs = mockFs ()
    use window = focusedWindow (Fakes.dependencies fs @"C:\start")
    let list = leftList window
    press list KeyMap.hiddenKey

    press list KeyMap.hiddenKey

    Assert.Equal<string list>([ ".."; "notes.txt" ], names list)

[<Fact>]
let ``the classic key does the same where the terminal delivers it`` () =
    let fs = mockFs ()
    use window = focusedWindow (Fakes.dependencies fs @"C:\start")
    let list = leftList window

    press list KeyMap.classicHiddenKey

    Assert.Equal<string list>([ ".."; ".config"; "notes.txt"; "vault.dat" ], names list)

[<Fact>]
let ``the panel title says the hidden entries are on show`` () =
    let fs = mockFs ()
    use window = focusedWindow (Fakes.dependencies fs @"C:\start")
    let list = leftList window

    press list KeyMap.hiddenKey

    Assert.Equal(@"C:\start [name ↑ hidden]", panelTitle FilePanel.LeftId window)

[<Fact>]
let ``the other panel is left alone`` () =
    let fs = mockFs ()
    use window = focusedWindow (Fakes.dependencies fs @"C:\start")
    let list = leftList window

    press list KeyMap.hiddenKey

    Assert.Equal<string list>([ ".."; "notes.txt" ], names (rightList window))
    Assert.Equal(@"C:\start [name ↑]", panelTitle FilePanel.RightId window)
