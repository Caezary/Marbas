module Marbas.Tests.Ui.FileViewTests

open Xunit
open Marbas.FileSystem.FileContent
open Marbas.Ui

let private whole body = { Body = body; Extent = Whole }

[<Fact>]
let ``text is shown as it was read`` () =
    let content = whole (Text [ "first"; "second" ])

    let lines = FileView.lines content

    Assert.Equal<string list>([ "first"; "second" ], lines)

[<Fact>]
let ``bytes are shown as a hex dump`` () =
    let content = whole (Binary "Hi"B)

    let lines = FileView.lines content

    Assert.Equal<string list>(HexDump.describe "Hi"B, lines)

[<Fact>]
let ``a file cut short says so on the last line`` () =
    let content =
        { Body = Text [ "first" ]
          Extent = Truncated }

    let lines = FileView.lines content

    Assert.Equal<string list>([ "first"; FileView.TruncatedNotice ], lines)
