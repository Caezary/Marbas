module Marbas.Tests.FileSystem.FavoritesTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private favoritesPath = fp (at "config/favorites.conf")

let private slot digit =
    Expect.some (FavoriteSlot.tryCreate digit)

let private onDisk path = Place.OnDisk(fp (at path))

let private inArchive archive inside =
    Place.InArchive(fp (at archive), Expect.some (ArchivePath.tryCreate inside))

let private keep digit place favorites =
    Favorites.apply (Keep(slot digit, place)) favorites

let private sample =
    Favorites.empty
    |> keep 1 (onDisk "work")
    |> keep 7 (inArchive "backup.zip" "docs/2024")

let private write (fs: MockFileSystem) lines =
    fs.AddFile(FileSystemPath.value favoritesPath, MockFileData(String.concat "\n" lines))

let private unwritten error =
    match error with
    | FavoritesAccessDenied path -> path
    | FavoritesFailed(path, _) -> path

[<Fact>]
let ``no slot holds a place before anything is kept`` () =
    let places =
        FavoriteSlot.all
        |> List.choose (fun slot -> Favorites.placeIn slot Favorites.empty)

    Assert.Empty places

[<Fact>]
let ``a place kept under a digit is the place that slot holds`` () =
    let favorites = keep 3 (onDisk "work") Favorites.empty

    let held = Favorites.placeIn (slot 3) favorites

    Assert.Equal(Some(onDisk "work"), held)

[<Fact>]
let ``a place kept under a digit leaves every other slot as it was`` () =
    let favorites = keep 3 (onDisk "elsewhere") sample

    let held =
        [ 1; 7 ] |> List.map (fun digit -> Favorites.placeIn (slot digit) favorites)

    Assert.Equal<Place option list>([ Some(onDisk "work"); Some(inArchive "backup.zip" "docs/2024") ], held)

[<Fact>]
let ``a place kept under a digit that held one takes its slot`` () =
    let favorites = keep 1 (onDisk "elsewhere") sample

    let held = Favorites.placeIn (slot 1) favorites

    Assert.Equal(Some(onDisk "elsewhere"), held)

[<Fact>]
let ``a place whose path breaks a line is not kept`` () =
    let favorites = keep 1 (Place.OnDisk(fp (at "two\nlines"))) sample

    Assert.Equal(sample, favorites)

[<Fact>]
let ``what is formatted is what is parsed back`` () =
    let parsed = Favorites.format sample |> Favorites.parse

    Assert.Equal(sample, parsed)

[<Fact>]
let ``each slot is written under its digit, in the order of the digits`` () =
    let lines = Favorites.format sample

    Assert.Equal<string list>(
        [ "favorite.1=" + at "work"
          "favorite.7=" + at "backup.zip"
          "favorite.7.inside=docs/2024" ],
        lines
    )

[<Fact>]
let ``nothing to read is no favourite at all`` () =
    let parsed = Favorites.parse []

    Assert.Equal(Favorites.empty, parsed)

[<Theory>]
[<InlineData("favorite.10=/elsewhere")>]
[<InlineData("favorite.x=/elsewhere")>]
[<InlineData("favorite.2=")>]
[<InlineData("favorite.2.inside=docs")>]
[<InlineData("unknown.key=value")>]
[<InlineData("no equals sign here")>]
let ``a line that names no slot or no place is passed over`` (line: string) =
    let lines = line :: Favorites.format sample

    let parsed = Favorites.parse lines

    Assert.Equal(sample, parsed)

[<Fact>]
let ``a slot with a level inside an archive that cannot be read costs that slot alone`` () =
    let lines =
        [ "favorite.1=" + at "work"
          "favorite.2=" + at "backup.zip"
          "favorite.2.inside=../outside" ]

    let parsed = Favorites.parse lines

    Assert.Equal(keep 1 (onDisk "work") Favorites.empty, parsed)

[<Fact>]
let ``the favourites sit beside the settings, in a file of their own`` () =
    let fs = MockFileSystem()

    let path = Favorites.beside fs (fp (at "config/Marbas/settings.conf"))

    Assert.Equal(fp (at "config/Marbas/favorites.conf"), path)

[<Fact>]
let ``favourites never written load as none`` () =
    let fs = MockFileSystem()

    let loaded = Favorites.load fs favoritesPath

    Assert.Equal(Ok Favorites.empty, loaded)

[<Fact>]
let ``a change made on top of a file another copy wrote keeps both`` () =
    let fs = MockFileSystem()
    write fs (Favorites.format sample)

    let changed = Favorites.change fs favoritesPath (Keep(slot 4, onDisk "elsewhere"))

    let expected = keep 4 (onDisk "elsewhere") sample
    Assert.Equal(Ok expected, changed)
    Assert.Equal(Ok expected, Favorites.load fs favoritesPath)

[<Fact>]
let ``the first change creates the file and the directory it lives in`` () =
    let fs = MockFileSystem()

    let changed = Favorites.change fs favoritesPath (Keep(slot 2, onDisk "work"))

    let loaded = Favorites.load fs favoritesPath
    Assert.Equal(changed, loaded)
    Assert.Equal(Some(onDisk "work"), Favorites.placeIn (slot 2) (Expect.ok loaded))

[<Fact>]
let ``favourites that cannot be written answer an error naming the file`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "blocked", MockFileData "x")
    let blocked = fp (at "blocked/favorites.conf")

    let changed = Favorites.change fs blocked (Keep(slot 2, onDisk "work"))

    Assert.Equal(blocked, unwritten (Expect.error changed))

[<Fact>]
let ``favourites that cannot be read are answered as an error and left as they were`` () =
    let fs = Fakes.UnreadableFile(FileSystemPath.value favoritesPath)
    write fs (Favorites.format sample)

    let changed = Favorites.change fs favoritesPath (Keep(slot 2, onDisk "work"))

    let kept = fs.GetFile(FileSystemPath.value favoritesPath).TextContents
    Assert.Equal(favoritesPath, unwritten (Expect.error changed))
    Assert.Equal(String.concat "\n" (Favorites.format sample), kept)

[<Fact>]
let ``an emptied slot holds no place`` () =
    let favorites = Favorites.apply (EmptySlot(slot 1)) sample

    let held = Favorites.placeIn (slot 1) favorites

    Assert.Equal(None, held)

[<Fact>]
let ``emptying a slot leaves every other slot as it was`` () =
    let favorites = Favorites.apply (EmptySlot(slot 1)) sample

    let held = Favorites.placeIn (slot 7) favorites

    Assert.Equal(Some(inArchive "backup.zip" "docs/2024"), held)

[<Fact>]
let ``emptying a slot that holds nothing changes nothing`` () =
    let favorites = Favorites.apply (EmptySlot(slot 4)) sample

    Assert.Equal(sample, favorites)

[<Fact>]
let ``a slot emptied is not written to the file`` () =
    let fs = MockFileSystem()
    write fs (Favorites.format sample)

    Favorites.change fs favoritesPath (EmptySlot(slot 1)) |> Expect.ok |> ignore

    let lines = fs.File.ReadAllLines(FileSystemPath.value favoritesPath)
    Assert.DoesNotContain("favorite.1=" + at "work", lines)
