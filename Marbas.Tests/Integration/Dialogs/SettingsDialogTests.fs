module Marbas.Tests.Integration.Dialogs.SettingsDialogTests

open System.Drawing
open Xunit
open Terminal.Gui.Input
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.Theme
open Marbas.Ui.Dialogs
open Marbas.Tests

let private ext raw = Expect.some (FileExtension.tryCreate raw)

let private program raw = Expect.some (ProgramPath.tryCreate raw)

let private stored =
    { DefaultEditor = Some(program "code.exe")
      Associations =
        [ { Extension = ext "fs"
            Role = ForEditing
            Program = program "rider.exe" }
          { Extension = ext "txt"
            Role = ForEditing
            Program = program "notepad.exe" }
          { Extension = ext "html"
            Role = ForOpening
            Program = program "firefox.exe" } ] }

let private laidOutWith programs candidate =
    DialogStyle.apply ()
    let dialog = SettingsDialog.create programs candidate
    dialog.BeginInit()
    dialog.EndInit()
    dialog.Layout(Size(80, 25)) |> ignore
    dialog

let private laidOut programs = laidOutWith programs None

let private press (dialog: SettingsDialog.SettingsView) id =
    (SettingsDialog.button dialog id).InvokeCommand Command.Accept |> ignore

let private rowsOf (dialog: SettingsDialog.SettingsView) role =
    let list = SettingsDialog.known dialog role
    let source = list.Source.ToList()
    [ for index in 0 .. list.Source.Count - 1 -> source[index].ToString() ]

let private rows (dialog: SettingsDialog.SettingsView) = rowsOf dialog ForEditing

let private choose (dialog: SettingsDialog.SettingsView) role =
    (SettingsDialog.selector dialog).Value <-
        System.Nullable(
            match role with
            | ForEditing -> 0
            | ForOpening -> 1
        )

[<Fact>]
let ``the default editor is offered ready to be edited`` () =
    use dialog = laidOut stored

    Assert.Equal("code.exe", (SettingsDialog.field dialog SettingsDialog.DefaultEditorId).Text)

[<Fact>]
let ``a settings dialog with no default editor opens empty`` () =
    use dialog = laidOut ProgramSettings.none

    Assert.Equal("", (SettingsDialog.field dialog SettingsDialog.DefaultEditorId).Text)
    Assert.Empty(rowsOf dialog ForEditing)
    Assert.Empty(rowsOf dialog ForOpening)

[<Fact>]
let ``every association is listed under the role it was made in`` () =
    use dialog = laidOut stored

    let edited = rowsOf dialog ForEditing

    Assert.Equal(2, edited.Length)
    Assert.StartsWith("fs", edited[0])
    Assert.Contains("rider.exe", edited[0])
    Assert.StartsWith("txt", edited[1])
    Assert.Contains("notepad.exe", edited[1])

    let opened = Assert.Single(rowsOf dialog ForOpening)

    Assert.StartsWith("html", opened)
    Assert.Contains("firefox.exe", opened)

[<Fact>]
let ``a dismissed dialog yields no programs`` () =
    use dialog = laidOut stored

    Assert.Equal(None, SettingsDialog.programs dialog)

[<Fact>]
let ``the default editor that was typed comes back`` () =
    use dialog = laidOut stored
    (SettingsDialog.field dialog SettingsDialog.DefaultEditorId).Text <- "vim"
    dialog.Result <- 1

    let answered = SettingsDialog.programs dialog |> Expect.some

    Assert.Equal(Some(program "vim"), answered.DefaultEditor)

[<Fact>]
let ``a default editor left blank names no editor`` () =
    use dialog = laidOut stored
    (SettingsDialog.field dialog SettingsDialog.DefaultEditorId).Text <- "   "
    dialog.Result <- 1

    let answered = SettingsDialog.programs dialog |> Expect.some

    Assert.Equal(None, answered.DefaultEditor)

