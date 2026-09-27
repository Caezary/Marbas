module Marbas.Tests.FileSystem.PanelLocationTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private inside raw = ArchivePath.tryCreate raw |> Expect.some

let private fs = MockFileSystem() :> IFileSystem

let private archive = fp (at "start/backup.zip")

[<Fact>]
let ``a location on disk is the directory it names`` () =
    let location = OnDisk(fp (at "start"))

    Assert.Equal(Some(fp (at "start")), PanelLocation.directory location)

[<Fact>]
let ``a location inside an archive names no directory on disk`` () =
    let location = InArchive(archive, inside "docs")

    Assert.Equal(None, PanelLocation.directory location)

[<Fact>]
let ``a location on disk is held by itself`` () =
    let location = OnDisk(fp (at "start"))

    Assert.Equal(fp (at "start"), PanelLocation.holding fs location)

[<Fact>]
let ``a location inside an archive is held by the directory holding the archive`` () =
    let location = InArchive(archive, inside "docs/deep")

    Assert.Equal(fp (at "start"), PanelLocation.holding fs location)

[<Fact>]
let ``a location on disk is spelled by its path`` () =
    let location = OnDisk(fp (at "start"))

    Assert.Equal(at "start", PanelLocation.value fs location)

[<Fact>]
let ``the top level of an archive is spelled by the archive itself`` () =
    let location = InArchive(archive, ArchivePath.root)

    Assert.Equal(at "start/backup.zip", PanelLocation.value fs location)

[<Fact>]
let ``a level inside an archive is spelled below the archive`` () =
    let location = InArchive(archive, inside "docs/deep")

    Assert.Equal(at "start/backup.zip/docs/deep", PanelLocation.value fs location)

[<Fact>]
let ``a name in a location on disk is a path in that directory`` () =
    let location = OnDisk(fp (at "start"))

    Assert.Equal(Some(fp (at "start/notes.txt")), PanelLocation.combine fs location "notes.txt")

[<Fact>]
let ``a name in a location inside an archive is no path on disk`` () =
    let location = InArchive(archive, ArchivePath.root)

    Assert.Equal(None, PanelLocation.combine fs location "notes.txt")

let private entry name =
    { Name = EntryName.create name
      Kind = File
      Size = Some 2L
      Measured = None
      Modified = System.DateTimeOffset.MinValue
      Visibility = Plain
      Executability = NotExecutable
      LinkTarget = None
      Verification = Verified }

let private hitIn where name =
    { Location = OnDisk(fp (at where))
      Entry = entry name }

let private groundOf roots =
    { Roots = roots |> List.map (fp >> OnDisk)
      OtherRoots = [] }

[<Fact>]
let ``a hit found below the one root it was searched from is shown by its path under it`` () =
    let ground = groundOf [ at "start" ]

    let shown = FindHit.displayName fs ground (hitIn "start/docs" "inner.txt")

    Assert.Equal(EntryName.create (Root.relative "docs/inner.txt"), shown)

[<Fact>]
let ``a hit found over several roots is shown by its whole path`` () =
    let ground = groundOf [ at "start"; at "work" ]

    let shown = FindHit.displayName fs ground (hitIn "start/docs" "inner.txt")

    Assert.Equal(EntryName.create (at "start/docs/inner.txt"), shown)

[<Fact>]
let ``the hits are indexed by the name each is shown under`` () =
    let ground = groundOf [ at "start" ]
    let hit = hitIn "start/docs" "inner.txt"

    let index = FoundEntries.shownBy fs ground [ hit; hitIn "start" "notes.txt" ]

    Assert.Equal(Some hit, index |> Map.tryFind (EntryName.create (Root.relative "docs/inner.txt")))
    Assert.Equal(2, index.Count)

[<Fact>]
let ``a name at a level inside an archive has its place in that archive`` () =
    let location = InArchive(archive, inside "docs")

    let place = PanelLocation.archivePlace location (EntryName.create "notes.txt")

    Assert.Equal(Some(archive, inside "docs/notes.txt"), place)

[<Fact>]
let ``a name on disk has no place in an archive`` () =
    let location = OnDisk(fp (at "start"))

    let place = PanelLocation.archivePlace location (EntryName.create "notes.txt")

    Assert.Equal(None, place)

[<Fact>]
let ``a hit inside an archive has its place in that archive`` () =
    let hit =
        { Location = InArchive(archive, ArchivePath.root)
          Entry = entry "notes.txt" }

    let place = FindHit.archivePlace hit

    Assert.Equal(Some(archive, inside "notes.txt"), place)

[<Fact>]
let ``a hit on disk has no place in an archive`` () =
    let place = FindHit.archivePlace (hitIn "start" "notes.txt")

    Assert.Equal(None, place)
