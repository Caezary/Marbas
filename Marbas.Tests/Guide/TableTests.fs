module Marbas.Tests.Guide.TableTests

open Xunit
open Marbas.Guide

let private cell text = Cell(Phrase [ Text text ])

let private empty = Cell(Phrase [])

let private header = Row [ cell "Key"; cell "Alternative"; cell "Action" ]

[<Fact>]
let ``a row as wide as the header is kept`` () =
    let row = Row [ cell "Ins"; cell "Space"; cell "Mark" ]

    let table = Table.create header [ row ]

    Assert.Equal<Row list>([ row ], Table.body table)

[<Fact>]
let ``a shorter row is padded with empty cells`` () =
    let table = Table.create header [ Row [ cell "Ins" ] ]

    Assert.Equal<Row list>([ Row [ cell "Ins"; empty; empty ] ], Table.body table)

[<Fact>]
let ``a longer row is cut to the header`` () =
    let table =
        Table.create header [ Row [ cell "Ins"; cell "Space"; cell "Mark"; cell "extra" ] ]

    Assert.Equal<Row list>([ Row [ cell "Ins"; cell "Space"; cell "Mark" ] ], Table.body table)

[<Fact>]
let ``the header is kept as given`` () =
    let table = Table.create header []

    Assert.Equal(header, Table.header table)
