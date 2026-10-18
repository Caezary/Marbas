module Marbas.Tests.Integration.Viewer.FileViewerCodePageTests

open System.Drawing
open System.Text
open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Marbas.Viewing
open Marbas.Ui.Keys
open Marbas.Ui.Viewer
open Marbas.Tests.Integration.Gui
open Marbas.Tests.Integration.Viewer.FileViewerFixture

let private recordingInto (offers: ResizeArray<CodePageOffer>) answer offer =
    offers.Add offer
    answer

[<Fact>]
let ``F8 offers the code page in use and what the viewer settled on`` () =
    let offers = ResizeArray<CodePageOffer>()

    use window =
        viewingWith ignore (recordingInto offers None) (Size(80, 24)) (text [ "first" ])

    press (FileViewer.content window) Key.F8

    Assert.Equal(
        { Detected = { Charset = Utf8; Bom = WithoutBom }
          Current = Automatic },
        Assert.Single offers
    )

[<Fact>]
let ``F8 again offers the code page chosen the time before`` () =
    let offers = ResizeArray<CodePageOffer>()

    use window =
        viewingWith ignore (recordingInto offers (Some(Fixed Cp852))) (Size(80, 24)) latinBytes

    let content = FileViewer.content window
    press content Key.F8

    press content Key.F8

    Assert.Equal(Fixed Cp852, offers[1].Current)

[<Fact>]
let ``every code page reads the same bytes its own way`` () =
    let differing = MockFileData [| 0x80uy; 0xA5uy; 0xB1uy; 0xB9uy |]

    let firstRows =
        CodePage.offered
        |> List.map (fun page ->
            use window = choosing page differing
            let content = FileViewer.content window
            press content Key.F8
            List.head (content.Rows()))

    Assert.Equal<string list>([ ".¥±¹"; "����"; "ꖀ릱"; "肥놹"; ".¥±¹"; "€Ą±ą"; "Çą▒╣"; ".Ľąš"; "€¥±¹"; "ÇÑ▒╣" ], firstRows)

[<Fact>]
let ``a code page that was not chosen leaves the file as it was`` () =
    use window = laidOut latinBytes
    let content = FileViewer.content window

    press content Key.F8

    Assert.Equal(Automatic, content.CodePage)
    Assert.Equal("A¥", List.head (content.Rows()))

[<Fact>]
let ``the header names the code page chosen`` () =
    use window = choosing (Fixed Cp852) latinBytes

    press (FileViewer.content window) Key.F8

    Assert.Contains("CP852", drawnText (FileViewer.header window))

[<Fact>]
let ``a new code page keeps the place in the text`` () =
    use window = choosing (Fixed Windows1250) (numbered 100)
    let content = FileViewer.content window
    press content Key.PageDown

    press content Key.F8

    Assert.Equal(22, content.Top)
    Assert.Equal("line 23", List.head (content.Rows()))

[<Fact>]
let ``a code page with a wider line feed finds the lines it holds`` () =
    use window =
        choosing (Fixed Utf16Le) (MockFileData(Encoding.Unicode.GetBytes "first\nsecond"))

    let content = FileViewer.content window

    press content Key.F8

    Assert.Equal<string list>([ "first"; "second" ], content.Rows())

[<Fact>]
let ``in hex a new code page keeps the row and reads its characters anew`` () =
    use window = choosing (Fixed Cp852) (sixteenByteLines 100)
    let content = FileViewer.content window
    press content Key.F4
    press content Key.PageDown

    press content Key.F8

    Assert.Equal(HexMode, content.Mode)
    Assert.Equal(22, content.Top)

[<Fact>]
let ``in hex the character column follows the code page`` () =
    use window = choosing (Fixed Cp852) latinBytes
    let content = FileViewer.content window
    press content Key.F4

    press content Key.F8

    Assert.EndsWith("Aą", List.head (content.Rows()))

[<Fact>]
let ``the next viewer of the file settles its encoding afresh`` () =
    use chosen = choosing (Fixed Cp852) latinBytes
    press (FileViewer.content chosen) Key.F8

    use next = laidOut latinBytes

    let content = FileViewer.content next
    Assert.Equal(Automatic, content.CodePage)
    Assert.Equal({ Charset = Latin1; Bom = WithoutBom }, content.Encoding)
    Assert.Equal("A¥", List.head (content.Rows()))

[<Fact>]
let ``the key bar offers the code page`` () =
    use window = laidOut (text [ "first" ])

    let labels =
        FunctionKeyBar.slots (FileViewer.keyBar window) |> List.map _.CommandName.Text

    Assert.Equal("Code", labels[7])
