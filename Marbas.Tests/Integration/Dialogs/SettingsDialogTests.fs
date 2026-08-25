module Marbas.Tests.Integration.Dialogs.SettingsDialogTests

open System.Drawing
open Xunit
open Terminal.Gui.Input
open Marbas.FileSystem
open Marbas.Ui.Theme
open Marbas.Ui.Dialogs
open Marbas.Tests

let private ext raw = Expect.some (FileExtension.tryCreate raw)

let private editor raw = Expect.some (EditorPath.tryCreate raw)

let private stored =
    { DefaultEditor = Some(editor "code.exe")
      Associations =
        [ { Extension = ext "fs"; Editor = editor "rider.exe" }
          { Extension = ext "txt"; Editor = editor "notepad.exe" } ] }

let private laidOutWith editors candidate =
    DialogStyle.apply ()
    let dialog = SettingsDialog.create editors candidate
    dialog.BeginInit()
    dialog.EndInit()
    dialog.Layout(Size(80, 24)) |> ignore
    dialog

let private laidOut editors = laidOutWith editors None

let private press (dialog: SettingsDialog.SettingsView) id =
    (SettingsDialog.button dialog id).InvokeCommand Command.Accept |> ignore

let private rows (dialog: SettingsDialog.SettingsView) =
    let list = SettingsDialog.known dialog
    let source = list.Source.ToList()
    [ for index in 0 .. list.Source.Count - 1 -> source[index].ToString() ]

[<Fact>]
let ``the default editor is offered ready to be edited`` () =
    use dialog = laidOut stored

    Assert.Equal("code.exe", (SettingsDialog.field dialog SettingsDialog.DefaultEditorId).Text)

[<Fact>]
let ``a settings dialog with no default editor opens empty`` () =
    use dialog = laidOut EditorSettings.none

    Assert.Equal("", (SettingsDialog.field dialog SettingsDialog.DefaultEditorId).Text)
    Assert.Empty(rows dialog)

[<Fact>]
let ``every association is listed`` () =
    use dialog = laidOut stored

    let listed = rows dialog

    Assert.Equal(2, listed.Length)
    Assert.StartsWith("fs", listed[0])
    Assert.Contains("rider.exe", listed[0])
    Assert.StartsWith("txt", listed[1])
    Assert.Contains("notepad.exe", listed[1])

[<Fact>]
let ``a dismissed dialog yields no editors`` () =
    use dialog = laidOut stored

    Assert.Equal(None, SettingsDialog.editors dialog)

[<Fact>]
let ``the default editor that was typed comes back`` () =
    use dialog = laidOut stored
    (SettingsDialog.field dialog SettingsDialog.DefaultEditorId).Text <- "vim"
    dialog.Result <- 1

    let answered = SettingsDialog.editors dialog |> Expect.some

    Assert.Equal(Some(editor "vim"), answered.DefaultEditor)

[<Fact>]
let ``a default editor left blank names no editor`` () =
    use dialog = laidOut stored
    (SettingsDialog.field dialog SettingsDialog.DefaultEditorId).Text <- "   "
    dialog.Result <- 1

    let answered = SettingsDialog.editors dialog |> Expect.some

    Assert.Equal(None, answered.DefaultEditor)

[<Fact>]
let ``an extension and an editor that are added join the list`` () =
    use dialog = laidOut stored
    (SettingsDialog.field dialog SettingsDialog.ExtensionId).Text <- "md"
    (SettingsDialog.field dialog SettingsDialog.EditorId).Text <- "typora.exe"

    press dialog SettingsDialog.AddId
    dialog.Result <- 1

    let answered = SettingsDialog.editors dialog |> Expect.some

    Assert.Equal(3, (rows dialog).Length)
    Assert.Equal(Some(editor "typora.exe"), EditorSettings.editorFor (EntryName.create "notes.md") answered)

[<Fact>]
let ``adding an extension that is already known replaces its editor`` () =
    use dialog = laidOut stored
    (SettingsDialog.field dialog SettingsDialog.ExtensionId).Text <- "txt"
    (SettingsDialog.field dialog SettingsDialog.EditorId).Text <- "vim"

    press dialog SettingsDialog.AddId
    dialog.Result <- 1

    let answered = SettingsDialog.editors dialog |> Expect.some

    Assert.Equal(2, (rows dialog).Length)
    Assert.Equal(Some(editor "vim"), EditorSettings.editorFor (EntryName.create "notes.txt") answered)

