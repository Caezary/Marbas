module Marbas.Tests.FileSystem.RewindingStreamTests

open System.IO
open System.Text
open Xunit
open Marbas.FileSystem

let private content = Encoding.ASCII.GetBytes "abcdefghij"

let private opening () =
    let opens = ref 0

    let openStream () =
        opens.Value <- opens.Value + 1
        new MemoryStream(content) :> Stream

    opens, openStream

let private readAt (stream: Stream) offset count =
    let bytes = Array.zeroCreate count
    stream.Seek(offset, SeekOrigin.Begin) |> ignore
    stream.ReadExactly(bytes, 0, count)
    Encoding.ASCII.GetString bytes

[<Fact>]
let ``the length is the one the entry declared, without the stream being opened`` () =
    let opens, openStream = opening ()

    use stream = new RewindingStream(int64 content.Length, openStream)

    Assert.Equal(10L, stream.Length)
    Assert.Equal(0, opens.Value)

[<Fact>]
let ``reading forward opens the stream once`` () =
    let opens, openStream = opening ()
    use stream = new RewindingStream(int64 content.Length, openStream)

    let first = readAt stream 0L 3
    let second = readAt stream 5L 3

    Assert.Equal("abc", first)
    Assert.Equal("fgh", second)
    Assert.Equal(1, opens.Value)

[<Fact>]
let ``reading backwards opens the stream again`` () =
    let opens, openStream = opening ()
    use stream = new RewindingStream(int64 content.Length, openStream)

    let far = readAt stream 6L 2
    let back = readAt stream 1L 2

    Assert.Equal("gh", far)
    Assert.Equal("bc", back)
    Assert.Equal(2, opens.Value)

[<Fact>]
let ``reading the same place twice opens the stream once`` () =
    let opens, openStream = opening ()
    use stream = new RewindingStream(int64 content.Length, openStream)

    let first = readAt stream 4L 2
    let again = readAt stream 6L 2

    Assert.Equal("ef", first)
    Assert.Equal("gh", again)
    Assert.Equal(1, opens.Value)

[<Fact>]
let ``the position follows what was read`` () =
    let _, openStream = opening ()
    use stream = new RewindingStream(int64 content.Length, openStream)
    let bytes = Array.zeroCreate 4

    stream.ReadExactly(bytes, 0, 4)

    Assert.Equal(4L, stream.Position)

[<Fact>]
let ``a read past the end answers what is there`` () =
    let _, openStream = opening ()
    use stream = new RewindingStream(int64 content.Length, openStream)
    let bytes = Array.zeroCreate 4

    stream.Seek(8L, SeekOrigin.Begin) |> ignore
    let read = stream.Read(bytes, 0, 4)

    Assert.Equal(2, read)
    Assert.Equal("ij", Encoding.ASCII.GetString(bytes, 0, read))

[<Fact>]
let ``seeking from the end counts back from the length`` () =
    let _, openStream = opening ()
    use stream = new RewindingStream(int64 content.Length, openStream)
    let bytes = Array.zeroCreate 2

    stream.Seek(-2L, SeekOrigin.End) |> ignore
    stream.ReadExactly(bytes, 0, 2)

    Assert.Equal("ij", Encoding.ASCII.GetString bytes)

[<Fact>]
let ``a stream that was never read is never opened`` () =
    let opens, openStream = opening ()

    (new RewindingStream(int64 content.Length, openStream) :> Stream).Dispose()

    Assert.Equal(0, opens.Value)
