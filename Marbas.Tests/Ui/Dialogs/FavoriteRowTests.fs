module Marbas.Tests.Ui.Dialogs.FavoriteRowTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.Dialogs
open Marbas.Ui.Format
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private slot digit =
    Expect.some (FavoriteSlot.tryCreate digit)

[<Fact>]
let ``a slot holding a directory shows its digit and the path`` () =
    let row = FavoriteRow.describe (MockFileSystem()) 60 (slot 3) (Some(Place.OnDisk(fp (at "work"))))

    Assert.Equal("3  " + at "work", row)

[<Fact>]
let ``an empty slot is its digit and nothing else`` () =
    let row = FavoriteRow.describe (MockFileSystem()) 60 (slot 0) None

    Assert.Equal("0", row)

[<Fact>]
let ``a level inside an archive shows the archive's path and the level`` () =
    let place = Place.InArchive(fp (at "backup.zip"), Expect.some (ArchivePath.tryCreate "docs/2024"))

    let row = FavoriteRow.describe (MockFileSystem()) 60 (slot 7) (Some place)

    Assert.Equal("7  " + at "backup.zip/docs/2024", row)

[<Fact>]
let ``a path wider than the row is shortened in the middle`` () =
    let place = Place.OnDisk(fp (at "projects/clients/northwind/reports/quarterly/2026/drafts"))

    let row = FavoriteRow.describe (MockFileSystem()) 30 (slot 5) (Some place)

    Assert.True(row.Length <= 30, $"the row '{row}' is wider than 30")
    Assert.StartsWith("5  ", row)
    Assert.EndsWith(PathText.Ellipsis + Root.relative "/2026/drafts", row)
