module Marbas.Ui.Dialogs.FavoritesDialog

open System
open System.Collections.ObjectModel
open System.IO.Abstractions
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Profile
open Marbas.State
open Marbas.Ui
open Marbas.Ui.Format
open Marbas.Ui.Keys
open Marbas.Ui.Theme

[<Literal>]
let Title = "Favourites"

[<Literal>]
let Hint = "Enter or a digit: go    Ins: add current path    Del: remove"

[<Literal>]
let private SizeShare = 80

[<Literal>]
let private ListHeight = 10

[<Literal>]
let private FrameHeight = 5

type FavoritesView(favorites: Favorites) =
    inherit Dialog(Title = Title, Width = Dim.Percent SizeShare, Height = Dim.Absolute(ListHeight + FrameHeight))

    member val Favorites = favorites with get, set

    member val Chosen: Place option = None with get, set

type private Changing =
    { Fs: IFileSystem
      Current: Place option
      Change: Ports.FavoritesAfter
      Slots: ListView }

let private rows fs width favorites =
    FavoriteSlot.all
    |> List.map (fun slot -> FavoriteRow.describe fs width slot (Favorites.placeIn slot favorites))

let private positionOf slot = FavoriteSlot.value slot

let private fill (changing: Changing) (dialog: FavoritesView) =
    let cursor =
        changing.Slots.SelectedItem |> Option.ofNullable |> Option.defaultValue 0

    rows changing.Fs changing.Slots.Viewport.Width dialog.Favorites
    |> ObservableCollection
    |> changing.Slots.SetSource

    changing.Slots.SelectedItem <- Nullable cursor

let private slotUnderCursor (slots: ListView) =
    slots.SelectedItem
    |> Option.ofNullable
    |> Option.bind (fun index -> List.tryItem index FavoriteSlot.all)

let private going (changing: Changing) (dialog: FavoritesView) slot =
    match Favorites.placeIn slot dialog.Favorites with
    | Some place ->
        dialog.Chosen <- Some place
        dialog.RequestStop()
    | None -> changing.Slots.SelectedItem <- Nullable(positionOf slot)

let private changed (changing: Changing) (dialog: FavoritesView) change =
    changing.Change change
    |> Option.iter (fun favorites ->
        dialog.Favorites <- favorites
        fill changing dialog
        dialog.SetNeedsDraw())

let private keeping (changing: Changing) dialog =
    match slotUnderCursor changing.Slots, changing.Current with
    | Some slot, Some place -> changed changing dialog (Keep(slot, place))
    | _ -> ()

let private emptying (changing: Changing) dialog =
    slotUnderCursor changing.Slots
    |> Option.iter (EmptySlot >> changed changing dialog)

let private answer (changing: Changing) (dialog: FavoritesView) (key: Key) =
    let handled act =
        key.Handled <- true
        act ()

    match KeyMap.favoriteSlotOf key with
    | Some slot -> handled (fun () -> going changing dialog slot)
    | None when key = KeyMap.favoritesGoKey ->
        handled (fun () -> slotUnderCursor changing.Slots |> Option.iter (going changing dialog))
    | None when key = KeyMap.favoritesKeepKey -> handled (fun () -> keeping changing dialog)
    | None when key = KeyMap.favoritesEmptyKey -> handled (fun () -> emptying changing dialog)
    | None -> ()

let create (fs: IFileSystem) (change: FavoriteChange -> Favorites option) (current: Place option) favorites =
    let dialog = new FavoritesView(favorites)
    DialogStyle.dress dialog |> ignore

    let slots =
        new ListView(X = 1, Y = 1, Width = Dim.Fill 1, Height = ListHeight, KeystrokeNavigator = null)

    let hint =
        new Label(X = 1, Y = Pos.Bottom slots + Pos.Absolute 1, Height = 1, Text = Hint)
        |> LiteralText.showing

    let changing =
        { Fs = fs
          Current = current
          Change = change
          Slots = slots }

    fill changing dialog
    slots.FrameChanged.Add(fun _ -> fill changing dialog)
    slots.KeyDown.Add(answer changing dialog)
    dialog.Add(slots, hint)
    dialog

let slots (dialog: FavoritesView) = SubView.byType<ListView> dialog

let chosen (dialog: FavoritesView) = dialog.Chosen
