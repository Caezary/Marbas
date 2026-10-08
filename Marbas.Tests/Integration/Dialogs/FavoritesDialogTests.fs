module Marbas.Tests.Integration.Dialogs.FavoritesDialogTests

open System
open System.Drawing
open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.Keys
open Marbas.Ui.Theme
open Marbas.Ui.Dialogs
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private fp = FileSystemPath.create

let private slot digit =
    Expect.some (FavoriteSlot.tryCreate digit)

let private here = Place.OnDisk(fp (at "start"))

let private work = Place.OnDisk(fp (at "work"))

let private held =
    Favorites.empty
    |> Favorites.apply (Keep(slot 2, work))
    |> Favorites.apply (Keep(slot 5, Place.OnDisk(fp (at "backup"))))

let private applying (changes: ResizeArray<FavoriteChange>) =
    let mutable favorites = held

    fun change ->
        changes.Add change
        favorites <- Favorites.apply change favorites
        Some favorites

let private refusing (changes: ResizeArray<FavoriteChange>) =
    fun change ->
        changes.Add change
        None

let private laidOutWith change current =
    DialogStyle.apply ()
    let dialog = FavoritesDialog.create (MockFileSystem()) change current held
    dialog.BeginInit()
    dialog.EndInit()
    dialog.Layout(Size(100, 30)) |> ignore
    (FavoritesDialog.slots dialog).SetFocus() |> ignore
    dialog

let private laidOut () =
    laidOutWith (applying (ResizeArray())) (Some here)

let private shown (dialog: FavoritesDialog.FavoritesView) =
    let list = FavoritesDialog.slots dialog
    let source = list.Source.ToList()
    [ for index in 0 .. list.Source.Count - 1 -> source[index].ToString() ]

let private standOn (digit: int) (dialog: FavoritesDialog.FavoritesView) =
    (FavoritesDialog.slots dialog).SelectedItem <- Nullable digit

[<Fact>]
let ``the ten slots are listed in order, each with its digit and its place`` () =
    use dialog = laidOut ()

    let rows = shown dialog

    Assert.Equal<string list>(
        [ "0"
          "1"
          "2  " + at "work"
          "3"
          "4"
          "5  " + at "backup"
          "6"
          "7"
          "8"
          "9" ],
        rows
    )

[<Fact>]
let ``the list opens on the first slot`` () =
    use dialog = laidOut ()

    let cursor = (FavoritesDialog.slots dialog).SelectedItem

    Assert.Equal(0, cursor.GetValueOrDefault -1)

[<Fact>]
let ``a popup closed without a choice goes nowhere`` () =
    use dialog = laidOut ()

    let chosen = FavoritesDialog.chosen dialog

    Assert.Equal(None, chosen)

[<Fact>]
let ``Enter goes to the place of the slot under the cursor`` () =
    use dialog = laidOut ()
    standOn 2 dialog

    dialog.NewKeyDownEvent Key.Enter |> ignore

    Assert.Equal(Some work, FavoritesDialog.chosen dialog)

[<Fact>]
let ``a digit goes to the place under it, wherever the cursor stands`` () =
    use dialog = laidOut ()

    dialog.NewKeyDownEvent(Key '2') |> ignore

    Assert.Equal(Some work, FavoritesDialog.chosen dialog)

[<Fact>]
let ``Enter on an empty slot goes nowhere`` () =
    use dialog = laidOut ()
    standOn 3 dialog

    dialog.NewKeyDownEvent Key.Enter |> ignore

    Assert.Equal(None, FavoritesDialog.chosen dialog)

[<Fact>]
let ``the digit of an empty slot moves the cursor onto it and goes nowhere`` () =
    use dialog = laidOut ()

    dialog.NewKeyDownEvent(Key '7') |> ignore

    Assert.Equal(None, FavoritesDialog.chosen dialog)
    Assert.Equal(7, (FavoritesDialog.slots dialog).SelectedItem.GetValueOrDefault -1)

[<Fact>]
let ``Ins keeps the active panel's place in the slot under the cursor`` () =
    let changes = ResizeArray<FavoriteChange>()
    use dialog = laidOutWith (applying changes) (Some here)
    standOn 3 dialog

    press dialog KeyMap.favoritesKeepKey

    Assert.Equal<FavoriteChange list>([ Keep(slot 3, here) ], List.ofSeq changes)
    Assert.Equal("3  " + at "start", (shown dialog)[3])

[<Fact>]
let ``Ins overwrites the place a slot held`` () =
    use dialog = laidOut ()
    standOn 2 dialog

    press dialog KeyMap.favoritesKeepKey

    Assert.Equal("2  " + at "start", (shown dialog)[2])

[<Fact>]
let ``Ins over search results keeps nothing`` () =
    let changes = ResizeArray<FavoriteChange>()
    use dialog = laidOutWith (applying changes) None
    standOn 3 dialog

    press dialog KeyMap.favoritesKeepKey

    Assert.Empty changes

[<Fact>]
let ``Del empties the slot under the cursor`` () =
    let changes = ResizeArray<FavoriteChange>()
    use dialog = laidOutWith (applying changes) (Some here)
    standOn 2 dialog

    press dialog KeyMap.favoritesEmptyKey

    Assert.Equal<FavoriteChange list>([ EmptySlot(slot 2) ], List.ofSeq changes)
    Assert.Equal("2", (shown dialog)[2])

[<Fact>]
let ``a change the file refused leaves the slots as they were`` () =
    use dialog = laidOutWith (refusing (ResizeArray())) (Some here)
    standOn 2 dialog

    press dialog KeyMap.favoritesEmptyKey

    Assert.Equal("2  " + at "work", (shown dialog)[2])

[<Fact>]
let ``the cursor stays on the slot it changed`` () =
    use dialog = laidOut ()
    standOn 5 dialog

    press dialog KeyMap.favoritesEmptyKey

    Assert.Equal(5, (FavoritesDialog.slots dialog).SelectedItem.GetValueOrDefault -1)
