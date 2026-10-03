module Marbas.Tests.State.FavoriteTests

open System
open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private slot digit =
    Expect.some (FavoriteSlot.tryCreate digit)

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddDirectory(at "backup")
    fs.AddDirectory(at "elsewhere")
    fs

let private loadedBoth (fs: IFileSystem) =
    create (fp (at "start"))
    |> loadPanel fs Left (fp (at "start"))
    |> Expect.ok
    |> loadPanel fs Right (fp (at "backup"))
    |> Expect.ok

let private hitIn where name : FindHit =
    { Location = OnDisk(fp (at where))
      Entry =
        { Name = EntryName.create name
          Kind = File
          Size = None
          Measured = None
          Modified = DateTimeOffset.MinValue
          Visibility = Plain
          Executability = NotExecutable
          LinkTarget = None
          Verification = Verified } }

let private results hits =
    { Query =
        { What = FindWhat.create (NameMasks.read "*.txt") None FilesOnly
          Scope = FromDirectory
          Hidden = Concealed
          Links = StopAtLinks
          Archives = SkipArchives }
      Ground =
        { Roots = [ OnDisk(fp (at "start")) ]
          OtherRoots = [] }
      Hits = hits
      Outcome = FileSearch.Finished { Files = List.length hits; Directories = 0; Skipped = 0 }
      Cursor = 0 }

[<Fact>]
let ``keeping asks for the slot of the directory the active panel stands in`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> activate Right

    let result = update fs FavoriteKeepRequested state

    Assert.Equal<Effect list>([ PromptForFavoriteSlot(Place.OnDisk(fp (at "backup"))) ], result.Effects)

[<Fact>]
let ``keeping inside an archive asks for the slot of the level the panel stands on`` () =
    let fs = ArchiveFixture.packed ()
    let docs = Expect.some (ArchivePath.tryCreate "docs")
    let loaded = ArchiveFixture.loaded fs |> activate Left

    let state =
        standInside fs Left (InArchive(ArchiveFixture.archive, docs)) loaded
        |> Expect.ok

    let result = update fs FavoriteKeepRequested state

    Assert.Equal<Effect list>([ PromptForFavoriteSlot(Place.InArchive(ArchiveFixture.archive, docs)) ], result.Effects)

[<Fact>]
let ``a panel of search results keeps nothing`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let showing =
        (update fs (SearchAnswered { Landing = Some InPanel; Results = results [ hitIn "start" "notes.txt" ] }) state)
            .State

    let result = update fs FavoriteKeepRequested showing

    Assert.Empty result.Effects

[<Fact>]
let ``going to a favourite looks up what its slot holds`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let result = update fs (FavoriteRequested(slot 4)) state

    Assert.Equal<Effect list>([ LookUpFavorite(slot 4) ], result.Effects)

[<Fact>]
let ``a favourite found is where the active panel stands`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> activate Right

    let result = update fs (FavoriteFound(Place.OnDisk(fp (at "elsewhere")))) state

    Assert.Equal(OnDisk(fp (at "elsewhere")), result.State.Right.Path)
    Assert.Equal(OnDisk(fp (at "start")), result.State.Left.Path)

[<Fact>]
let ``a favourite found inside an archive is walked back into`` () =
    let fs = ArchiveFixture.packed ()
    let docs = Expect.some (ArchivePath.tryCreate "docs")
    let state = ArchiveFixture.loaded fs |> activate Left

    let result = update fs (FavoriteFound(Place.InArchive(ArchiveFixture.archive, docs))) state

    Assert.Equal(InArchive(ArchiveFixture.archive, docs), result.State.Left.Path)

[<Fact>]
let ``going to a favourite is remembered in the folder history`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let result = update fs (FavoriteFound(Place.OnDisk(fp (at "elsewhere")))) state

    Assert.Contains(ChangeHistory(Remember(Visited(Place.OnDisk(fp (at "elsewhere"))))), result.Effects)

[<Fact>]
let ``a favourite that is gone is reported and the panel stays where it was`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let result = update fs (FavoriteFound(Place.OnDisk(fp (at "gone")))) state

    Assert.Equal(OnDisk(fp (at "start")), result.State.Left.Path)
    Assert.Equal<Effect list>([ ReportListingError(DirectoryNotFound(fp (at "gone"))) ], result.Effects)
