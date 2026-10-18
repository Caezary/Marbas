module Marbas.Tests.Integration.Dialogs.MenuEntryDialogTests

open System
open System.Drawing
open Xunit
open Terminal.Gui.Input
open Terminal.Gui.Views
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Profile
open Marbas.Ui.Theme
open Marbas.Ui.Dialogs
open Marbas.Ui.Dialogs.MenuEntryForm
open Marbas.Tests

let private compare =
    { Hotkey = Expect.some (MenuHotkey.tryCreate "C")
      Label = Expect.some (MenuLabel.tryCreate "Compare")
      Line = Expect.some (CommandLine.tryCreate "code --diff %Lp %Rp")
      Run = InShellKeptOpen
      Elevation = AsAdministrator }

let private edit =
    { Hotkey = Expect.some (MenuHotkey.tryCreate "E")
      Label = Expect.some (MenuLabel.tryCreate "Edit")
      Line = Expect.some (CommandLine.tryCreate "vim %p")
      Run = Directly
      Elevation = AsUser }

let private opened prompt =
    DialogStyle.apply ()
    let dialog = MenuEntryDialog.create prompt
    dialog.BeginInit()
    dialog.EndInit()
    dialog.Layout(Size(100, 30)) |> ignore
    dialog

let private adding () =
    opened
        { Editing = Adding
          Others = [ compare ] }

let private fill (dialog: MenuEntryDialog.EntryView) hotkey label line =
    (MenuEntryDialog.field dialog MenuEntryDialog.HotkeyId).Text <- hotkey
    (MenuEntryDialog.field dialog MenuEntryDialog.LabelId).Text <- label
    (MenuEntryDialog.field dialog MenuEntryDialog.LineId).Text <- line

let private confirm (dialog: MenuEntryDialog.EntryView) =
    (Seq.last dialog.Buttons).InvokeCommand Command.Accept |> ignore

let private choosing (selector: OptionSelector) index = selector.Value <- Nullable index

[<Fact>]
let ``a new entry is read from the fields once confirmed`` () =
    use dialog = adding ()
    fill dialog "e" "Edit" "vim %p"
    dialog.Result <- 1

    let answer = MenuEntryDialog.answer dialog

    Assert.Equal(Some(CommandEntered edit), answer)

[<Fact>]
let ``the way of running and the rights are read from their controls`` () =
    use dialog = adding ()
    fill dialog "e" "Edit" "vim %p"
    choosing (MenuEntryDialog.runSelector dialog) 2
    (MenuEntryDialog.administratorBox dialog).Value <- CheckState.Checked
    dialog.Result <- 1

    let answer = MenuEntryDialog.answer dialog

    Assert.Equal(
        Some(
            CommandEntered
                { edit with
                    Run = InShellKeptOpen
                    Elevation = AsAdministrator }
        ),
        answer
    )

[<Fact>]
let ``a hotkey another entry holds is refused in the dialog, which stays open and says why`` () =
    use dialog = adding ()
    fill dialog "c" "Copy" "copy %s"

    confirm dialog

    Assert.False(dialog.Result.HasValue, $"the popup closed with {dialog.Result}")
    Assert.Equal(MenuEntryForm.describe (HotkeyHeld compare), (MenuEntryDialog.reason dialog).Text)

[<Fact>]
let ``an entry with nothing typed is refused`` () =
    use dialog = adding ()

    confirm dialog

    Assert.False(dialog.Result.HasValue, $"the popup closed with {dialog.Result}")
    Assert.Equal(MenuEntryForm.describe NoLabel, (MenuEntryDialog.reason dialog).Text)

[<Theory>]
[<InlineData(1)>]
[<InlineData(2)>]
let ``a line chosen instead needs none of the fields`` (kind: int) =
    use dialog = adding ()
    choosing (MenuEntryDialog.kindSelector dialog) kind
    dialog.Result <- 1

    let answer = MenuEntryDialog.answer dialog

    Assert.Equal(Some(LineEntered(List.item (kind - 1) [ AboveCursor; BelowCursor ])), answer)

[<Fact>]
let ``a line chosen instead is not refused for its empty fields`` () =
    use dialog = adding ()
    choosing (MenuEntryDialog.kindSelector dialog) 2

    confirm dialog

    Assert.Equal("", (MenuEntryDialog.reason dialog).Text)

[<Fact>]
let ``choosing a line greys out the fields of an entry`` () =
    use dialog = adding ()

    choosing (MenuEntryDialog.kindSelector dialog) 1

    Assert.False (MenuEntryDialog.field dialog MenuEntryDialog.LineId).Enabled

[<Fact>]
let ``an entry being changed fills the fields and offers no line`` () =
    use dialog =
        opened
            { Editing = Changing compare
              Others = [ edit ] }

    let fields =
        [ MenuEntryDialog.LabelId; MenuEntryDialog.HotkeyId; MenuEntryDialog.LineId ]
        |> List.map (fun id -> (MenuEntryDialog.field dialog id).Text)

    Assert.Equal<string list>([ "Compare"; "C"; "code --diff %Lp %Rp" ], fields)
    Assert.Equal(Nullable 2, (MenuEntryDialog.runSelector dialog).Value)
    Assert.Equal(CheckState.Checked, (MenuEntryDialog.administratorBox dialog).Value)
    Assert.False (MenuEntryDialog.kindSelector dialog).Visible

[<Fact>]
let ``an entry being changed keeps its own hotkey`` () =
    use dialog =
        opened
            { Editing = Changing compare
              Others = [ edit ] }

    dialog.Result <- 1

    let answer = MenuEntryDialog.answer dialog

    Assert.Equal(Some(CommandEntered compare), answer)

[<Fact>]
let ``a cancelled dialog answers nothing`` () =
    use dialog = adding ()
    fill dialog "e" "Edit" "vim %p"

    let answer = MenuEntryDialog.answer dialog

    Assert.Equal(None, answer)
