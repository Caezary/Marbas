module Marbas.Tests.Ui.Viewer.ViewerHeaderTests

open Xunit
open Marbas.Ui.Viewer
open Marbas.Ui.Viewer.ViewerHeader
open Marbas.Tests

let private at = Root.at

let private bar =
    { Name = at "start/notes.txt"
      Mode = TextMode
      Encoding = "UTF-8"
      Length = 12480L
      Place = AtLine 411
      Percent = 47 }

[<Fact>]
let ``the bar names the file, the mode, the encoding, the size, the line and how far in it is`` () =
    let shown = describe 80 bar

    Assert.StartsWith(at "start/notes.txt", shown)
    Assert.EndsWith("Text  UTF-8  12,480 B  line 412  47%", shown)

[<Fact>]
let ``the bar fills the width it was given`` () =
    let shown = describe 80 bar

    Assert.Equal(80, shown.Length)

[<Fact>]
let ``lines are counted from one`` () =
    let shown = describe 80 { bar with Place = AtLine 0 }

    Assert.Contains("line 1", shown)

[<Fact>]
let ``in hex the place is an offset, not a line`` () =
    let shown = describe 80 { bar with Mode = HexMode; Place = AtOffset 41408L }

    Assert.EndsWith("Hex  UTF-8  12,480 B  0000A1C0  47%", shown)

[<Fact>]
let ``a name too long for the bar keeps its end`` () =
    let shown = describe 60 { bar with Name = at "start/a/very/deeply/nested/place/of/its/own/notes.txt" }

    Assert.Equal(60, shown.Length)
    Assert.Contains(Ellipsis, shown)
    Assert.Contains("notes.txt", shown)

[<Fact>]
let ``a bar with no room for a name still says where it is`` () =
    let shown = describe 20 bar

    Assert.Equal(20, shown.Length)
    Assert.EndsWith("47%", shown)
