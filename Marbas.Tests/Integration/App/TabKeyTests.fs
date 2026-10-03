module Marbas.Tests.Integration.App.TabKeyTests

open System.IO.Abstractions.TestingHelpers
open Terminal.Gui.Input
open Xunit
open Marbas.Collections
open Marbas.Ui.Keys
open Marbas.Ui.App
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
                Prompts.ChooseDrive = Fakes.choosingDrive (at "backup")
                Prompts.ChooseTab = fun _ -> Some 0 }

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
                Prompts.ChooseTab =
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
                Prompts.ChooseDrive = Fakes.choosingDrive (at "backup")
                Prompts.ChooseTab = fun _ -> None }

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
                Prompts.ChooseDrive = Fakes.choosingDrive (at "backup") }

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
                Prompts.ChooseTab = fun _ -> Some 0 }

    press (leftList window) KeyMap.newTabKey
    fs.AddFile(at "start/added.txt", MockFileData "new")

    press (leftList window) KeyMap.tabsKey

    let source = (leftList window).Source.ToList()
    let rows = [ for index in 0 .. source.Count - 1 -> string source[index] ]

    Assert.Contains(rows, fun (row: string) -> row.StartsWith "added.txt")

[<Fact>]
let ``the corner says which tab is in front and how many there are`` () =
    let fs = mockFs ()
    use window = focusedWindow (Fakes.dependencies fs (at "start"))
    let counter () = (panel TabCounter.Id window).Text

    Assert.Equal("[1/1]", counter ())

    press (leftList window) KeyMap.newTabKey
    press (leftList window) KeyMap.newTabKey

    Assert.Equal("[3/3]", counter ())

    press (leftList window) KeyMap.previousTabKey

    Assert.Equal("[2/3]", counter ())

    press (leftList window) KeyMap.closeTabKey

    Assert.Equal("[2/2]", counter ())

[<Fact>]
let ``walking to a tab puts the focus on the panel that tab left active`` () =
    let fs = mockFs ()
    use window = focusedWindow (Fakes.dependencies fs (at "start"))
    press (leftList window) KeyMap.newTabKey
    (panel FilePanel.RightId window).SetFocus() |> ignore

    press (rightList window) KeyMap.previousTabKey

    Assert.True((panel FilePanel.LeftId window).HasFocus)

[<Fact>]
let ``walking to a tab takes the focus off the panel the previous tab left active`` () =
    let fs = mockFs ()
    use window = focusedWindow (Fakes.dependencies fs (at "start"))
    press (leftList window) KeyMap.newTabKey
    (panel FilePanel.RightId window).SetFocus() |> ignore

    press (rightList window) KeyMap.previousTabKey

    Assert.False((panel FilePanel.RightId window).HasFocus)

[<Fact>]
let ``closing a tab puts the focus on the panel the remaining tab left active`` () =
    let fs = mockFs ()
    use window = focusedWindow (Fakes.dependencies fs (at "start"))
    press (leftList window) KeyMap.newTabKey
    (panel FilePanel.RightId window).SetFocus() |> ignore

    press (rightList window) KeyMap.closeTabKey

    Assert.True((panel FilePanel.LeftId window).HasFocus)

[<Fact>]
let ``the keys after walking to a tab move and open in the panel that tab left active`` () =
    let fs = mockFs ()
    fs.AddDirectory(at "start/inner")
    use window = focusedWindow (Fakes.dependencies fs (at "start"))
    press (leftList window) KeyMap.newTabKey
    (panel FilePanel.RightId window).SetFocus() |> ignore
    press (rightList window) KeyMap.previousTabKey
    pressKey window Key.CursorDown

    pressKey window Key.Enter

    Assert.Equal(at "start/inner", shownPath FilePanel.LeftId window)
