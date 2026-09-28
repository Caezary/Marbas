module Marbas.Tests.Ui.Dialogs.HistoryRowTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Ui.Dialogs
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private now = DateTimeOffset(2026, 9, 28, 15, 30, 0, TimeSpan.FromHours 2.0)

[<Fact>]
let ``a moment of today is shown as its time`` () =
    let shown = HistoryRow.moment now (DateTimeOffset(2026, 9, 28, 9, 5, 0, TimeSpan.FromHours 2.0))

    Assert.Equal("09:05", shown)

[<Fact>]
let ``a moment of an earlier day is shown as its date`` () =
    let shown = HistoryRow.moment now (DateTimeOffset(2026, 9, 25, 23, 50, 0, TimeSpan.FromHours 2.0))

    Assert.Equal("2026-09-25", shown)

[<Fact>]
let ``a moment is read in the offset of now, not in the one it was written with`` () =
    let shown = HistoryRow.moment now (DateTimeOffset(2026, 9, 27, 23, 0, 0, TimeSpan.Zero))

    Assert.Equal("01:00", shown)

[<Fact>]
let ``a directory on disk is shown as its path`` () =
    let shown = HistoryRow.place (MockFileSystem()) (Place.OnDisk(fp (at "work/docs")))

    Assert.Equal(at "work/docs", shown)

[<Fact>]
let ``a level inside an archive is shown as the archive's path followed by the level`` () =
    let inside = Expect.some (ArchivePath.tryCreate "docs/2024")

    let shown = HistoryRow.place (MockFileSystem()) (Place.InArchive(fp (at "backup.zip"), inside))

    Assert.Equal(at "backup.zip/docs/2024", shown)

[<Fact>]
let ``the top of an archive is shown as the archive's path`` () =
    let shown =
        HistoryRow.place (MockFileSystem()) (Place.InArchive(fp (at "backup.zip"), ArchivePath.root))

    Assert.Equal(at "backup.zip", shown)
