module Marbas.Tests.Integration.FileViewerTests

open System
open System.Drawing
open System.Text
open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Drawing
open Terminal.Gui.Input
open Terminal.Gui.Views
open Marbas.FileSystem
open Marbas.Ui
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private fp = FileSystemPath.create

let private notes = at "start/notes.txt"

let private sourceOf (data: MockFileData) =
    let fs = MockFileSystem()
    fs.AddFile(notes, data)
    FileWindow.openFile fs (fp notes) |> Expect.ok

let private viewing close size (data: MockFileData) =
    let window = FileViewer.create close (sourceOf data)
    window.BeginInit()
    window.EndInit()
    window.Layout(size) |> ignore
    window

let private laidOut data = viewing ignore (Size(80, 24)) data

let private text (lines: string seq) = MockFileData(String.concat "\n" lines)

let private numbered count =
    text [ for line in 1..count -> $"line {line}" ]

let private sixteenByteLines count =
    text [ for line in 1..count -> line.ToString "000000000000000" ]

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
    Assert.StartsWith("00000000  66 69 72 73 74", List.head (content.Rows()))
    Assert.Contains("Hex", drawnText (FileViewer.header window))

[<Fact>]
let ``the hex dump keeps the place the text was at`` () =
    use window = laidOut (sixteenByteLines 100)
    let content = FileViewer.content window
    press content Key.PageDown

    press content Key.F4

    Assert.Equal(HexMode, content.Mode)
    Assert.Equal(ViewerHeader.AtOffset 352L, content.Place)
    Assert.StartsWith("00000160", List.head (content.Rows()))

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
