module Marbas.Tests.Integration.Dialogs.OpenWithDialogTests

open System.Drawing
open Xunit
open Terminal.Gui.Input
open Terminal.Gui.Views
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.Theme
open Marbas.Ui.Dialogs
open Marbas.Tests

let private en = EntryName.create

let private program raw = Expect.some (ProgramPath.tryCreate raw)

let private handler name raw =
    Handler.tryCreate name (ProgramPath.tryCreate raw) |> Expect.some

let private known =
    [ handler "Firefox" "/usr/bin/firefox"
      handler "Visual Studio Code" "/usr/bin/code" ]

let private laidOutRemembering remembered name handlers chosen =
    DialogStyle.apply ()
    let dialog = OpenWithDialog.create (en name) handlers chosen remembered
    dialog.BeginInit()
    dialog.EndInit()
    dialog.Layout(Size(90, 25)) |> ignore
    dialog

let private laidOutWith name handlers chosen =
    laidOutRemembering [] name handlers chosen

let private laidOut () = laidOutWith "page.html" known None

let private rows (dialog: Dialog) =
    let list = OpenWithDialog.known dialog
    let source = list.Source.ToList()
    [ for index in 0 .. list.Source.Count - 1 -> source[index].ToString() ]

[<Fact>]
let ``every handler the system knows is listed`` () =
    use dialog = laidOut ()

    let listed = rows dialog

    Assert.Equal(2, listed.Length)
    Assert.StartsWith("Firefox", listed[0])
    Assert.Contains("/usr/bin/code", listed[1])

[<Fact>]
let ``the first handler is the one offered before anything is picked`` () =
    use dialog = laidOut ()

    Assert.Equal("/usr/bin/firefox", (OpenWithDialog.field dialog).Text)

[<Fact>]
let ``a program already remembered is the one offered, and the row it stands on`` () =
    use dialog = laidOutWith "page.html" known (Some(program "/usr/bin/code"))

    Assert.Equal("/usr/bin/code", (OpenWithDialog.field dialog).Text)
    Assert.Equal(System.Nullable 1, (OpenWithDialog.known dialog).SelectedItem)

[<Fact>]
let ``moving over a row offers the program on it`` () =
    use dialog = laidOut ()

    (OpenWithDialog.known dialog).SelectedItem <- 1

    Assert.Equal("/usr/bin/code", (OpenWithDialog.field dialog).Text)

[<Fact>]
let ``a dismissed dialog chooses nothing`` () =
    use dialog = laidOut ()

    Assert.Equal(None, OpenWithDialog.choice dialog)

[<Fact>]
let ``the program in the field is the one chosen`` () =
    use dialog = laidOut ()
    (OpenWithDialog.field dialog).Text <- "/opt/tools/viewer"
    dialog.Result <- 1

    let answered = OpenWithDialog.choice dialog |> Expect.some

    Assert.Equal(program "/opt/tools/viewer", answered.Program)
    Assert.Equal(ThisTimeOnly, answered.Use)

[<Fact>]
let ``a ticked box asks for the choice to be remembered`` () =
    use dialog = laidOut ()
    (OpenWithDialog.checkBox dialog).Value <- CheckState.Checked
    dialog.Result <- 1

    let answered = OpenWithDialog.choice dialog |> Expect.some

    Assert.Equal(AlwaysThisWay, answered.Use)

[<Fact>]
let ``a blank program chooses nothing`` () =
    use dialog = laidOut ()
    (OpenWithDialog.field dialog).Text <- "   "
    dialog.Result <- 1

    Assert.Equal(None, OpenWithDialog.choice dialog)

[<Fact>]
let ``the box says which extension it would remember`` () =
    use dialog = laidOut ()

    Assert.Contains(".html", (OpenWithDialog.checkBox dialog).Text)

[<Fact>]
let ``a name with no extension has nothing to remember the choice against`` () =
    use dialog = laidOutWith "README" known None

    Assert.False (OpenWithDialog.checkBox dialog).Enabled

[<Fact>]
let ``a file the system knows no program for still takes one that is typed`` () =
    use dialog = laidOutWith "notes.zzq" [] None
    (OpenWithDialog.field dialog).Text <- "/usr/bin/vi"
    dialog.Result <- 1

    Assert.Empty(rows dialog)
    Assert.Equal(program "/usr/bin/vi", (OpenWithDialog.choice dialog |> Expect.some).Program)

[<Fact>]
let ``everything on the dialog fits inside it`` () =
    use dialog = laidOut ()

    let field = (OpenWithDialog.field dialog).Frame
    let box = (OpenWithDialog.checkBox dialog).Frame
    let list = (OpenWithDialog.known dialog).Frame

    Assert.True(field.Width > 0, "the program field must be wide enough to type into")
    Assert.True(list.Height > 0, "the handler list must have room for a row")
    Assert.True(field.Bottom <= dialog.Viewport.Height, $"the program field at %A{field} is clipped")
    Assert.True(box.Bottom <= dialog.Viewport.Height, $"the box at %A{box} is clipped")
    Assert.True(list.Bottom <= dialog.Viewport.Height, $"the handler list at %A{list} is clipped")
    Assert.True(list.Right <= dialog.Viewport.Width, $"the handler list at %A{list} runs past the frame")

[<Fact>]
let ``the programs typed before are offered as the list of the program field`` () =
    use dialog =
        laidOutRemembering [ "/opt/editor"; "/usr/bin/vim" ] "page.html" known None

    let offered =
        [ for item in (OpenWithDialog.field dialog :?> DropDownList).Source.ToList() -> string item ]

    Assert.Equal<string list>([ "/opt/editor"; "/usr/bin/vim" ], offered)

[<Theory>]
[<InlineData("CursorUp")>]
[<InlineData("CursorDown")>]
let ``the arrows are left to move between the fields of the open-with popup`` (spelled: string) =
    use dialog = laidOut ()

    let taken =
        (OpenWithDialog.field dialog).KeyBindings.TryGet(Key.TryParse spelled |> snd)
        |> fst

    Assert.False taken
