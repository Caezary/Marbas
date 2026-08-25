module Marbas.Tests.Integration.Panels.DriveSelectionTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Ui.Keys
open Marbas.Ui.Panels
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private chooserFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start")
    fs.AddDirectory(at "elsewhere")
    fs

let private windowWith (fs: MockFileSystem) chooseOption reportError =
    focusedWindow
        { Fakes.dependencies fs (at "start") with
            ReportError = reportError
            ChooseOption = chooseOption }

let private silent = fun (_: string) (_: string) -> ()

[<Fact>]
let ``alt+F1 opens the chosen root in the left panel`` () =
    use window = windowWith (chooserFs ()) (fun _ _ -> Some(at "elsewhere")) silent

    press (leftList window) KeyMap.leftRootKey

    Assert.Equal(at "elsewhere", shownPath FilePanel.LeftId window)
    Assert.Equal(at "start", shownPath FilePanel.RightId window)

[<Fact>]
let ``alt+F2 opens the chosen root in the right panel`` () =
    use window = windowWith (chooserFs ()) (fun _ _ -> Some(at "elsewhere")) silent

    press (leftList window) KeyMap.rightRootKey

    Assert.Equal(at "elsewhere", shownPath FilePanel.RightId window)
    Assert.Equal(at "start", shownPath FilePanel.LeftId window)

[<WindowsFact>]
let ``the chooser is offered every mounted drive`` () =
    let fs = chooserFs ()
    fs.AddDirectory @"D:\data"
    let offered = ResizeArray<string list>()

    use window =
        windowWith
            fs
            (fun _ drives ->
                offered.Add drives
                None)
            silent

    press (leftList window) KeyMap.leftRootKey

    Assert.Equal<string list>([ @"C:\"; @"D:\" ], Assert.Single offered)

[<Fact>]
let ``cancelling the chooser leaves both panels untouched`` () =
    use window = windowWith (chooserFs ()) (fun _ _ -> None) silent

    press (leftList window) KeyMap.leftRootKey

    Assert.Equal(at "start", shownPath FilePanel.LeftId window)

[<Fact>]
let ``a root that cannot be read is reported and the panel stays put`` () =
    let reported = ResizeArray<string>()
    use window = windowWith (chooserFs ()) (fun _ _ -> Some(at "gone")) (fun _ -> reported.Add)

    press (leftList window) KeyMap.leftRootKey

    Assert.Contains(at "gone", Assert.Single reported)
    Assert.Equal(at "start", shownPath FilePanel.LeftId window)
