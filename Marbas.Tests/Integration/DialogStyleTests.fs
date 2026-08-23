module Marbas.Tests.Integration.DialogStyleTests

open System
open System.Drawing
open Xunit
open Terminal.Gui.Configuration
open Terminal.Gui.Drawing
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Ui

let private laidOut () =
    DialogStyle.apply ()
    let dialog = PromptDialog.create Shell.MkdirPrompt "Name:" ""
    dialog.BeginInit()
    dialog.EndInit()
    dialog.Layout(Size(80, 24)) |> ignore
    dialog

[<Fact>]
let ``a popup is framed with a double line, the way a focused panel is`` () =
    use dialog = laidOut ()

    Assert.Equal(Nullable LineStyle.Double, dialog.BorderStyle)

[<Fact>]
let ``a popup casts no shadow beside its frame`` () =
    use dialog = laidOut ()

    Assert.Equal(Nullable ShadowStyles.None, dialog.ShadowStyle)

[<Fact>]
let ``the buttons of a popup cast no shadow either`` () =
    use dialog = laidOut ()

    Assert.All(dialog.Buttons, fun button -> Assert.Equal(Nullable ShadowStyles.None, button.ShadowStyle))

[<Fact>]
let ``the buttons of a popup fit between the sides of its frame`` () =
    use dialog = laidOut ()

    let taken = dialog.Buttons |> Seq.sumBy (fun button -> button.Frame.Width)

    Assert.True(taken <= dialog.Frame.Width - 2, $"buttons take {taken} of a frame {dialog.Frame.Width} wide")

[<Fact>]
let ``the buttons of a popup sit centred under what it asks`` () =
    use dialog = laidOut ()

    Assert.Equal(Alignment.Center, dialog.ButtonAlignment)

[<Fact>]
let ``a popup wears the classic dialog colours`` () =
    use dialog = laidOut ()

    Assert.Equal(ClassicScheme.dialog, dialog.GetScheme())

[<Fact>]
let ``a popup keeps the room it needs for what it asks`` () =
    use dialog = laidOut ()

    Assert.True(dialog.Viewport.Height > 1, $"a shadow has eaten the content area %A{dialog.Viewport}")
    Assert.Equal(0, dialog.Margin.Thickness.Bottom)
    Assert.Equal(0, dialog.Margin.Thickness.Right)

[<Fact>]
let ``a popup is bracketed with signs every console font carries`` () =
    DialogStyle.apply ()

    Assert.Equal(DialogStyle.LeftBracket, Glyphs.LeftBracket)
    Assert.Equal(DialogStyle.RightBracket, Glyphs.RightBracket)

[<Fact>]
let ``a message box is framed and aligned like every other popup`` () =
    DialogStyle.apply ()

    Assert.Equal(DialogStyle.Frame, MessageBox.DefaultBorderStyle)
    Assert.Equal(DialogStyle.ButtonAlignment, MessageBox.DefaultButtonAlignment)

[<Fact>]
let ``a message box wears the classic dialog colours it cannot be given`` () =
    DialogStyle.apply ()

    Assert.Equal(ClassicScheme.dialog, SchemeManager.GetScheme Schemes.Dialog)

[<Fact>]
let ``a message box that reports a failure wears the classic error colours`` () =
    DialogStyle.apply ()

    Assert.Equal(ClassicScheme.errorDialog, SchemeManager.GetScheme Schemes.Error)