[<Fact>]
let ``an extension and a program that are added join the list`` () =
    use dialog = laidOut stored
    (SettingsDialog.field dialog SettingsDialog.ExtensionId).Text <- "md"
    (SettingsDialog.field dialog SettingsDialog.ProgramId).Text <- "typora.exe"

    press dialog SettingsDialog.AddId
    dialog.Result <- 1

    let answered = SettingsDialog.programs dialog |> Expect.some

    Assert.Equal(3, (rows dialog).Length)

    Assert.Equal(
        Some(program "typora.exe"),
        ProgramSettings.programFor ForEditing (EntryName.create "notes.md") answered
    )

[<Fact>]
let ``the role that is chosen is the one the association is made in`` () =
    use dialog = laidOut stored
    choose dialog ForOpening
    (SettingsDialog.field dialog SettingsDialog.ExtensionId).Text <- "pdf"
    (SettingsDialog.field dialog SettingsDialog.ProgramId).Text <- "acrobat.exe"

    press dialog SettingsDialog.AddId
    dialog.Result <- 1

    let answered = SettingsDialog.programs dialog |> Expect.some

    Assert.Equal(2, (rowsOf dialog ForOpening).Length)
    Assert.Equal(2, (rowsOf dialog ForEditing).Length)
    Assert.Equal(Some(program "acrobat.exe"), ProgramSettings.associated ForOpening (ext "pdf") answered)
    Assert.Equal(None, ProgramSettings.associated ForEditing (ext "pdf") answered)

[<Fact>]
let ``adding an extension that is already known replaces its program`` () =
    use dialog = laidOut stored
    (SettingsDialog.field dialog SettingsDialog.ExtensionId).Text <- "txt"
    (SettingsDialog.field dialog SettingsDialog.ProgramId).Text <- "vim"

    press dialog SettingsDialog.AddId
    dialog.Result <- 1

    let answered = SettingsDialog.programs dialog |> Expect.some

    Assert.Equal(2, (rows dialog).Length)
    Assert.Equal(Some(program "vim"), ProgramSettings.programFor ForEditing (EntryName.create "notes.txt") answered)

[<Fact>]
let ``Enter in either field adds the association instead of closing the popup`` () =
    for id in [ SettingsDialog.ExtensionId; SettingsDialog.ProgramId ] do
        use dialog = laidOut stored
        (SettingsDialog.field dialog SettingsDialog.ExtensionId).Text <- "md"
        (SettingsDialog.field dialog SettingsDialog.ProgramId).Text <- "typora.exe"

        (SettingsDialog.field dialog id).InvokeCommand Command.Accept |> ignore

        Assert.Equal(3, (rows dialog).Length)
        Assert.True(dialog.Canceled, $"Enter in {id} must not close the popup")

[<Fact>]
let ``an association with nothing on either side is not added`` () =
    use dialog = laidOut stored
    (SettingsDialog.field dialog SettingsDialog.ExtensionId).Text <- "md"
    (SettingsDialog.field dialog SettingsDialog.ProgramId).Text <- "   "

    press dialog SettingsDialog.AddId

    Assert.Equal(2, (rows dialog).Length)

[<Fact>]
let ``the extension in the field is the one removed`` () =
    use dialog = laidOut stored
    (SettingsDialog.field dialog SettingsDialog.ExtensionId).Text <- "txt"

    press dialog SettingsDialog.RemoveId
    dialog.Result <- 1

    let answered = SettingsDialog.programs dialog |> Expect.some

    Assert.Equal(1, (rows dialog).Length)
    Assert.Equal(None, ProgramSettings.associated ForEditing (ext "txt") answered)

[<Fact>]
let ``removing takes the row out of the chosen role alone`` () =
    use dialog = laidOut stored
    choose dialog ForOpening
    (SettingsDialog.field dialog SettingsDialog.ExtensionId).Text <- "html"

    press dialog SettingsDialog.RemoveId
    dialog.Result <- 1

    let answered = SettingsDialog.programs dialog |> Expect.some

    Assert.Empty(rowsOf dialog ForOpening)
    Assert.Equal(2, (rowsOf dialog ForEditing).Length)
    Assert.Equal(None, ProgramSettings.associated ForOpening (ext "html") answered)

[<Fact>]
let ``the association under the cursor fills the fields`` () =
    use dialog = laidOut stored

    (SettingsDialog.known dialog ForEditing).SelectedItem <- 1

    Assert.Equal("txt", (SettingsDialog.field dialog SettingsDialog.ExtensionId).Text)
    Assert.Equal("notepad.exe", (SettingsDialog.field dialog SettingsDialog.ProgramId).Text)

