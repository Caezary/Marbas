module Marbas.Tests.FileSystem.PanelLocationTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
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
