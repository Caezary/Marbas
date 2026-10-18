module Marbas.Tests.State.Shell.ShellFavoriteTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Profile
open Marbas.Domain
open Marbas.Messages
open Marbas.State
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.Shell.ShellFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private favoriteSlot digit =
    Expect.some (FavoriteSlot.tryCreate digit)

let private kept (deps: Ports.Dependencies) =
    Favorites.load deps.FileSystem deps.FavoritesPath |> Expect.ok

[<Fact>]
let ``a slot answered for a favourite keeps the place in it`` () =
    let deps =
        { noDeps () with
            Prompts.AskFavoriteSlot = fun _ -> Some(favoriteSlot 3) }

    Shell.interpret deps ignore (PromptForFavoriteSlot(Place.OnDisk(fp (at "work"))))

    Assert.Equal(Some(Place.OnDisk(fp (at "work"))), Favorites.placeIn (favoriteSlot 3) (kept deps))

[<Fact>]
let ``the slot is asked for with the place it will keep`` () =
    let asked = ResizeArray<Place>()

    let deps =
        { noDeps () with
            Prompts.AskFavoriteSlot =
                fun place ->
                    asked.Add place
                    None }

    Shell.interpret deps ignore (PromptForFavoriteSlot(Place.OnDisk(fp (at "work"))))

    Assert.Equal<Place list>([ Place.OnDisk(fp (at "work")) ], List.ofSeq asked)

[<Fact>]
let ``a favourite asked for and not answered keeps nothing`` () =
    let deps = noDeps ()

    Shell.interpret deps ignore (PromptForFavoriteSlot(Place.OnDisk(fp (at "work"))))

    Assert.False(deps.FileSystem.File.Exists(FileSystemPath.value deps.FavoritesPath))

