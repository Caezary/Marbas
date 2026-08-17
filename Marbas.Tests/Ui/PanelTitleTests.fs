module Marbas.Tests.Ui.PanelTitleTests

open Xunit
open Marbas.FileSystem
open Marbas.Ui
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private describe key direction =
    PanelTitle.describe (fp (at "start")) (SortOrder.create key direction) Concealed

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
    let title = PanelTitle.describe (fp (at "start")) SortOrder.byName Shown

    Assert.Equal(at "start" + " [name ↑ hidden]", title)

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
