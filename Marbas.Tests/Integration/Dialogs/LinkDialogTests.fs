module Marbas.Tests.Integration.Dialogs.LinkDialogTests

open System
open System.Drawing
open Xunit
open Terminal.Gui.Views
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.Theme
open Marbas.Ui.Dialogs
open Marbas.Ui.Messages
open Marbas.Tests

let private suggestion = Root.at "backup/notes.txt"

let private nothingRefused (_: string) (_: LinkKind) = None

let private hardRefused (_: string) (kind: LinkKind) =
    match kind with
    | Hard -> Some HardLinkToDirectory
    | _ -> None

let private hardRefusedElsewhere (text: string) (kind: LinkKind) =
    match text, kind with
    | "elsewhere", Hard -> Some HardLinkAcrossVolumes
    | _ -> None

let private laidOut refusal =
    DialogStyle.apply ()
    let dialog = LinkDialog.create refusal suggestion
    dialog.BeginInit()
    dialog.EndInit()
    dialog.Layout(Size(80, 24)) |> ignore
    dialog

let private target dialog = LinkDialog.field dialog

let private kinds dialog = LinkDialog.selector dialog

let private enabled dialog =
    (kinds dialog).SubViews |> Seq.map _.Enabled |> List.ofSeq

let private reason dialog = (LinkDialog.reason dialog).Text

let private accepted (dialog: Dialog) =
    dialog.Result <- 1
    LinkDialog.choice dialog |> Expect.some

[<Fact>]
let ``the suggested link is offered ready to be edited, as a symbolic link`` () =
    use dialog = laidOut nothingRefused

    Assert.Equal(suggestion, (target dialog).Text)
    Assert.Equal(Some 0, (kinds dialog).Value |> Option.ofNullable)

[<Fact>]
let ``every kind is offered`` () =
    use dialog = laidOut nothingRefused

    Assert.Equal<string list>([ "_Symbolic link"; "_Hard link"; "_Junction" ], List.ofSeq (kinds dialog).Labels)
    Assert.Equal<bool list>([ true; true; true ], enabled dialog)
    Assert.Equal("", reason dialog)

[<Fact>]
let ``a kind the target cannot carry is disabled with its reason beneath`` () =
    use dialog = laidOut hardRefused

    Assert.Equal<bool list>([ true; false; true ], enabled dialog)
    Assert.Equal(LinkRefusalMessage.describe HardLinkToDirectory, reason dialog)

[<Fact>]
let ``typing another place asks again what each kind can carry`` () =
    use dialog = laidOut hardRefusedElsewhere

    (target dialog).Text <- "elsewhere"

    Assert.Equal<bool list>([ true; false; true ], enabled dialog)
    Assert.Equal(LinkRefusalMessage.describe HardLinkAcrossVolumes, reason dialog)

[<Fact>]
let ``a chosen kind the new place refuses gives way to a symbolic link`` () =
    use dialog = laidOut hardRefusedElsewhere
    (kinds dialog).Value <- Nullable 1

    (target dialog).Text <- "elsewhere"

    Assert.Equal(Some 0, (kinds dialog).Value |> Option.ofNullable)

[<Fact>]
let ``the answer carries what was typed and the kind that was chosen`` () =
    use dialog = laidOut nothingRefused
    (target dialog).Text <- Root.at "backup/twin.txt"
    (kinds dialog).Value <- Nullable 2

    let choice = accepted dialog

    Assert.Equal({ Target = Root.at "backup/twin.txt"; Kind = Junction }, choice)

[<Fact>]
let ``a cancelled dialog answers nothing`` () =
    use dialog = laidOut nothingRefused

    let choice = LinkDialog.choice dialog

    Assert.Equal(None, choice)
