module Marbas.Tests.FileSystem.WindowSearchTests

open System
open System.IO
open System.Text
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.WindowSearch
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private notes = at "start/notes.txt"

let private sourceOf (data: MockFileData) =
    let fs = MockFileSystem()
    fs.AddFile(notes, data)
    FileWindow.openFile fs (fp notes) |> Expect.ok

let private wanted raw = FindText.tryCreate raw |> Option.get

let private going: Control =
    { Reached = ignore
      IsCancelled = fun () -> false }

let private searching (content: string) raw from =
    use source = sourceOf (MockFileData content)
    forward going source source.Encoding (wanted raw) from

type private BrokenStream() =
    inherit MemoryStream(Array.zeroCreate<byte> 100)

    override _.Read(_: byte array, _: int, _: int) : int = raise (IOException "the disk went away")

    override _.Read(_: Span<byte>) : int = raise (IOException "the disk went away")

[<Fact>]
let ``text on the line it starts from is found there`` () =
    let answer = searching "alpha beta\ngamma" "beta" 0L

    Assert.Equal(Found { LineStart = 0L; Column = 6; Length = 4 }, answer)

[<Fact>]
let ``text further down is found on its own line`` () =
    let answer = searching "alpha\nbeta gamma\ndelta" "gamma" 0L

    Assert.Equal(Found { LineStart = 6L; Column = 5; Length = 5 }, answer)

[<Fact>]
let ``a plain search does not mind the letter case`` () =
    let answer = searching "alpha\nGamma" "gAMMA" 0L

    Assert.Equal(Found { LineStart = 6L; Column = 0; Length = 5 }, answer)

[<Fact>]
let ``the first of several matches is the one found`` () =
    let answer = searching "one\ntwo two\ntwo" "two" 0L

    Assert.Equal(Found { LineStart = 4L; Column = 0; Length = 3 }, answer)

[<Fact>]
let ``what lies before the line the search starts from is not found`` () =
    let answer = searching "needle\nhay\nneedle" "needle" 4L

    Assert.Equal(Found { LineStart = 11L; Column = 0; Length = 6 }, answer)

[<Fact>]
let ``text past the first window of bytes is found`` () =
    let filler = String.replicate 5000 "hay and more hay\n"
    let content = filler + "the needle\n"

    let answer = searching content "needle" 0L

    Assert.Equal(Found { LineStart = int64 filler.Length; Column = 4; Length = 6 }, answer)

[<Fact>]
let ``text that is not in the file is not found`` () =
    let answer = searching "alpha\nbeta" "gamma" 0L

    Assert.Equal(NotFound, answer)

[<Fact>]
let ``text is not matched across a line break`` () =
    let answer = searching "alpha\nbeta" "a b" 0L

    Assert.Equal(NotFound, answer)

[<Fact>]
let ``an empty file holds nothing`` () =
    let answer = searching "" "gamma" 0L

    Assert.Equal(NotFound, answer)

[<Fact>]
let ``text is matched in the encoding it is read in`` () =
    use source = sourceOf (MockFileData [| 0x41uy; 0x0Auy; 0x42uy; 0xA5uy |])
    let encoding = { Charset = Windows1250; Bom = WithoutBom }

    let answer = forward going source encoding (wanted "Ą") 0L

    Assert.Equal(Found { LineStart = 2L; Column = 1; Length = 1 }, answer)

[<Fact>]
let ``a wide file is searched by its characters`` () =
    let wide = Array.append (Encoding.Unicode.GetPreamble()) (Encoding.Unicode.GetBytes "first\nthe needle")
    use source = sourceOf (MockFileData wide)

    let answer = forward going source source.Encoding (wanted "needle") 0L

    Assert.Equal(Found { LineStart = 14L; Column = 4; Length = 6 }, answer)

[<Fact>]
let ``a search told to stop stops before reading`` () =
    use source = sourceOf (MockFileData "the needle")

    let stopping =
        { going with
            IsCancelled = fun () -> true }

    let answer = forward stopping source source.Encoding (wanted "needle") 0L

    Assert.Equal(Stopped, answer)

[<Fact>]
let ``a search says how far it has read`` () =
    use source = sourceOf (MockFileData(String('a', LineIndex.ChunkLength + 10)))
    let reached = ResizeArray<int64>()

    let watching =
        { going with
            Reached = reached.Add }

    forward watching source source.Encoding (wanted "needle") 0L |> ignore

    Assert.Equal<int64 list>([ 0L; int64 LineIndex.ChunkLength ], List.ofSeq reached)

[<Fact>]
let ``a file that fails to read is answered with the failure`` () =
    use source = new FileWindow.Source(fp notes, new BrokenStream(), { Charset = Utf8; Bom = WithoutBom })

    let answer = forward going source source.Encoding (wanted "needle") 0L

    Assert.Equal(Unreadable(ReadFailed(fp notes, "the disk went away")), answer)
