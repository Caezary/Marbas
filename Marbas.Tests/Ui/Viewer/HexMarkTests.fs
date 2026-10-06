module Marbas.Tests.Ui.Viewer.HexMarkTests

open Xunit
open Marbas.FileSystem
open Marbas.Ui.Viewer

let private hit offset length : ByteSearch.Match = { Offset = offset; Length = length }

let private hexFrom index = HexDump.hexColumn index

let private hexUntil index = HexDump.hexColumn index + 2

let private textFrom index = HexDump.textColumn index

let private textUntil index = HexDump.textColumn index + 1

[<Fact>]
let ``a run inside one half is marked once in the hex and once in the text`` () =
    let spans = HexMark.spans (hit 2L 3) 0

    Assert.Equal<(int * int) list>([ hexFrom 2, hexUntil 4; textFrom 2, textUntil 4 ], spans)

[<Fact>]
let ``a run across the rule is marked on either side of it`` () =
    let spans = HexMark.spans (hit 22L 4) 1

    Assert.Equal<(int * int) list>([ hexFrom 6, hexUntil 7; hexFrom 8, hexUntil 9; textFrom 6, textUntil 9 ], spans)

[<Fact>]
let ``a run from the row above is marked where it ends`` () =
    let spans = HexMark.spans (hit 14L 4) 1

    Assert.Equal<(int * int) list>([ hexFrom 0, hexUntil 1; textFrom 0, textUntil 1 ], spans)

[<Fact>]
let ``a run that goes on to the next row is marked to the end of this one`` () =
    let spans = HexMark.spans (hit 14L 4) 0

    Assert.Equal<(int * int) list>([ hexFrom 14, hexUntil 15; textFrom 14, textUntil 15 ], spans)

[<Fact>]
let ``a run over a whole row marks both halves and the whole text`` () =
    let spans = HexMark.spans (hit 0L 48) 1

    Assert.Equal<(int * int) list>([ hexFrom 0, hexUntil 7; hexFrom 8, hexUntil 15; textFrom 0, textUntil 15 ], spans)

[<Theory>]
[<InlineData(0)>]
[<InlineData(2)>]
let ``a row the run does not reach is not marked`` (row: int) =
    let spans = HexMark.spans (hit 20L 4) row

    Assert.Empty spans
