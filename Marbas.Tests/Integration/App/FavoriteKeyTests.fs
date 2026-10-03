module Marbas.Tests.Integration.App.FavoriteKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.Keys
open Marbas.Ui.Panels
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private fp = FileSystemPath.create

let private favoritesPath = fp (at "favorites.conf")

let private slot digit =
    Expect.some (FavoriteSlot.tryCreate digit)

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddDirectory(at "elsewhere")
    fs

let private kept fs digit =
    Favorites.load fs favoritesPath |> Expect.ok |> Favorites.placeIn (slot digit)

let private keptBefore fs digit path =
    Favorites.change fs favoritesPath (Keep(slot digit, Place.OnDisk(fp (at path))))
    |> Expect.ok
    |> ignore

[<Fact>]
let ``Alt+K and a digit keep the directory the active panel stands in`` () =
    let fs = mockFs ()

    use window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                Prompts.AskFavoriteSlot = fun _ -> Some(slot 3) }

    pressKey window KeyMap.keepFavoriteKey

    Assert.Equal(Some(Place.OnDisk(fp (at "start"))), kept fs 3)

[<Fact>]
let ``Alt+K and a digit overwrite whatever the slot held`` () =
    let fs = mockFs ()
    keptBefore fs 3 "elsewhere"

    use window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                Prompts.AskFavoriteSlot = fun _ -> Some(slot 3) }

    pressKey window KeyMap.keepFavoriteKey

    Assert.Equal(Some(Place.OnDisk(fp (at "start"))), kept fs 3)

[<Fact>]
let ``Alt+K followed by something other than a digit keeps nothing`` () =
    let fs = mockFs ()
    use window = focusedWindow (Fakes.dependencies fs (at "start"))

    pressKey window KeyMap.keepFavoriteKey

    Assert.False(fs.File.Exists(FileSystemPath.value favoritesPath))

[<Fact>]
let ``Alt with a digit sends the active panel to the favourite under it`` () =
    let fs = mockFs ()
    keptBefore fs 3 "elsewhere"
    use window = focusedWindow (Fakes.dependencies fs (at "start"))

    pressKey window (KeyMap.favoriteKey (slot 3))

    Assert.Equal(at "elsewhere", shownPath FilePanel.LeftId window)

[<Fact>]
let ``going to a favourite is remembered in the folder history`` () =
    let fs = mockFs ()
    keptBefore fs 3 "elsewhere"
    use window = focusedWindow (Fakes.dependencies fs (at "start"))

    pressKey window (KeyMap.favoriteKey (slot 3))

    let folders = History.load fs (fp (at "history.conf")) |> Expect.ok
    Assert.Equal(Place.OnDisk(fp (at "elsewhere")), (List.head folders.Folders).Item)

[<Fact>]
let ``Alt with the digit of an empty slot does nothing, with no file to read`` () =
    let fs = mockFs ()
    let reported = ResizeArray<string>()

    use window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                Prompts.ReportError = fun report -> reported.Add report.Title }

    pressKey window (KeyMap.favoriteKey (slot 3))

    Assert.Equal(at "start", shownPath FilePanel.LeftId window)
    Assert.Empty reported

[<Fact>]
let ``a favourite that has gone is reported and the panel stays where it was`` () =
    let fs = mockFs ()
    keptBefore fs 3 "gone"
    let reported = ResizeArray<string>()

    use window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                Prompts.ReportError = fun report -> reported.Add report.Message }

    pressKey window (KeyMap.favoriteKey (slot 3))

    Assert.Contains(at "gone", Assert.Single reported)
    Assert.Equal(at "start", shownPath FilePanel.LeftId window)

let private opened (asked: ResizeArray<Place option>) =
    fun (prompt: Marbas.Ui.App.Ports.FavoritesPrompt) ->
        asked.Add prompt.Current
        None

[<Fact>]
let ``Alt+F8 opens the favourites with the place the active panel stands in`` () =
    let asked = ResizeArray<Place option>()

    use window =
        focusedWindow
            { Fakes.dependencies (mockFs ()) (at "start") with
                Prompts.ChooseFavorite = opened asked }

    pressKey window KeyMap.favoritesKey

    Assert.Equal<Place option list>([ Some(Place.OnDisk(fp (at "start"))) ], List.ofSeq asked)

[<Fact>]
let ``Alt+B opens the same favourites`` () =
    let asked = ResizeArray<Place option>()

    use window =
        focusedWindow
            { Fakes.dependencies (mockFs ()) (at "start") with
                Prompts.ChooseFavorite = opened asked }

    pressKey window KeyMap.spareFavoritesKey

    Assert.Equal<Place option list>([ Some(Place.OnDisk(fp (at "start"))) ], List.ofSeq asked)

[<Fact>]
let ``a place chosen in the favourites is where the active panel stands`` () =
    use window =
        focusedWindow
            { Fakes.dependencies (mockFs ()) (at "start") with
                Prompts.ChooseFavorite = fun _ -> Some(Place.OnDisk(fp (at "elsewhere"))) }

    pressKey window KeyMap.favoritesKey

    Assert.Equal(at "elsewhere", shownPath FilePanel.LeftId window)
