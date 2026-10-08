module Marbas.Tests.Ui.Viewer.RowBytesTests

open Xunit
open Marbas.Ui.Viewer

[<Fact>]
let ``a row in the middle carries the bytes on either side of it`` () =
    let window = [| 0uy .. 39uy |]

    let row = RowBytes.within window 16 16

    Assert.Equal<byte array>([| 13uy; 14uy; 15uy |], row.Before)
    Assert.Equal<byte array>([| 16uy .. 31uy |], row.Bytes)
    Assert.Equal<byte array>([| 32uy; 33uy; 34uy |], row.After)

[<Fact>]
let ``the first row has nothing before it`` () =
    let window = [| 0uy .. 19uy |]

    let row = RowBytes.within window 0 16

    Assert.Empty row.Before
    Assert.Equal<byte array>([| 0uy .. 15uy |], row.Bytes)

[<Fact>]
let ``a short last row has nothing after it`` () =
    let window = [| 0uy .. 19uy |]

    let row = RowBytes.within window 16 16

    Assert.Equal<byte array>([| 16uy .. 19uy |], row.Bytes)
    Assert.Empty row.After
