module Marbas.Tests.Integration.Dialogs.ViewerSearchDialogTests

open System.Drawing
open Xunit
open Terminal.Gui.Views
open Marbas.Domain
open Marbas.Ui.Theme
open Marbas.Ui.Dialogs
open Marbas.Ui.Viewer

let private typed raw = EnteredText.tryCreate raw |> Option.get

let private wanted raw = FindText.tryCreate raw |> Option.get

let private openedOn (offer: SearchOffer) =
    DialogStyle.apply ()
    let dialog = ViewerSearchDialog.create offer
    dialog.BeginInit()
    dialog.EndInit()
    dialog.Layout(Size(80, 24)) |> ignore
    dialog

let private fresh () =
    openedOn { Remembered = []; Last = None }

let private listed (dialog: Dialog) =
    let field = ViewerSearchDialog.field dialog :?> DropDownList
    [ for item in field.Source.ToList() -> string item ]

let private AcceptingButton = 1

[<Fact>]
let ``the field offers the texts searched before, newest first`` () =
    use dialog =
        openedOn
            { Remembered = [ typed "newer"; typed "older" ]
              Last = None }

    let offered = listed dialog

    Assert.Equal<string list>([ "newer"; "older" ], offered)

[<Fact>]
let ``a first search starts from an empty field`` () =
    use dialog = fresh ()

    let text = (ViewerSearchDialog.field dialog).Text

    Assert.Equal("", text)

[<Fact>]
let ``a search asked again holds the last text`` () =
    use dialog =
        openedOn
            { Remembered = [ typed "older" ]
              Last = Some(wanted "needle") }

    let text = (ViewerSearchDialog.field dialog).Text

    Assert.Equal("needle", text)

[<Fact>]
let ``the four options are there`` () =
    use dialog = fresh ()

    let labels =
        ViewerSearchDialog.boxes
        |> List.map (fun box -> (ViewerSearchDialog.checkBox dialog box).Text)

    Assert.Equal<string list>([ "C_ase sensitive"; "_Whole words"; "_Regular expression"; "_Backwards" ], labels)

[<Fact>]
let ``the options wait for the searches they belong to`` () =
    use dialog = fresh ()

    let enabled =
        ViewerSearchDialog.boxes
        |> List.filter (fun box -> (ViewerSearchDialog.checkBox dialog box).Enabled)

    Assert.Empty enabled

[<Fact>]
let ``everything stays inside the frame`` () =
    use dialog = fresh ()

    let lowest =
        ViewerSearchDialog.boxes
        |> List.map (fun box -> (ViewerSearchDialog.checkBox dialog box).Frame)
        |> List.maxBy _.Bottom

    Assert.True(lowest.Bottom <= dialog.Viewport.Height, $"the box at %A{lowest} is clipped by %A{dialog.Viewport}")
    Assert.True(lowest.Right <= dialog.Viewport.Width, $"the box at %A{lowest} is clipped by %A{dialog.Viewport}")

[<Fact>]
let ``a confirmed popup yields what was typed`` () =
    use dialog = fresh ()
    (ViewerSearchDialog.field dialog).Text <- "needle "
    dialog.Result <- AcceptingButton

    let text = ViewerSearchDialog.text dialog

    Assert.Equal(Some "needle ", text)

[<Fact>]
let ``a popup closed without confirming yields nothing`` () =
    use dialog = fresh ()
    (ViewerSearchDialog.field dialog).Text <- "needle"

    let text = ViewerSearchDialog.text dialog

    Assert.Equal(None, text)
