module Marbas.Tests.Integration.DriveSelectionTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Ui
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private twoDriveFs () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start"
    fs.AddDirectory @"D:\data"
    fs

let private windowWith (fs: MockFileSystem) chooseOption reportError =
    focusedWindow
        { Fakes.dependencies fs @"C:\start" with
            ReportError = reportError
            ChooseOption = chooseOption }

let private silent = fun (_: string) (_: string) -> ()

[<Fact>]
let ``alt+F1 opens the chosen drive in the left panel`` () =
    use window = windowWith (twoDriveFs ()) (fun _ _ -> Some @"D:\") silent

    press (leftList window) PanelInput.leftDriveKey

    Assert.Equal(@"D:\", shownPath FilePanel.LeftId window)
    Assert.Equal(@"C:\start", shownPath FilePanel.RightId window)

[<Fact>]
let ``alt+F2 opens the chosen drive in the right panel`` () =
    use window = windowWith (twoDriveFs ()) (fun _ _ -> Some @"D:\") silent

    press (leftList window) PanelInput.rightDriveKey

    Assert.Equal(@"D:\", shownPath FilePanel.RightId window)
    Assert.Equal(@"C:\start", shownPath FilePanel.LeftId window)

[<Fact>]
let ``the chooser is offered every mounted drive`` () =
    let offered = ResizeArray<string list>()
    use window = windowWith (twoDriveFs ()) (fun _ drives -> offered.Add drives; None) silent

    press (leftList window) PanelInput.leftDriveKey

    Assert.Equal<string list>([ @"C:\"; @"D:\" ], Assert.Single offered)

[<Fact>]
let ``cancelling the chooser leaves both panels untouched`` () =
    use window = windowWith (twoDriveFs ()) (fun _ _ -> None) silent

    press (leftList window) PanelInput.leftDriveKey

    Assert.Equal(@"C:\start", shownPath FilePanel.LeftId window)

[<Fact>]
let ``an unreadable drive is reported and the panel stays put`` () =
    let reported = ResizeArray<string>()
    use window = windowWith (twoDriveFs ()) (fun _ _ -> Some @"Z:\") (fun _ -> reported.Add)

    press (leftList window) PanelInput.leftDriveKey

    Assert.Contains(@"Z:\", Assert.Single reported)
    Assert.Equal(@"C:\start", shownPath FilePanel.LeftId window)
