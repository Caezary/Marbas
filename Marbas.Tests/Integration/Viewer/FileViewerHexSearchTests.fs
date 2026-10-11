module Marbas.Tests.Integration.Viewer.FileViewerHexSearchTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.Viewer
open Marbas.Tests
open Marbas.Tests.Integration.Gui
open Marbas.Tests.Integration.Viewer.FileViewerFixture

let private needle = [| 0xFCuy; 0xFDuy; 0xFEuy; 0xFFuy |]

let private binaryWithNeedlesAt (offsets: int list) length =
    let bytes = Array.init length (fun index -> byte (index % 251))

    offsets
    |> List.iter (fun offset -> Array.blit needle 0 bytes offset needle.Length)

    MockFileData bytes

let private bytesAnswer raw =
    { plainAnswer raw with
        TypedAs = AsBytes }

let private searchedInHex ports data =
    let window = searching ports data
    let content = FileViewer.content window
    press content Key.F4
    press content Key.F7
    window

let private marksOf row first last =
    [ row, HexDump.hexColumn first, HexDump.hexColumn last + 2 - HexDump.hexColumn first
      row, HexDump.textColumn first, last - first + 1 ]

let private wantedBytes raw = WantedBytes.tryParse raw |> Expect.ok

[<Fact>]
let ``a run of bytes on the screen is marked in both columns`` () =
    use window =
        searchedInHex (answeringWith (bytesAnswer "FC FD FE FF")) (binaryWithNeedlesAt [ 40 ] 1000)

    let content = FileViewer.content window

    Assert.Equal(0, content.Top)
    Assert.Equal<(int * int * int) list>(marksOf 2 8 11, content.FoundOnScreen())

[<Fact>]
let ``a run split across two windows of bytes comes to the top`` () =
    let offset = LineIndex.ChunkLength - 2

    use window =
        searchedInHex (answeringWith (bytesAnswer "FC FD FE FF")) (binaryWithNeedlesAt [ offset ] (offset + 1000))

    let content = FileViewer.content window

    Assert.Equal(offset / 16, content.Top)
    Assert.Equal<(int * int * int) list>(marksOf 0 14 15 @ marksOf 1 0 1, content.FoundOnScreen())

[<Fact>]
let ``Shift+F7 in hex goes on to the next run`` () =
    use window =
        searchedInHex (answeringWith (bytesAnswer "FC FD FE FF")) (binaryWithNeedlesAt [ 40; 80 ] 1000)

    let content = FileViewer.content window

    again content

    Assert.Equal<(int * int * int) list>(marksOf 5 0 3, content.FoundOnScreen())

[<Fact>]
let ``bytes searched back start from the bottom of the screen`` () =
    let answer =
        { bytesAnswer "FC FD FE FF" with
            Direction = Backwards }

    use window =
        searchedInHex (answeringWith answer) (binaryWithNeedlesAt [ 40; 80 ] 1000)

    let content = FileViewer.content window

    Assert.Equal<(int * int * int) list>(marksOf 5 0 3, content.FoundOnScreen())

[<Fact>]
let ``Shift+F7 in hex goes on backwards`` () =
    let answer =
        { bytesAnswer "FC FD FE FF" with
            Direction = Backwards }

    use window =
        searchedInHex (answeringWith answer) (binaryWithNeedlesAt [ 40; 80 ] 1000)

    let content = FileViewer.content window

    again content

    Assert.Equal<(int * int * int) list>(marksOf 2 8 11, content.FoundOnScreen())

[<Fact>]
let ``bytes that are not in the file are reported`` () =
    let reports = ResizeArray<SearchReport>()

    let ports =
        { answeringWith (bytesAnswer "FC FD FE FF") with
            Report = reports.Add }

    use window = searchedInHex ports (binaryWithNeedlesAt [] 1000)

    Assert.Equal(BytesNotFound(wantedBytes "FC FD FE FF"), Assert.Single reports)

[<Fact>]
let ``a field that is not bytes is reported before anything is read`` () =
    let reports = ResizeArray<SearchReport>()
    let remembered = ResizeArray<EnteredText>()
    let runs = ResizeArray<unit>()

    let ports =
        { answeringWith (bytesAnswer "MZ") with
            Report = reports.Add
            ByteSearches.Remember = remembered.Add
            RunByteSearch =
                fun _ ->
                    runs.Add()
                    WindowSearch.NotFound }

    use window = searchedInHex ports (binaryWithNeedlesAt [ 40 ] 1000)

    Assert.Equal(Refused(BrokenBytes(NotAByte "MZ")), Assert.Single reports)
    Assert.Empty runs
    Assert.Empty remembered

[<Fact>]
let ``bytes searched for are remembered in their own list`` () =
    let bytesRemembered = ResizeArray<EnteredText>()
    let textRemembered = ResizeArray<EnteredText>()

    let ports =
        { answeringWith (bytesAnswer "fc fd") with
            Searches.Remember = textRemembered.Add
            ByteSearches.Remember = bytesRemembered.Add }

    use window = searchedInHex ports (binaryWithNeedlesAt [ 40 ] 1000)

    Assert.Equal(typed "fc fd", Assert.Single bytesRemembered)
    Assert.Empty textRemembered

[<Fact>]
let ``F7 in hex again offers the last bytes`` () =
    let offers = ResizeArray<SearchOffer>()

    let ports =
        { quietPorts with
            AskSearch = recordingOffers offers (Some(bytesAnswer "FC FD")) }

    use window = searchedInHex ports (binaryWithNeedlesAt [ 40 ] 1000)

    press (FileViewer.content window) Key.F7

    Assert.Equal(
        LastBytes(
            Some
                { Bytes = wantedBytes "FC FD"
                  Direction = Forwards }
        ),
        offers[1].Last
    )

[<Fact>]
let ``a run marked in hex is not marked in text`` () =
    use window =
        searchedInHex (answeringWith (bytesAnswer "FC FD FE FF")) (binaryWithNeedlesAt [ 40 ] 1000)

    let content = FileViewer.content window

    press content Key.F4

    Assert.Empty(content.FoundOnScreen())

[<Fact>]
let ``choosing a code page keeps the mark in hex`` () =
    let ports =
        { answeringWith (bytesAnswer "FC FD FE FF") with
            ChooseCodePage = fun _ -> Some(Fixed Windows1250) }

    use window = searchedInHex ports (binaryWithNeedlesAt [ 40 ] 1000)
    let content = FileViewer.content window

    press content Key.F8

    Assert.Equal<(int * int * int) list>(marksOf 2 8 11, content.FoundOnScreen())
