module Marbas.Tests.Integration.App.TabKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Collections
open Marbas.Ui.Keys
open Marbas.Ui.Panels
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes.txt", MockFileData "x")
    fs.AddDirectory(at "backup")
    fs

[<Fact>]
let ``the tabs key asks which tab to stand on and goes there`` () =
    let fs = mockFs ()

    use window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                ChooseOption = fun _ _ -> Some(at "backup")
                ChooseTab = fun _ -> Some 0 }

    press (leftList window) KeyMap.newTabKey
    press (leftList window) KeyMap.leftRootKey
    press (leftList window) KeyMap.tabsKey

    Assert.Equal(at "start", shownPath FilePanel.LeftId window)

[<Fact>]
let ``the spare tabs key asks the same question`` () =
    let fs = mockFs ()
    let asked = ResizeArray<int>()

    use window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                ChooseTab =
                    fun tabs ->
                        asked.Add(Zipper.count tabs)
                        None }

    press (leftList window) KeyMap.spareTabsKey

    Assert.Equal<int list>([ 1 ], List.ofSeq asked)

[<Fact>]
let ``a tab the popup did not pick leaves the panels alone`` () =
    let fs = mockFs ()

    use window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                ChooseOption = fun _ _ -> Some(at "backup")
                ChooseTab = fun _ -> None }

    press (leftList window) KeyMap.newTabKey
    press (leftList window) KeyMap.leftRootKey
    press (leftList window) KeyMap.tabsKey

    Assert.Equal(at "backup", shownPath FilePanel.LeftId window)

[<Fact>]
let ``Alt and an arrow walk round the tabs without a popup`` () =
    let fs = mockFs ()

    use window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                ChooseOption = fun _ _ -> Some(at "backup") }

    press (leftList window) KeyMap.newTabKey
    press (leftList window) KeyMap.leftRootKey
    press (leftList window) KeyMap.nextTabKey

    Assert.Equal(at "start", shownPath FilePanel.LeftId window)

    press (leftList window) KeyMap.previousTabKey

    Assert.Equal(at "backup", shownPath FilePanel.LeftId window)

[<Fact>]
let ``a tab is read again when it is stood on`` () =
    let fs = mockFs ()

    use window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                ChooseTab = fun _ -> Some 0 }

    press (leftList window) KeyMap.newTabKey
    fs.AddFile(at "start/added.txt", MockFileData "new")

    press (leftList window) KeyMap.tabsKey

    let source = (leftList window).Source.ToList()
    let rows = [ for index in 0 .. source.Count - 1 -> string source[index] ]

    Assert.Contains(rows, fun (row: string) -> row.StartsWith "added.txt")
