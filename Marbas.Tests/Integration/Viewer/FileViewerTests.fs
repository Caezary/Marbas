module Marbas.Tests.Integration.Viewer.FileViewerTests

open System
open System.Drawing
open System.Text
open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Drawing
open Terminal.Gui.Input
open Terminal.Gui.Views
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.Keys
open Marbas.Ui.Viewer
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private fp = FileSystemPath.create

let private notes = at "start/notes.txt"

let private sourceOf (data: MockFileData) =
    let fs = MockFileSystem()
    fs.AddFile(notes, data)
    FileWindow.openFile fs (fp notes) |> Expect.ok

let private quietPorts: FileViewer.Ports =
    { Close = ignore
      ChooseCodePage = fun _ -> None
      AskSearch = fun _ -> None
      Searches =
        { Texts = fun () -> []
          Remember = ignore }
      RunSearch =
        fun work ->
            work
                { Reached = ignore
                  IsCancelled = fun () -> false }
      Report = ignore }

let private viewingThrough ports size (data: MockFileData) =
    let window = FileViewer.create ports (sourceOf data)
    window.BeginInit()
    window.EndInit()
    window.Layout(size) |> ignore
    window

let private viewingWith close choose size data =
    viewingThrough
        { quietPorts with
            Close = close
            ChooseCodePage = choose }
        size
        data

let private viewing close size data =
    viewingThrough { quietPorts with Close = close } size data

let private choosing page data =
    viewingWith ignore (fun _ -> Some page) (Size(80, 24)) data

let private laidOut data = viewing ignore (Size(80, 24)) data

let private text (lines: string seq) = MockFileData(String.concat "\n" lines)

let private numbered count =
    text [ for line in 1..count -> $"line {line}" ]

let private sixteenByteLines count =
    text [ for line in 1..count -> line.ToString "000000000000000" ]

let private latinBytes = MockFileData [| 0x41uy; 0xA5uy |]

let private recordingInto (offers: ResizeArray<CodePageOffer>) answer offer =
    offers.Add offer
    answer

[<Fact>]
let ``the viewer wears no border`` () =
    use window = laidOut (text [ "first" ])

    Assert.Equal(LineStyle.None, window.BorderStyle.Value)

[<Fact>]
let ``a header, the content and a key bar divide the whole screen between them`` () =
    use window = laidOut (text [ "first" ])

    let header = FileViewer.header window
    let content = (FileViewer.content window :> Terminal.Gui.ViewBase.View)
    let keyBar = FileViewer.keyBar window

    Assert.Equal(1, header.Frame.Height)
    Assert.Equal(0, header.Frame.Y)
    Assert.Equal(1, content.Frame.Y)
    Assert.Equal(1, keyBar.Frame.Height)
    Assert.Equal(23, keyBar.Frame.Y)
    Assert.Equal(22, content.Frame.Height)

[<Fact>]
let ``the header names the file, the mode, the encoding and the size`` () =
    use window = laidOut (text [ "first"; "second" ])

    let shown = drawnText (FileViewer.header window)

    Assert.Contains("notes.txt", shown)
    Assert.Contains("Text", shown)
    Assert.Contains("UTF-8", shown)
    Assert.Contains("12 B", shown)

[<Fact>]
let ``the key bar says what the function keys do`` () =
    use window = laidOut (text [ "first" ])

    let labels =
        FunctionKeyBar.slots (FileViewer.keyBar window)
        |> List.map _.CommandName.Text

    Assert.Equal("Wrap", labels[1])
    Assert.Equal("Quit", labels[2])
    Assert.Equal("Hex", labels[3])

[<Fact>]
let ``the content shows the first lines of the file`` () =
    use window = laidOut (numbered 100)

    let rows = (FileViewer.content window).Rows()

    Assert.Equal("line 1", List.head rows)
    Assert.Equal(22, List.length rows)

[<Fact>]
let ``a file shorter than the screen fills what it can`` () =
    use window = laidOut (numbered 3)

    let rows = (FileViewer.content window).Rows()

    Assert.Equal<string list>([ "line 1"; "line 2"; "line 3" ], rows)

[<Fact>]
let ``the down arrow moves on by a line`` () =
    use window = laidOut (numbered 100)
    let content = FileViewer.content window

    press content Key.CursorDown

    Assert.Equal(1, content.Top)
    Assert.Equal("line 2", List.head (content.Rows()))

