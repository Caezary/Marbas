module Marbas.Tests.Ui.Panels.PanelTitleTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Ui.Panels
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private fs = MockFileSystem() :> IFileSystem

let private start = OnDisk(fp (at "start"))

let private describe key direction =
    PanelTitle.describe fs start (SortOrder.create key direction) Concealed

[<Fact>]
let ``the title is the path followed by the sort order`` () =
    let title = describe SortKey.ByName SortDirection.Ascending

    Assert.Equal(at "start" + " [name ↑]", title)

[<Fact>]
let ``a descending order points the arrow down`` () =
    let title = describe SortKey.ByName SortDirection.Descending

    Assert.Equal(at "start" + " [name ↓]", title)

[<Fact>]
let ``a panel showing hidden entries says so`` () =
    let title = PanelTitle.describe fs start SortOrder.byName Shown

    Assert.Equal(at "start" + " [name ↑ hidden]", title)

[<Fact>]
let ``a panel inside an archive says where it stands in it`` () =
    let inside = ArchivePath.tryCreate "docs" |> Expect.some
    let location = InArchive(fp (at "start/backup.zip"), inside)

    let title = PanelTitle.describe fs location SortOrder.byName Concealed

    Assert.Equal(at "start/backup.zip/docs" + " [name ↑]", title)

[<Fact>]
let ``every sort key has its own label`` () =
    let labels =
        [ SortKey.ByName; SortKey.ByExtension; SortKey.ByDate; SortKey.BySize ]
        |> List.map (fun key -> describe key SortDirection.Ascending)

    Assert.Equal<string list>(
        [ at "start" + " [name ↑]"
          at "start" + " [ext ↑]"
          at "start" + " [date ↑]"
          at "start" + " [size ↑]" ],
        labels
    )
