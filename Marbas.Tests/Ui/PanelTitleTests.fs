module Marbas.Tests.Ui.PanelTitleTests

open Xunit
open Marbas.FileSystem
open Marbas.Ui

let private fp = FileSystemPath.create

let private describe key direction =
    PanelTitle.describe (fp @"C:\start") (SortOrder.create key direction) Concealed

[<Fact>]
let ``the title is the path followed by the sort order`` () =
    let title = describe SortKey.ByName SortDirection.Ascending

    Assert.Equal(@"C:\start [name ↑]", title)

[<Fact>]
let ``a descending order points the arrow down`` () =
    let title = describe SortKey.ByName SortDirection.Descending

    Assert.Equal(@"C:\start [name ↓]", title)

[<Fact>]
let ``a panel showing hidden entries says so`` () =
    let title = PanelTitle.describe (fp @"C:\start") SortOrder.byName Shown

    Assert.Equal(@"C:\start [name ↑ hidden]", title)

[<Fact>]
let ``every sort key has its own label`` () =
    let labels =
        [ SortKey.ByName; SortKey.ByExtension; SortKey.ByDate; SortKey.BySize ]
        |> List.map (fun key -> describe key SortDirection.Ascending)

    Assert.Equal<string list>(
        [ @"C:\start [name ↑]"
          @"C:\start [ext ↑]"
          @"C:\start [date ↑]"
          @"C:\start [size ↑]" ],
        labels
    )
