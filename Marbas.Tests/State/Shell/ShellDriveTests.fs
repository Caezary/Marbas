module Marbas.Tests.State.Shell.ShellDriveTests

open Xunit
open Marbas.FileSystem
open Marbas.Domain
open Marbas.State
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.Shell.ShellFixture

let private at = Root.at

let private fp = FileSystemPath.create

[<Fact>]
let ``a chosen drive is dispatched with the side it was asked for`` () =
    let sent, dispatch = capture ()

    let deps =
        { noDeps () with
            Prompts.ChooseDrive = Fakes.choosingDrive (at "elsewhere") }

    Shell.interpret deps dispatch (PromptForRoot Right)

    Assert.Equal<Message list>([ DriveChosen(Right, MountedDrive(Fakes.driveAt (at "elsewhere"))) ], List.ofSeq sent)

[<Fact>]
let ``a drive handed over is dispatched without a side`` () =
    let sent, dispatch = capture ()
    let stick = MountedDrive(Fakes.driveAt (at "media/stick"))

    let deps =
        { noDeps () with
            Prompts.ChooseDrive = fun _ -> Some(HandOver stick) }

    Shell.interpret deps dispatch (PromptForRoot Left)

    Assert.Equal<Message list>([ DriveHandedOver stick ], List.ofSeq sent)

[<Fact>]
let ``a chosen home directory is dispatched as it was chosen`` () =
    let sent, dispatch = capture ()

    let deps =
        { noDeps () with
            Prompts.ChooseDrive = fun _ -> Some(StandOn(HomeDirectory(fp (at "home/user")))) }

    Shell.interpret deps dispatch (PromptForRoot Left)

    Assert.Equal<Message list>([ DriveChosen(Left, HomeDirectory(fp (at "home/user"))) ], List.ofSeq sent)

[<Fact>]
let ``the chooser is told the side of the panel it was asked for`` () =
    let _, dispatch = capture ()
    let asked = ResizeArray<PanelSide>()

    let deps =
        { noDeps () with
            Prompts.ChooseDrive =
                fun request ->
                    asked.Add request.Side
                    None }

    Shell.interpret deps dispatch (PromptForRoot Left)
    Shell.interpret deps dispatch (PromptForRoot Right)

    Assert.Equal<PanelSide list>([ Left; Right ], List.ofSeq asked)

[<Fact>]
let ``the chooser is offered every drive row as it was listed`` () =
    let _, dispatch = capture ()
    let mutable offered = []

    let stick =
        { Root = fp (at "media/stick")
          Label = Some "STICK"
          Kind = Removable
          Size = 64L
          Free = 16L }

    let rows = [ MountedDrive stick; HomeDirectory(fp (at "home/user")) ]

    let deps =
        { noDeps () with
            Processes.ListDrives = fun () -> rows
            Prompts.ChooseDrive =
                fun offering ->
                    offered <- offering.Drives
                    None }

    Shell.interpret deps dispatch (PromptForRoot Left)

    Assert.Equal<DriveRow list>(rows, offered)

[<Fact>]
let ``closing the chooser dispatches nothing`` () =
    let sent, dispatch = capture ()

    Shell.interpret (noDeps ()) dispatch (PromptForRoot Left)

    Assert.Empty sent

[<Fact>]
let ``the drives are listed again each time the chooser opens`` () =
    let _, dispatch = capture ()
    let mutable listings = 0

    let deps =
        { noDeps () with
            Processes.ListDrives =
                fun () ->
                    listings <- listings + 1
                    [] }

    Shell.interpret deps dispatch (PromptForRoot Left)
    Shell.interpret deps dispatch (PromptForRoot Right)

    Assert.Equal(2, listings)
