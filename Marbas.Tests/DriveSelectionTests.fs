module Marbas.Tests.DriveSelectionTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Ui
open Marbas.Tests.TestHelpers

let private twoDriveFs () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start"
    fs.AddDirectory @"D:\data"
    fs

let private windowWith (fs: MockFileSystem) chooseOption reportError =
    let window =
        MainWindow.create
            { dependencies fs @"C:\start" with
                ReportError = reportError
                ChooseOption = chooseOption }

    window.BeginInit()
    window.EndInit()
    window.SetFocus() |> ignore
    window

[<Fact>]
let ``alt+F1 opens the chosen drive in the left panel`` () =
    let fs = twoDriveFs ()
    use window = windowWith fs (fun _ _ -> Some @"D:\") (fun _ _ -> ())

    let list = FilePanel.listView (panel FilePanel.LeftId window)
    press list MainWindow.leftDriveKey

    Assert.Equal(@"D:\", (panel FilePanel.LeftId window).Title)
    Assert.Equal(@"C:\start", (panel FilePanel.RightId window).Title)

[<Fact>]
let ``alt+F2 opens the chosen drive in the right panel`` () =
    let fs = twoDriveFs ()
    use window = windowWith fs (fun _ _ -> Some @"D:\") (fun _ _ -> ())

    let list = FilePanel.listView (panel FilePanel.LeftId window)
    press list MainWindow.rightDriveKey

    Assert.Equal(@"D:\", (panel FilePanel.RightId window).Title)
    Assert.Equal(@"C:\start", (panel FilePanel.LeftId window).Title)

[<Fact>]
let ``the chooser is offered every mounted drive`` () =
    let fs = twoDriveFs ()
    let offered = ResizeArray<string list>()

    use window =
        windowWith
            fs
            (fun _ drives ->
                offered.Add drives
                None)
            (fun _ _ -> ())

    let list = FilePanel.listView (panel FilePanel.LeftId window)
    press list MainWindow.leftDriveKey

    Assert.Equal<string list>([ @"C:\"; @"D:\" ], offered |> Seq.exactlyOne)

[<Fact>]
let ``cancelling the chooser leaves both panels untouched`` () =
    let fs = twoDriveFs ()
    use window = windowWith fs (fun _ _ -> None) (fun _ _ -> ())

    let list = FilePanel.listView (panel FilePanel.LeftId window)
    press list MainWindow.leftDriveKey

    Assert.Equal(@"C:\start", (panel FilePanel.LeftId window).Title)

[<Fact>]
let ``an unreadable drive is reported and the panel stays put`` () =
    let fs = twoDriveFs ()
    let reported = ResizeArray<string>()
    use window = windowWith fs (fun _ _ -> Some @"Z:\") (fun _ message -> reported.Add message)

    let list = FilePanel.listView (panel FilePanel.LeftId window)
    press list MainWindow.leftDriveKey

    Assert.Contains(@"Z:\", reported |> Seq.exactlyOne)
    Assert.Equal(@"C:\start", (panel FilePanel.LeftId window).Title)
