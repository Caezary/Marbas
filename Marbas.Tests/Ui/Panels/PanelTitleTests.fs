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

let private Roomy = 100

let private describe key direction =
    PanelTitle.describe fs Roomy start (SortOrder.create key direction) Concealed

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
    let title = PanelTitle.describe fs Roomy start SortOrder.byName Shown

    Assert.Equal(at "start" + " [name ↑ hidden]", title)

[<Fact>]
let ``a panel inside an archive says where it stands in it`` () =
    let inside = ArchivePath.tryCreate "docs" |> Expect.some
    let location = InArchive(fp (at "start/backup.zip"), inside)

    let title = PanelTitle.describe fs Roomy location SortOrder.byName Concealed

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

[<Fact>]
let ``a path too long for the width is shortened, and the sort marks stay`` () =
    let deep = OnDisk(fp (at "start/projects/work/marbas/source"))

    let title = PanelTitle.describe fs 24 deep SortOrder.byName Concealed

    Assert.EndsWith(" [name ↑]", title)
    Assert.True(title.Length <= 24, $"the title '{title}' is wider than the panel")
    Assert.Contains("...", title)
    Assert.Contains("source", title)

[<Fact>]
let ``a path that fits the width is left whole`` () =
    let title = PanelTitle.describe fs 24 start SortOrder.byName Concealed

    Assert.Equal(at "start" + " [name ↑]", title)