[<Fact>]
let ``Enter in either field adds the association instead of closing the popup`` () =
    for id in [ SettingsDialog.ExtensionId; SettingsDialog.EditorId ] do
        use dialog = laidOut stored
        (SettingsDialog.field dialog SettingsDialog.ExtensionId).Text <- "md"
        (SettingsDialog.field dialog SettingsDialog.EditorId).Text <- "typora.exe"

        (SettingsDialog.field dialog id).InvokeCommand Command.Accept |> ignore

        Assert.Equal(3, (rows dialog).Length)
        Assert.True(dialog.Canceled, $"Enter in {id} must not close the popup")

[<Fact>]
let ``an association with nothing on either side is not added`` () =
    use dialog = laidOut stored
    (SettingsDialog.field dialog SettingsDialog.ExtensionId).Text <- "md"
    (SettingsDialog.field dialog SettingsDialog.EditorId).Text <- "   "

    press dialog SettingsDialog.AddId

    Assert.Equal(2, (rows dialog).Length)

[<Fact>]
let ``the extension in the field is the one removed`` () =
    use dialog = laidOut stored
    (SettingsDialog.field dialog SettingsDialog.ExtensionId).Text <- "txt"

    press dialog SettingsDialog.RemoveId
    dialog.Result <- 1

    let answered = SettingsDialog.editors dialog |> Expect.some

    Assert.Equal(1, (rows dialog).Length)
    Assert.Equal(None, EditorSettings.associated (ext "txt") answered)

[<Fact>]
let ``the association under the cursor fills the fields`` () =
    use dialog = laidOut stored

    (SettingsDialog.known dialog).SelectedItem <- 1

    Assert.Equal("txt", (SettingsDialog.field dialog SettingsDialog.ExtensionId).Text)
    Assert.Equal("notepad.exe", (SettingsDialog.field dialog SettingsDialog.EditorId).Text)

[<Fact>]
let ``the executable under the panel cursor fills the default editor`` () =
    use dialog = laidOutWith stored (Some(editor "C:/tools/notepad++.exe"))
    (SettingsDialog.field dialog SettingsDialog.DefaultEditorId).SetFocus() |> ignore

    press dialog SettingsDialog.CandidateId

    Assert.Equal("C:/tools/notepad++.exe", (SettingsDialog.field dialog SettingsDialog.DefaultEditorId).Text)

[<Fact>]
let ``the executable under the panel cursor fills the editor being associated`` () =
    use dialog = laidOutWith stored (Some(editor "C:/tools/notepad++.exe"))
    (SettingsDialog.field dialog SettingsDialog.EditorId).SetFocus() |> ignore

    press dialog SettingsDialog.CandidateId

    Assert.Equal("C:/tools/notepad++.exe", (SettingsDialog.field dialog SettingsDialog.EditorId).Text)
    Assert.Equal("code.exe", (SettingsDialog.field dialog SettingsDialog.DefaultEditorId).Text)

[<Fact>]
let ``the default editor is the field filled before any has been typed in`` () =
    use dialog = laidOutWith stored (Some(editor "C:/tools/notepad++.exe"))

    press dialog SettingsDialog.CandidateId

    Assert.Equal("C:/tools/notepad++.exe", (SettingsDialog.field dialog SettingsDialog.DefaultEditorId).Text)

[<Fact>]
let ``with no executable under the cursor the button cannot be reached`` () =
    use dialog = laidOut stored

    Assert.False((SettingsDialog.button dialog SettingsDialog.CandidateId).Enabled)

[<Fact>]
let ``an executable under the cursor makes the button reachable`` () =
    use dialog = laidOutWith stored (Some(editor "C:/tools/notepad++.exe"))

    Assert.True((SettingsDialog.button dialog SettingsDialog.CandidateId).Enabled)

[<Fact>]
let ``every button fits inside the dialog`` () =
    use dialog = laidOutWith stored (Some(editor "C:/tools/notepad++.exe"))

    for id in [ SettingsDialog.AddId; SettingsDialog.RemoveId; SettingsDialog.CandidateId ] do
        let frame = (SettingsDialog.button dialog id).Frame
        Assert.True(frame.Right <= dialog.Viewport.Width, $"the button {id} at %A{frame} runs past the frame")

[<Fact>]
let ``every field fits inside the dialog`` () =
    use dialog = laidOut stored

    for id in [ SettingsDialog.DefaultEditorId; SettingsDialog.ExtensionId; SettingsDialog.EditorId ] do
        let frame = (SettingsDialog.field dialog id).Frame
        Assert.True(frame.Width > 0, $"the field {id} must be wide enough to type into")
        Assert.True(frame.Bottom <= dialog.Viewport.Height, $"the field {id} at %A{frame} is clipped")

    let list = (SettingsDialog.known dialog).Frame
    Assert.True(list.Height > 0, "the association list must have room for a row")
    Assert.True(list.Bottom <= dialog.Viewport.Height, $"the list at %A{list} is clipped")