[<Fact>]
let ``the up arrow moves back by a line`` () =
    use window = laidOut (numbered 100)
    let content = FileViewer.content window
    press content Key.CursorDown
    press content Key.CursorDown

    press content Key.CursorUp

    Assert.Equal(1, content.Top)

[<Fact>]
let ``the arrows stop at the start of the file`` () =
    use window = laidOut (numbered 100)
    let content = FileViewer.content window

    press content Key.CursorUp

    Assert.Equal(0, content.Top)

[<Fact>]
let ``the arrows stop at the end of the file`` () =
    use window = laidOut (numbered 3)
    let content = FileViewer.content window

    for _ in 1..20 do
        press content Key.CursorDown

    Assert.Equal(2, content.Top)

[<Fact>]
let ``a page moves on by a screenful`` () =
    use window = laidOut (numbered 100)
    let content = FileViewer.content window

    press content Key.PageDown

    Assert.Equal(22, content.Top)

[<Fact>]
let ``a page back returns where it came from`` () =
    use window = laidOut (numbered 100)
    let content = FileViewer.content window
    press content Key.PageDown

    press content Key.PageUp

    Assert.Equal(0, content.Top)

[<Fact>]
let ``the end key shows the last line at the bottom`` () =
    use window = laidOut (numbered 100)
    let content = FileViewer.content window

    press content Key.End

    Assert.Equal(78, content.Top)
    Assert.Equal("line 100", List.last (content.Rows()))

[<Fact>]
let ``the home key returns to the first line`` () =
    use window = laidOut (numbered 100)
    let content = FileViewer.content window
    press content Key.End

    press content Key.Home

    Assert.Equal(0, content.Top)

[<Fact>]
let ``the side arrows move across a long line`` () =
    use window = laidOut (text [ String('x', 200) ])
    let content = FileViewer.content window

    press content Key.CursorRight
    press content Key.CursorRight

    Assert.Equal(2, content.Left)
    Assert.Equal(80, (List.head (content.Rows())).Length)

[<Fact>]
let ``moving across stops where the longest line ends`` () =
    use window = laidOut (text [ String('x', 100) ])
    let content = FileViewer.content window

    for _ in 1..60 do
        press content Key.CursorRight

    Assert.Equal(20, content.Left)

[<Fact>]
let ``there is nowhere to move across a line that fits`` () =
    use window = laidOut (text [ "short" ])
    let content = FileViewer.content window

    for _ in 1..10 do
        press content Key.CursorRight

    Assert.Equal(0, content.Left)

[<Fact>]
let ``the percentage grows as the file is read`` () =
    use window = laidOut (numbered 100)
    let content = FileViewer.content window
    let atStart = content.Percent

    press content Key.End

    Assert.True(atStart < 100, $"a hundred lines showed {atStart}%% at the top")
    Assert.Equal(100, content.Percent)

[<Fact>]
let ``the header counts lines from one`` () =
    use window = laidOut (numbered 100)
    let content = FileViewer.content window

    press content Key.CursorDown

    Assert.Contains("line 2", drawnText (FileViewer.header window))

[<Fact>]
let ``json in utf-16 is shown as the text it is`` () =
    let json = "{ \"name\": \"value\" }"
    let wide = Array.append (Encoding.Unicode.GetPreamble()) (Encoding.Unicode.GetBytes json)
    use window = laidOut (MockFileData wide)

    let content = FileViewer.content window

    Assert.Equal(TextMode, content.Mode)
    Assert.Equal(json, List.head (content.Rows()))
    Assert.Contains("UTF-16 LE", drawnText (FileViewer.header window))

[<Fact>]
let ``F4 turns the text into a hex dump and back`` () =
    use window = laidOut (text [ "first" ])
    let content = FileViewer.content window

    press content Key.F4

    Assert.Equal(HexMode, content.Mode)
    Assert.StartsWith("0000000000  66 69 72 73 74", List.head (content.Rows()))
    Assert.Contains("Hex", drawnText (FileViewer.header window))

[<Fact>]
let ``the hex dump keeps the place the text was at`` () =
    use window = laidOut (sixteenByteLines 100)
    let content = FileViewer.content window
    press content Key.PageDown

    press content Key.F4

    Assert.Equal(HexMode, content.Mode)
    Assert.Equal(ViewerHeader.AtOffset 352L, content.Place)
    Assert.StartsWith("0000000160  ", List.head (content.Rows()))

