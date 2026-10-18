module Marbas.Tests.Viewing.ByteSearchTests

open System
open System.IO
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Viewing
open Marbas.Viewing.ByteSearch
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private notes = at "start/data.bin"

let private needle = [| 0xFCuy; 0xFDuy; 0xFEuy; 0xFFuy |]

let private window = LineIndex.ChunkLength

let private filler length =
    Array.init length (fun index -> byte (index % 251))

let private withNeedlesAt (offsets: int list) length =
    let bytes = filler length

    offsets
    |> List.iter (fun offset -> Array.blit needle 0 bytes offset needle.Length)

    bytes

let private sourceOf (bytes: byte array) =
    let fs = MockFileSystem()
    fs.AddFile(notes, MockFileData bytes)
    FileWindow.openFile fs (fp notes) |> Expect.ok

let private going: WindowSearch.Control =
    { Reached = ignore
      IsCancelled = fun () -> false }

let private stopping: WindowSearch.Control =
    { going with
        IsCancelled = fun () -> true }

let private wanted raw = WantedBytes.tryParse raw |> Expect.ok

let private forwards raw : Query =
    { Bytes = wanted raw
      Direction = Forwards }

let private backwards raw : Query =
    { Bytes = wanted raw
      Direction = Backwards }

let private searching control (bytes: byte array) query from =
    use source = sourceOf bytes
    ByteSearch.run control source query from

let private found offset length : WindowSearch.Answer<Match> =
    WindowSearch.Found { Offset = offset; Length = length }

type private BrokenStream() =
    inherit MemoryStream(Array.zeroCreate<byte> 100)

    override _.Read(_: byte array, _: int, _: int) : int =
        raise (IOException "the disk went away")

    override _.Read(_: Span<byte>) : int =
        raise (IOException "the disk went away")

[<Fact>]
let ``a run inside one window of bytes is found where it starts`` () =
    let answer = searching going (withNeedlesAt [ 40 ] 100) (forwards "FC FD FE FF") 0L

    Assert.Equal(found 40L 4, answer)

[<Fact>]
let ``a run at the very start is found`` () =
    let answer = searching going (withNeedlesAt [ 0 ] 100) (forwards "FCFDFEFF") 0L

    Assert.Equal(found 0L 4, answer)

[<Fact>]
let ``a run at the very end is found`` () =
    let answer = searching going (withNeedlesAt [ 96 ] 100) (forwards "FC FD FE FF") 0L

    Assert.Equal(found 96L 4, answer)

[<Fact>]
let ``a run split across two windows of bytes is found`` () =
    let offset = window - 2

    let answer =
        searching going (withNeedlesAt [ offset ] (window + 100)) (forwards "FC FD FE FF") 0L

    Assert.Equal(found (int64 offset) 4, answer)

[<Fact>]
let ``a run past the first window of bytes is found`` () =
    let offset = window * 2 + 7

    let answer =
        searching going (withNeedlesAt [ offset ] (window * 3)) (forwards "FC FD FE FF") 0L

    Assert.Equal(found (int64 offset) 4, answer)

[<Fact>]
let ``a run that is not in the file is not found`` () =
    let answer = searching going (filler 1000) (forwards "FC FD FE FF") 0L

    Assert.Equal(WindowSearch.NotFound, answer)

[<Fact>]
let ``an empty file holds no run`` () =
    let answer = searching going [||] (forwards "00") 0L

    Assert.Equal(WindowSearch.NotFound, answer)

[<Fact>]
let ``a search from an offset passes over a run before it`` () =
    let answer =
        searching going (withNeedlesAt [ 10; 50 ] 100) (forwards "FC FD FE FF") 11L

    Assert.Equal(found 50L 4, answer)

[<Fact>]
let ``a search from where a run starts finds that run`` () =
    let answer =
        searching going (withNeedlesAt [ 10; 50 ] 100) (forwards "FC FD FE FF") 10L

    Assert.Equal(found 10L 4, answer)

[<Fact>]
let ``a search past the last run ends at the end of the file`` () =
    let answer = searching going (withNeedlesAt [ 10 ] 100) (forwards "FC FD FE FF") 11L

    Assert.Equal(WindowSearch.NotFound, answer)

[<Fact>]
let ``a search back finds the last run starting before the offset`` () =
    let answer =
        searching going (withNeedlesAt [ 10; 50 ] 100) (backwards "FC FD FE FF") 50L

    Assert.Equal(found 10L 4, answer)

[<Fact>]
let ``a search back finds a run that starts before the offset and ends after it`` () =
    let answer =
        searching going (withNeedlesAt [ 10; 50 ] 100) (backwards "FC FD FE FF") 51L

    Assert.Equal(found 50L 4, answer)

[<Fact>]
let ``a search back from the end finds the last run`` () =
    let answer =
        searching going (withNeedlesAt [ 10; 96 ] 100) (backwards "FC FD FE FF") 100L

    Assert.Equal(found 96L 4, answer)

[<Fact>]
let ``a search back before the first run ends at the start of the file`` () =
    let answer =
        searching going (withNeedlesAt [ 10 ] 100) (backwards "FC FD FE FF") 10L

    Assert.Equal(WindowSearch.NotFound, answer)

[<Fact>]
let ``a search back finds a run split across two windows of bytes`` () =
    let length = window * 2
    let offset = length - window - 2

    let answer =
        searching going (withNeedlesAt [ offset ] length) (backwards "FC FD FE FF") (int64 length)

    Assert.Equal(found (int64 offset) 4, answer)

[<Fact>]
let ``a search back finds a run before the window it starts in`` () =
    let answer =
        searching going (withNeedlesAt [ 3 ] (window * 3)) (backwards "FC FD FE FF") (int64 (window * 3))

    Assert.Equal(found 3L 4, answer)

[<Fact>]
let ``a search told to stop stops before reading`` () =
    let answer =
        searching stopping (withNeedlesAt [ 10 ] 100) (forwards "FC FD FE FF") 0L

    Assert.Equal(WindowSearch.Stopped, answer)

[<Fact>]
let ``a search back told to stop stops before reading`` () =
    let answer =
        searching stopping (withNeedlesAt [ 10 ] 100) (backwards "FC FD FE FF") 100L

    Assert.Equal(WindowSearch.Stopped, answer)

[<Fact>]
let ``a search says how far it has read`` () =
    let reached = ResizeArray<int64>()

    let watching = { going with Reached = reached.Add }

    searching watching (filler (window + 10)) (forwards "FC FD") 0L |> ignore

    Assert.Equal<int64 list>([ 0L; int64 window - 1L ], List.ofSeq reached)

[<Fact>]
let ``a search back says how far it has read`` () =
    let reached = ResizeArray<int64>()

    let watching = { going with Reached = reached.Add }

    searching watching (filler (window + 10)) (backwards "FC FD") (int64 (window + 10))
    |> ignore

    Assert.Equal<int64 list>([ 10L; 0L ], List.ofSeq reached)

[<Fact>]
let ``a file that fails to read is answered with the failure`` () =
    use source =
        new FileWindow.Source(fp notes, new BrokenStream(), { Charset = Utf8; Bom = WithoutBom })

    let answer = ByteSearch.run going source (forwards "4D") 0L

    Assert.Equal(WindowSearch.Unreadable(ReadFailed(fp notes, "the disk went away")), answer)
