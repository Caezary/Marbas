module Marbas.Tests.Integration.App.HelpKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Marbas.Guide
open Marbas.Ui.Keys
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private refreshing: GuidePlace =
    { Chapter = ChapterName.tryCreate "02-panels.md" |> Expect.some
      Heading = Some(Anchor.create "reading-the-panel-again") }

let private showingHelp () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "start/todo.txt", MockFileData "hi")
    let shown = ResizeArray<GuidePlace>()

    let window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                Prompts.ShowHelp = shown.Add }

    window, shown

[<Fact>]
let ``F1 before any command opens the help at the contents`` () =
    let window, shown = showingHelp ()
    use window = window

    press (leftList window) Key.F1

    Assert.Equal(GuidePlace.contents, Assert.Single shown)

[<Fact>]
let ``F1 after a command opens the help at the section of that command`` () =
    let window, shown = showingHelp ()
    use window = window
    press (leftList window) KeyMap.refreshKey

    press (leftList window) Key.F1

    Assert.Equal(refreshing, Assert.Single shown)

[<Fact>]
let ``moving the cursor after a command leaves the help at that command`` () =
    let window, shown = showingHelp ()
    use window = window
    press (leftList window) KeyMap.refreshKey
    press (leftList window) Key.CursorDown

    press (leftList window) Key.F1

    Assert.Equal(refreshing, Assert.Single shown)

[<Fact>]
let ``a quick search after a command leaves the help at that command`` () =
    let window, shown = showingHelp ()
    use window = window
    press (leftList window) KeyMap.refreshKey
    press (leftList window) (Key 't')

    press (leftList window) Key.F1

    Assert.Equal(refreshing, Assert.Single shown)

[<Fact>]
let ``F1 pressed twice opens the help at the same place`` () =
    let window, shown = showingHelp ()
    use window = window
    press (leftList window) KeyMap.refreshKey
    press (leftList window) Key.F1

    press (leftList window) Key.F1

    Assert.Equal<GuidePlace list>([ refreshing; refreshing ], List.ofSeq shown)