[<Fact>]
let ``leaving the hex dump lands on the line the offset was in`` () =
    use window = laidOut (sixteenByteLines 100)
    let content = FileViewer.content window
    press content Key.PageDown
    let line = content.Top

    press content Key.F4
    press content Key.F4

    Assert.Equal(TextMode, content.Mode)
    Assert.Equal(line, content.Top)

[<Fact>]
let ``F2 wraps a long line over several rows`` () =
    use window = laidOut (text [ String('x', 200); "after" ])
    let content = FileViewer.content window

    press content Key.F2

    Assert.Equal(Wrapped, content.Wrap)
    Assert.Equal(80, (List.head (content.Rows())).Length)
    Assert.Equal<string list>([ String('x', 80); String('x', 80); String('x', 40); "after" ], content.Rows())

[<Fact>]
let ``F2 again lays the line back out flat`` () =
    use window = laidOut (text [ String('x', 200); "after" ])
    let content = FileViewer.content window
    press content Key.F2

    press content Key.F2

    Assert.Equal(Unwrapped, content.Wrap)
    Assert.Equal<string list>([ String('x', 80); "after" ], content.Rows())

[<Fact>]
let ``a wrapped page moves on by the lines it showed`` () =
    use window = laidOut (text [ for _ in 1..40 -> String('y', 160) ])
    let content = FileViewer.content window
    press content Key.F2

    press content Key.PageDown

    Assert.Equal(11, content.Top)

[<Fact>]
let ``the key bar offers the way back out of hex and wrap`` () =
    use window = laidOut (text [ "first" ])
    let content = FileViewer.content window

    press content Key.F4
    press content Key.F2

    let labels =
        FunctionKeyBar.slots (FileViewer.keyBar window)
        |> List.map _.CommandName.Text

    Assert.Equal("Unwrap", labels[1])
    Assert.Equal("Text", labels[3])

[<Fact>]
let ``a control character in the text stands in for itself`` () =
    use window = laidOut (MockFileData [| 0x61uy; 0x01uy; 0x62uy |])
    let content = FileViewer.content window

    let rows = content.Rows()

    Assert.Equal("a.b", List.head rows)

[<Fact>]
let ``an empty file shows nothing and says so`` () =
    use window = laidOut (MockFileData "")
    let content = FileViewer.content window

    Assert.Empty(content.Rows())
    Assert.Contains("0 B", drawnText (FileViewer.header window))

[<Fact>]
let ``the viewer paints every mode it can be in`` () =
    use window = laidOut (text [ String('x', 200); "after"; "last" ])
    let content = FileViewer.content window

    for key in [ Key.F2; Key.F2; Key.F4; Key.F4 ] do
        content.Draw()
        press content key

    content.Draw()

    Assert.Equal(TextMode, content.Mode)

[<Fact>]
let ``F3 closes the viewer`` () =
    let closed = ResizeArray<Window>()
    use window = viewing closed.Add (Size(80, 24)) (text [ "first" ])

    press (FileViewer.content window) Key.F3

    Assert.Same(window, Assert.Single closed)

[<Fact>]
let ``Esc closes the viewer`` () =
    let closed = ResizeArray<Window>()
    use window = viewing closed.Add (Size(80, 24)) (text [ "first" ])

    press (FileViewer.content window) Key.Esc

    Assert.Same(window, Assert.Single closed)

[<Fact>]
let ``a key the viewer does not use leaves it open`` () =
    let closed = ResizeArray<Window>()
    use window = viewing closed.Add (Size(80, 24)) (text [ "first" ])

    press (FileViewer.content window) (Key 'x')

    Assert.Empty closed

[<Fact>]
let ``a file of a hundred thousand lines opens at its first line`` () =
    use window = laidOut (numbered 100000)

    let content = FileViewer.content window

    Assert.Equal("line 1", List.head (content.Rows()))
    Assert.True(content.Percent < 5, $"the top of the file showed {content.Percent}%%")

[<Fact>]
let ``F8 offers the code page in use and what the viewer settled on`` () =
    let offers = ResizeArray<CodePageOffer>()
    use window = viewingWith ignore (recordingInto offers None) (Size(80, 24)) (text [ "first" ])

    press (FileViewer.content window) Key.F8

    Assert.Equal(
        { Detected = { Charset = Utf8; Bom = WithoutBom }
          Current = Automatic },
        Assert.Single offers
    )

