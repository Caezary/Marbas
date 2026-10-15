module Marbas.Tests.Integration.Dialogs.LinkDialogTests

open System
open System.Drawing
open Xunit
open Terminal.Gui.Input
open Terminal.Gui.Views
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Messages
open Marbas.Ui.Theme
open Marbas.Ui.Dialogs
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

let private laidOutWith remembered refusal =
    DialogStyle.apply ()

    let dialog =
        LinkDialog.create [ Symbolic; Hard; Junction ] refusal remembered suggestion

    dialog.BeginInit()
    dialog.EndInit()
    dialog.Layout(Size(80, 24)) |> ignore
    dialog

let private laidOut refusal = laidOutWith [] refusal

let private target dialog = LinkDialog.field dialog

let private kinds dialog = LinkDialog.selector dialog

let private enabled dialog =
    (kinds dialog).SubViews |> Seq.map _.Enabled |> List.ofSeq

let private reason dialog = (LinkDialog.reason dialog).Text

let private accepted (dialog: LinkDialog.LinkView) =
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

    Assert.Equal<string list>(
        [ "_Symbolic link"; "_Hard link"; "_Junction" ],
        List.ofSeq (Expect.notNull (kinds dialog).Labels)
    )

    Assert.Equal<bool list>([ true; true; true ], enabled dialog)
    Assert.Equal("", reason dialog)

[<Fact>]
let ``the dialog keeps the kinds it was built from`` () =
    DialogStyle.apply ()

    use dialog = LinkDialog.create [ Junction; Symbolic ] nothingRefused [] suggestion

    Assert.Equal<LinkKind list>([ Junction; Symbolic ], dialog.Kinds)

[<Fact>]
let ``only the kinds this system can make are offered`` () =
    DialogStyle.apply ()
    use dialog = LinkDialog.create [ Symbolic; Hard ] nothingRefused [] suggestion
    dialog.BeginInit()
    dialog.EndInit()
    (LinkDialog.selector dialog).Value <- Nullable 1
    dialog.Result <- 1

    let choice = LinkDialog.choice dialog |> Expect.some

    Assert.Equal<string list>([ "_Symbolic link"; "_Hard link" ], List.ofSeq (Expect.notNull (kinds dialog).Labels))
    Assert.Equal(Hard, choice.Kind)

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

    Assert.Equal(
        { Target = Root.at "backup/twin.txt"
          Kind = Junction },
        choice
    )

[<Fact>]
let ``a cancelled dialog answers nothing`` () =
    use dialog = laidOut nothingRefused

    let choice = LinkDialog.choice dialog

    Assert.Equal(None, choice)

[<Fact>]
let ``the places linked at before are offered as the list of the target field`` () =
    use dialog = laidOutWith [ @"D:\links\one"; @"D:\links\two" ] nothingRefused

    let offered =
        [ for item in (Expect.notNull (target dialog :?> DropDownList).Source).ToList() -> string item ]

    Assert.Equal<string list>([ @"D:\links\one"; @"D:\links\two" ], offered)

[<Theory>]
[<InlineData("CursorUp")>]
[<InlineData("CursorDown")>]
let ``the arrows are left to move between the fields of the link popup`` (spelled: string) =
    use dialog = laidOut nothingRefused

    let taken = (target dialog).KeyBindings.TryGet(Key.TryParse spelled |> snd) |> fst

    Assert.False taken
