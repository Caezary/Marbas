module Marbas.Tests.Integration.Panels.DriveSelectionTests

open Xunit
open Marbas.FileSystem
open Marbas.Ui.Keys
open Marbas.Ui.Panels
open Marbas.Ui.App
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private fp = FileSystemPath.create

let private chooserFs () =
    let fs = Fakes.MountedFileSystem [ Fakes.Mount.ext4 (at "") ]
    fs.AddDirectory(at "start")
    fs.AddDirectory(at "elsewhere")
    fs

let private windowWith (fs: Fakes.MountedFileSystem) chooseDrive reportError =
    focusedWindow
        { Fakes.dependencies fs (at "start") with
            Prompts.ReportError = reportError
            Prompts.ChooseDrive = chooseDrive }

let private silent = ignore<Ports.ErrorReport>

let private offeredRoots (offered: ResizeArray<DriveRow list>) =
    offered |> Seq.map (List.map (DriveRow.root >> FileSystemPath.value)) |> List.ofSeq

[<Fact>]
let ``alt+F1 opens the chosen root in the left panel`` () =
    use window = windowWith (chooserFs ()) (Fakes.choosingDrive (at "elsewhere")) silent

    press (leftList window) KeyMap.leftRootKey

    Assert.Equal(at "elsewhere", shownPath FilePanel.LeftId window)
    Assert.Equal(at "start", shownPath FilePanel.RightId window)

[<Fact>]
let ``alt+F2 opens the chosen root in the right panel`` () =
    use window = windowWith (chooserFs ()) (Fakes.choosingDrive (at "elsewhere")) silent

    press (leftList window) KeyMap.rightRootKey

    Assert.Equal(at "elsewhere", shownPath FilePanel.RightId window)
    Assert.Equal(at "start", shownPath FilePanel.LeftId window)

[<Fact>]
let ``the chooser is offered every mounted drive`` () =
    let fs = chooserFs ()
    fs.Plug(Fakes.Mount.ext4 (at "media/stick"))
    let offered = ResizeArray<DriveRow list>()

    use window =
        windowWith
            fs
            (fun drives ->
                offered.Add drives
                None)
            silent

    press (leftList window) KeyMap.leftRootKey

    Assert.Equal<string list list>([ [ at ""; at "media/stick" ] ], offeredRoots offered)

[<Fact>]
let ``a drive plugged in between two openings is offered by the second`` () =
    let fs = chooserFs ()
    let offered = ResizeArray<DriveRow list>()

    use window =
        windowWith
            fs
            (fun drives ->
                offered.Add drives
                None)
            silent

    press (leftList window) KeyMap.leftRootKey
    fs.Plug(Fakes.Mount.ext4 (at "media/stick"))
    press (leftList window) KeyMap.rightRootKey

    Assert.Equal<string list list>([ [ at "" ]; [ at ""; at "media/stick" ] ], offeredRoots offered)

[<Fact>]
let ``a drive handed over opens outside and moves neither panel`` () =
    let fs = chooserFs ()
    fs.Plug(Fakes.Mount.ext4 (at "media/stick"))
    fs.AddDirectory(at "media/stick")
    let started = ResizeArray<LaunchRequest>()
    let stick = MountedDrive(Fakes.driveAt (at "media/stick"))

    use window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                Prompts.ChooseDrive = fun _ -> Some(HandOver stick)
                Processes.Launch = fun request -> started.Add request; Ok() }

    press (leftList window) KeyMap.rightRootKey

    Assert.Equal(fp (at "media/stick"), (Assert.Single started).Target)
    Assert.Equal(at "start", shownPath FilePanel.LeftId window)
    Assert.Equal(at "start", shownPath FilePanel.RightId window)

[<Fact>]
let ``cancelling the chooser leaves both panels untouched`` () =
    use window = windowWith (chooserFs ()) (fun _ -> None) silent

    press (leftList window) KeyMap.leftRootKey

    Assert.Equal(at "start", shownPath FilePanel.LeftId window)
    Assert.Equal(at "start", shownPath FilePanel.RightId window)

[<Fact>]
let ``a root that cannot be read is reported and the panel stays put`` () =
    let reported = ResizeArray<string>()
    use window = windowWith (chooserFs ()) (Fakes.choosingDrive (at "gone")) (fun report -> reported.Add report.Message)

    press (leftList window) KeyMap.leftRootKey

    Assert.Contains(at "gone", Assert.Single reported)
    Assert.Equal(at "start", shownPath FilePanel.LeftId window)