[<Fact>]
let ``F8 again offers the code page chosen the time before`` () =
    let offers = ResizeArray<CodePageOffer>()
    use window = viewingWith ignore (recordingInto offers (Some(Fixed Cp852))) (Size(80, 24)) latinBytes
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

    Assert.Equal<string list>(
        [ ".¥±¹"
          "����"
          "ꖀ릱"
          "肥놹"
          ".¥±¹"
          "€Ą±ą"
          "Çą▒╣"
          ".Ľąš"
          "€¥±¹"
          "ÇÑ▒╣" ],
        firstRows
    )

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
    use window = choosing (Fixed Utf16Le) (MockFileData(Encoding.Unicode.GetBytes "first\nsecond"))
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
        FunctionKeyBar.slots (FileViewer.keyBar window)
        |> List.map _.CommandName.Text

    Assert.Equal("Code", labels[7])

let private typed raw = EnteredText.tryCreate raw |> Option.get

let private wanted raw = FindText.tryCreate raw |> Option.get

let private answering raw =
    { quietPorts with
        AskSearch = fun _ -> Some raw }

let private searching ports data =
    viewingThrough ports (Size(80, 24)) data

let private searched ports data =
    let window = searching ports data
    press (FileViewer.content window) Key.F7
    window

let private stopping (work: WindowSearch.Control -> WindowSearch.Answer) =
    work
        { Reached = ignore
          IsCancelled = fun () -> true }

let private recordingOffers (offers: ResizeArray<SearchOffer>) answer offer =
    offers.Add offer
    answer

let private counting (runs: ResizeArray<unit>) (_: WindowSearch.Control -> WindowSearch.Answer) =
    runs.Add()
    WindowSearch.NotFound

[<Fact>]
let ``F7 offers the texts searched before and nothing typed yet`` () =
    let offers = ResizeArray<SearchOffer>()

    let ports =
        { quietPorts with
            AskSearch = recordingOffers offers None
            Searches.Texts = fun () -> [ typed "older" ] }

    use window = searched ports (numbered 10)

    Assert.Equal({ Remembered = [ typed "older" ]; Last = None }, Assert.Single offers)

[<Fact>]
let ``F7 pressed again offers the last text searched for`` () =
    let offers = ResizeArray<SearchOffer>()

    let ports =
        { quietPorts with
            AskSearch = recordingOffers offers (Some "line 5") }

    use window = searched ports (numbered 10)

    press (FileViewer.content window) Key.F7

    Assert.Equal(Some(wanted "line 5"), offers[1].Last)

[<Fact>]
let ``a text searched for is remembered as it was typed`` () =
    let remembered = ResizeArray<EnteredText>()

    let ports =
        { answering "line 5 " with
            Searches.Remember = remembered.Add }

    use window = searched ports (numbered 10)

    Assert.Equal(typed "line 5 ", Assert.Single remembered)

[<Fact>]
let ``an empty field searches for nothing`` () =
    let runs = ResizeArray<unit>()

    let ports =
        { answering "" with
            RunSearch = counting runs }

    use window = searched ports (numbered 10)

    Assert.Empty runs

[<Fact>]
let ``a popup closed without a text searches for nothing`` () =
    let runs = ResizeArray<unit>()

    let ports =
        { quietPorts with
            RunSearch = counting runs }

    use window = searched ports (numbered 10)

    Assert.Empty runs

[<Fact>]
let ``a match on the screen leaves the screen where it stands`` () =
    use window = searched (answering "line 5") (numbered 100)

    let content = FileViewer.content window

    Assert.Equal(0, content.Top)
    Assert.Equal<(int * int * int) list>([ 4, 0, 6 ], content.FoundOnScreen())

[<Fact>]
let ``a match further down a file larger than one window of bytes comes to the top`` () =
    use window = searched (answering "line 9000") (numbered 10000)

    let content = FileViewer.content window

    Assert.Equal(8999, content.Top)
    Assert.Equal("line 9000", List.head (content.Rows()))
    Assert.Equal<(int * int * int) list>([ 0, 0, 9 ], content.FoundOnScreen())