[<Fact>]
let ``favourites that cannot be written are reported`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "blocked", MockFileData "x")
    let mutable reported = ""

    let deps =
        { Fakes.dependencies fs (at "start") with
            FavoritesPath = fp (at "blocked/favorites.conf")
            Prompts.AskFavoriteSlot = fun _ -> Some(favoriteSlot 3)
            Prompts.ReportError = fun report -> reported <- report.Title }

    Shell.interpret deps ignore (PromptForFavoriteSlot(Place.OnDisk(fp (at "work"))))

    Assert.Equal(FavoritesErrorMessage.Title, reported)

[<Fact>]
let ``a favourite looked up is dispatched as found`` () =
    let sent, dispatch = capture ()
    let deps = noDeps ()

    Favorites.change deps.FileSystem deps.FavoritesPath (Keep(favoriteSlot 5, Place.OnDisk(fp (at "work"))))
    |> Expect.ok
    |> ignore

    Shell.interpret deps dispatch (LookUpFavorite(favoriteSlot 5))

    Assert.Equal<Message list>([ FavoriteFound(Place.OnDisk(fp (at "work"))) ], List.ofSeq sent)

[<Fact>]
let ``an empty slot looked up dispatches nothing`` () =
    let sent, dispatch = capture ()

    Shell.interpret (noDeps ()) dispatch (LookUpFavorite(favoriteSlot 5))

    Assert.Empty sent

[<Fact>]
let ``favourites that cannot be read are reported when looked up`` () =
    let fs = Fakes.UnreadableFile(at "favorites.conf")
    fs.AddFile(at "favorites.conf", MockFileData "favorite.5=/work")
    let mutable reported = ""

    let deps =
        { Fakes.dependencies fs (at "start") with
            Prompts.ReportError = fun report -> reported <- report.Title }

    Shell.interpret deps ignore (LookUpFavorite(favoriteSlot 5))

    Assert.Equal(FavoritesErrorMessage.Title, reported)

let private keptIn (deps: Ports.Dependencies) digit path =
    Favorites.change deps.FileSystem deps.FavoritesPath (Keep(favoriteSlot digit, Place.OnDisk(fp (at path))))
    |> Expect.ok
    |> ignore

[<Fact>]
let ``the favourites popup is offered what the file holds and the place to keep`` () =
    let asked = ResizeArray<Ports.FavoritesPrompt>()

    let deps =
        { noDeps () with
            Prompts.ChooseFavorite =
                fun prompt ->
                    asked.Add prompt
                    None }

    keptIn deps 2 "work"

    Shell.interpret deps ignore (ShowFavorites(Some(Place.OnDisk(fp (at "start")))))

    let prompt = Assert.Single asked
    Assert.Equal(Some(Place.OnDisk(fp (at "work"))), Favorites.placeIn (favoriteSlot 2) prompt.Favorites)
    Assert.Equal(Some(Place.OnDisk(fp (at "start"))), prompt.Current)

[<Fact>]
let ``a place chosen in the favourites popup is dispatched as found`` () =
    let sent, dispatch = capture ()

    let deps =
        { noDeps () with
            Prompts.ChooseFavorite = fun _ -> Some(Place.OnDisk(fp (at "work"))) }

    Shell.interpret deps dispatch (ShowFavorites None)

    Assert.Equal<Message list>([ FavoriteFound(Place.OnDisk(fp (at "work"))) ], List.ofSeq sent)

[<Fact>]
let ``closing the favourites popup dispatches nothing`` () =
    let sent, dispatch = capture ()

    Shell.interpret (noDeps ()) dispatch (ShowFavorites None)

    Assert.Empty sent

[<Fact>]
let ``favourites that cannot be read are reported and the popup is not shown`` () =
    let fs = Fakes.UnreadableFile(at "favorites.conf")
    fs.AddFile(at "favorites.conf", MockFileData "favorite.5=/work")
    let mutable reported = ""
    let mutable shown = false

    let deps =
        { Fakes.dependencies fs (at "start") with
            Prompts.ReportError = fun report -> reported <- report.Title
            Prompts.ChooseFavorite =
                fun _ ->
                    shown <- true
                    None }

    Shell.interpret deps ignore (ShowFavorites None)

    Assert.Equal(FavoritesErrorMessage.Title, reported)
    Assert.False shown

[<Fact>]
let ``a slot filled from the popup is written at once and handed back`` () =
    let mutable answered = None

    let deps =
        { noDeps () with
            Prompts.ChooseFavorite =
                fun prompt ->
                    answered <- prompt.Change(Keep(favoriteSlot 4, Place.OnDisk(fp (at "start"))))
                    None }

    Shell.interpret deps ignore (ShowFavorites(Some(Place.OnDisk(fp (at "start")))))

    Assert.Equal(Some(Place.OnDisk(fp (at "start"))), Favorites.placeIn (favoriteSlot 4) (kept deps))
    Assert.Equal(Some(kept deps), answered)

[<Fact>]
let ``a slot emptied in the popup is empty after a restart`` () =
    let deps =
        { noDeps () with
            Prompts.ChooseFavorite =
                fun prompt ->
                    prompt.Change(EmptySlot(favoriteSlot 2)) |> ignore
                    None }

    keptIn deps 2 "work"

    Shell.interpret deps ignore (ShowFavorites None)

    let restarted = Favorites.load deps.FileSystem deps.FavoritesPath |> Expect.ok
    Assert.Equal(None, Favorites.placeIn (favoriteSlot 2) restarted)

[<Fact>]
let ``a change the file refuses from the popup is reported and hands back nothing`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "blocked", MockFileData "x")
    let mutable reported = ""
    let mutable answered = Some Favorites.empty

    let deps =
        { Fakes.dependencies fs (at "start") with
            FavoritesPath = fp (at "blocked/favorites.conf")
            Prompts.ReportError = fun report -> reported <- report.Title
            Prompts.ChooseFavorite =
                fun prompt ->
                    answered <- prompt.Change(Keep(favoriteSlot 4, Place.OnDisk(fp (at "start"))))
                    None }

    Shell.interpret deps ignore (ShowFavorites None)

    Assert.Equal(FavoritesErrorMessage.Title, reported)
    Assert.Equal(None, answered)