[<Fact>]
let ``an association picked out of the opening list chooses its role too`` () =
    use dialog = laidOut stored

    (SettingsDialog.known dialog ForOpening).SelectedItem <- 0

    Assert.Equal("html", (SettingsDialog.field dialog SettingsDialog.ExtensionId).Text)
    Assert.Equal("firefox.exe", (SettingsDialog.field dialog SettingsDialog.ProgramId).Text)
    Assert.Equal(System.Nullable 1, (SettingsDialog.selector dialog).Value)

[<Fact>]
let ``the executable under the panel cursor fills the default editor`` () =
    use dialog = laidOutWith stored (Some(program "C:/tools/notepad++.exe"))
    (SettingsDialog.field dialog SettingsDialog.DefaultEditorId).SetFocus() |> ignore

    press dialog SettingsDialog.CandidateId

    Assert.Equal("C:/tools/notepad++.exe", (SettingsDialog.field dialog SettingsDialog.DefaultEditorId).Text)

[<Fact>]
let ``the executable under the panel cursor fills the program being associated`` () =
    use dialog = laidOutWith stored (Some(program "C:/tools/notepad++.exe"))
    (SettingsDialog.field dialog SettingsDialog.ProgramId).SetFocus() |> ignore

    press dialog SettingsDialog.CandidateId

    Assert.Equal("C:/tools/notepad++.exe", (SettingsDialog.field dialog SettingsDialog.ProgramId).Text)
    Assert.Equal("code.exe", (SettingsDialog.field dialog SettingsDialog.DefaultEditorId).Text)

[<Fact>]
let ``the default editor is the field filled before any has been typed in`` () =
    use dialog = laidOutWith stored (Some(program "C:/tools/notepad++.exe"))

    press dialog SettingsDialog.CandidateId

    Assert.Equal("C:/tools/notepad++.exe", (SettingsDialog.field dialog SettingsDialog.DefaultEditorId).Text)

[<Fact>]
let ``with no executable under the cursor the button cannot be reached`` () =
    use dialog = laidOut stored

    Assert.False((SettingsDialog.button dialog SettingsDialog.CandidateId).Enabled)

[<Fact>]
let ``an executable under the cursor makes the button reachable`` () =
    use dialog = laidOutWith stored (Some(program "C:/tools/notepad++.exe"))

    Assert.True((SettingsDialog.button dialog SettingsDialog.CandidateId).Enabled)

[<Fact>]
let ``every button fits inside the dialog`` () =
    use dialog = laidOutWith stored (Some(program "C:/tools/notepad++.exe"))

    for id in [ SettingsDialog.AddId; SettingsDialog.RemoveId; SettingsDialog.CandidateId ] do
        let frame = (SettingsDialog.button dialog id).Frame
        Assert.True(frame.Right <= dialog.Viewport.Width, $"the button {id} at %A{frame} runs past the frame")

[<Fact>]
let ``every field fits inside the dialog`` () =
    use dialog = laidOut stored

    for id in [ SettingsDialog.DefaultEditorId; SettingsDialog.ExtensionId; SettingsDialog.ProgramId ] do
        let frame = (SettingsDialog.field dialog id).Frame
        Assert.True(frame.Width > 0, $"the field {id} must be wide enough to type into")
        Assert.True(frame.Bottom <= dialog.Viewport.Height, $"the field {id} at %A{frame} is clipped")

[<Fact>]
let ``both lists fit inside the dialog`` () =
    use dialog = laidOut stored

    let editing = (SettingsDialog.known dialog ForEditing).Frame
    let opening = (SettingsDialog.known dialog ForOpening).Frame

    Assert.True(editing.Height > 0, "the editing list must have room for a row")
    Assert.True(opening.Height > 0, "the opening list must have room for a row")
    Assert.True(editing.Bottom <= dialog.Viewport.Height, $"the editing list at %A{editing} is clipped")
    Assert.True(opening.Bottom <= dialog.Viewport.Height, $"the opening list at %A{opening} is clipped")
