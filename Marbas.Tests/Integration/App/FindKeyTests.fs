module Marbas.Tests.Integration.App.FindKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.Ui.Keys
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs

let private windowAsking asking =
    focusedWindow
        { Fakes.dependencies (mockFs ()) (at "start") with
            AskFind = asking }

let private recording (answers: (MarkedGround * HiddenEntries) list ref) =
    fun ground hidden (_: int option) ->
        answers.Value <- answers.Value @ [ ground, hidden ]
        None

[<Fact>]
let ``Alt+F7 asks what to look for`` () =
    let asked = ref []
    use window = windowAsking (recording asked)

    pressKey window KeyMap.findKey

    Assert.Equal<(MarkedGround * HiddenEntries) list>([ NoDirectoryMarked, Concealed ], asked.Value)

[<Fact>]
let ``Alt+Shift+F asks the same where a terminal keeps Alt+F7 for itself`` () =
    let asked = ref []
    use window = windowAsking (recording asked)

    pressKey window KeyMap.spareFindKey

    Assert.Equal<(MarkedGround * HiddenEntries) list>([ NoDirectoryMarked, Concealed ], asked.Value)

[<Fact>]
let ``a marked directory offers the scope that walks the marks`` () =
    let asked = ref []
    use window = windowAsking (recording asked)
    (leftList window).SelectedItem <- 1
    pressKey window KeyMap.insertKey

    pressKey window KeyMap.findKey

    Assert.Equal<(MarkedGround * HiddenEntries) list>([ DirectoriesMarked, Concealed ], asked.Value)

[<Fact>]
let ``a panel showing its hidden entries offers to search them`` () =
    let asked = ref []
    use window = windowAsking (recording asked)
    pressKey window KeyMap.hiddenKey

    pressKey window KeyMap.findKey

    Assert.Equal<(MarkedGround * HiddenEntries) list>([ NoDirectoryMarked, Shown ], asked.Value)