[<Fact>]
let ``a search starts from the top of the screen`` () =
    use window = searching (answering "needle") (text [ "needle"; "hay"; "hay"; "needle" ])
    let content = FileViewer.content window
    press content Key.CursorDown

    press content Key.F7

    Assert.Equal<(int * int * int) list>([ 2, 0, 6 ], content.FoundOnScreen())

[<Fact>]
let ``text that is not in the file is reported and the screen stays`` () =
    let reports = ResizeArray<SearchReport>()

    let ports =
        { answering "absent" with
            Report = reports.Add }

    use window = searching ports (numbered 100)
    let content = FileViewer.content window
    press content Key.CursorDown

    press content Key.F7

    Assert.Equal(TextNotFound(wanted "absent"), Assert.Single reports)
    Assert.Equal(1, content.Top)
    Assert.Empty(content.FoundOnScreen())

[<Fact>]
let ``a search stopped with Esc leaves the screen and says nothing`` () =
    let reports = ResizeArray<SearchReport>()

    let ports =
        { answering "line 90" with
            RunSearch = stopping
            Report = reports.Add }

    use window = searched ports (numbered 100)

    let content = FileViewer.content window
    Assert.Empty reports
    Assert.Equal(0, content.Top)
    Assert.Empty(content.FoundOnScreen())

[<Fact>]
let ``a file that fails while it is searched is reported`` () =
    let reports = ResizeArray<SearchReport>()
    let failure = ReadFailed(fp notes, "the disk went away")

    let ports =
        { answering "line 90" with
            RunSearch = fun _ -> WindowSearch.Unreadable failure
            Report = reports.Add }

    use window = searched ports (numbered 100)

    Assert.Equal(SearchFailed failure, Assert.Single reports)

[<Fact>]
let ``a match past the right edge moves the screen across to it`` () =
    use window = searched (answering "needle") (text [ String('a', 100) + "needle" ])

    let content = FileViewer.content window

    Assert.Equal(26, content.Left)
    Assert.Equal<(int * int * int) list>([ 0, 74, 6 ], content.FoundOnScreen())

[<Fact>]
let ``a match in a wrapped line is marked on the row it falls on`` () =
    use window = searching (answering "needle") (text [ String('a', 100) + "needle" ])
    let content = FileViewer.content window
    press content Key.F2

    press content Key.F7

    Assert.Equal<(int * int * int) list>([ 1, 20, 6 ], content.FoundOnScreen())

[<Fact>]
let ``a match the wrap splits is marked on both rows`` () =
    use window = searching (answering "needle") (text [ String('a', 77) + "needle" ])
    let content = FileViewer.content window
    press content Key.F2

    press content Key.F7

    Assert.Equal<(int * int * int) list>([ 0, 77, 3; 1, 0, 3 ], content.FoundOnScreen())

[<Fact>]
let ``a match after a tab is marked where it is shown`` () =
    use window = searched (answering "needle") (text [ "a\tneedle" ])

    let content = FileViewer.content window

    Assert.Equal<(int * int * int) list>([ 0, 8, 6 ], content.FoundOnScreen())

[<Fact>]
let ``the search reads the file in the code page chosen`` () =
    let ports =
        { answering "Ą" with
            ChooseCodePage = fun _ -> Some(Fixed Windows1250) }

    use window = searching ports latinBytes
    let content = FileViewer.content window
    press content Key.F8

    press content Key.F7

    Assert.Equal<(int * int * int) list>([ 0, 1, 1 ], content.FoundOnScreen())

[<Fact>]
let ``choosing another code page drops the mark`` () =
    let ports =
        { answering "line 5" with
            ChooseCodePage = fun _ -> Some(Fixed Windows1250) }

    use window = searched ports (numbered 100)
    let content = FileViewer.content window

    press content Key.F8

    Assert.Empty(content.FoundOnScreen())

[<Fact>]
let ``F7 in hex asks for nothing`` () =
    let offers = ResizeArray<SearchOffer>()

    let ports =
        { quietPorts with
            AskSearch = recordingOffers offers None }

    use window = searching ports (numbered 10)
    let content = FileViewer.content window
    press content Key.F4

    press content Key.F7

    Assert.Empty offers

[<Fact>]
let ``the key bar offers the search`` () =
    use window = laidOut (text [ "first" ])

    let labels =
        FunctionKeyBar.slots (FileViewer.keyBar window)
        |> List.map _.CommandName.Text

    Assert.Equal("Search", labels[6])
