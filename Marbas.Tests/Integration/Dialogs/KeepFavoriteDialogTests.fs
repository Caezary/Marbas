module Marbas.Tests.Integration.Dialogs.KeepFavoriteDialogTests

open System.Drawing
open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.Format
open Marbas.Ui.Theme
open Marbas.Ui.Dialogs
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private work = Place.OnDisk(fp (at "work"))

let private longPath =
    Place.OnDisk(fp (at "projects/clients/northwind/reports/quarterly/2026/drafts"))

let private laidOutOn screenWidth place =
    DialogStyle.apply ()
    let dialog = KeepFavoriteDialog.create (MockFileSystem()) screenWidth place
    dialog.BeginInit()
    dialog.EndInit()
    dialog.Layout(Size(screenWidth, 25)) |> ignore
    dialog

let private laidOut () = laidOutOn 80 work

let private slot digit =
    Expect.some (FavoriteSlot.tryCreate digit)

[<Fact>]
let ``a dialog closed before any key keeps nothing`` () =
    use dialog = laidOut ()

    let chosen = KeepFavoriteDialog.slot dialog

    Assert.Equal(None, chosen)

[<Theory>]
[<InlineData('0', 0)>]
[<InlineData('4', 4)>]
[<InlineData('9', 9)>]
let ``a digit is the slot the place is kept under`` (character: char, digit: int) =
    use dialog = laidOut ()

    dialog.NewKeyDownEvent(Key character) |> ignore

    Assert.Equal(Some(slot digit), KeepFavoriteDialog.slot dialog)

[<Fact>]
let ``a digit pressed with Alt still held is the slot too`` () =
    use dialog = laidOut ()

    dialog.NewKeyDownEvent Key.D4.WithAlt |> ignore

    Assert.Equal(Some(slot 4), KeepFavoriteDialog.slot dialog)

[<Fact>]
let ``a letter keeps nothing`` () =
    use dialog = laidOut ()

    dialog.NewKeyDownEvent Key.K |> ignore

    Assert.Equal(None, KeepFavoriteDialog.slot dialog)

[<Fact>]
let ``escape keeps nothing`` () =
    use dialog = laidOut ()

    dialog.NewKeyDownEvent Key.Esc |> ignore

    Assert.Equal(None, KeepFavoriteDialog.slot dialog)

[<Fact>]
let ``the first key decides, and a digit after a letter keeps nothing`` () =
    use dialog = laidOut ()
    dialog.NewKeyDownEvent Key.K |> ignore

    dialog.NewKeyDownEvent(Key '4') |> ignore

    Assert.Equal(None, KeepFavoriteDialog.slot dialog)

[<Fact>]
let ``the popup is titled for adding to the favourites`` () =
    use dialog = laidOut ()

    let title = dialog.Title

    Assert.Equal("Add to favourites", title)

[<Fact>]
let ``the question names the path it will add`` () =
    use dialog = laidOut ()

    let question = (KeepFavoriteDialog.question dialog).Text

    Assert.Equal("Press a digit to add " + at "work" + " to favourites", question)

[<Fact>]
let ``a level inside an archive is named as the archive's path and the level`` () =
    let place = Place.InArchive(fp (at "backup.zip"), Expect.some (ArchivePath.tryCreate "docs/2024"))
    use dialog = laidOutOn 80 place

    let question = (KeepFavoriteDialog.question dialog).Text

    Assert.Equal("Press a digit to add " + at "backup.zip/docs/2024" + " to favourites", question)

[<Fact>]
let ``the question fits inside the frame on one line`` () =
    use dialog = laidOut ()

    let question = KeepFavoriteDialog.question dialog

    Assert.Equal(question.Text.Length, question.Frame.Width)
    Assert.Equal(1, question.Frame.Height)
    Assert.True(question.Frame.Right <= dialog.Viewport.Width, $"the question at %A{question.Frame} runs past the frame")

[<Fact>]
let ``a path too long for the screen is shortened in the middle and the popup stays on it`` () =
    use dialog = laidOutOn 60 longPath

    let question = (KeepFavoriteDialog.question dialog).Text

    Assert.True(dialog.Frame.Width <= 60, $"the popup is {dialog.Frame.Width} columns wide")
    Assert.StartsWith("Press a digit to add ", question)
    Assert.EndsWith(PathText.Ellipsis + Root.relative "/2026/drafts" + " to favourites", question)
