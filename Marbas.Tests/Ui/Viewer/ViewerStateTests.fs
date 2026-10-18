module Marbas.Tests.Ui.Viewer.ViewerStateTests

open System
open System.Drawing
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Viewing
open Marbas.Ui.Viewer
open Marbas.Ui.Viewer.ViewerKeys
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private notes = at "start/notes.txt"

let private screen = Size(80, 22)

let private opened (data: MockFileData) =
    let fs = MockFileSystem()
    fs.AddFile(notes, data)
    FileWindow.openFile fs (fp notes) |> Expect.ok |> ViewerState.start

let private text (lines: string seq) = MockFileData(String.concat "\n" lines)

let private numbered count =
    text [ for line in 1..count -> $"line {line}" ]

let private sixteenByteLines count =
    text [ for line in 1..count -> line.ToString "000000000000000" ]

let private latinBytes = MockFileData [| 0x41uy; 0xA5uy |]

let private after commands state =
    commands
    |> List.fold (fun reached command -> ViewerState.run screen command reached) state

let private lineFive: WindowSearch.Match =
    { LineStart = 28L
      Column = 0
      Length = 6 }

let private lineFifty: WindowSearch.Match =
    { LineStart = 383L
      Column = 0
      Length = 7 }

let private needleAfter column : WindowSearch.Match =
    { LineStart = 0L
      Column = column
      Length = 6 }

let private placed lineStart column : WindowSearch.Place =
    { LineStart = lineStart
      Column = column }

let private needle = [| 0xFCuy; 0xFDuy; 0xFEuy; 0xFFuy |]

let private binaryWithNeedleAt (offset: int) length =
    let bytes = Array.init length (fun index -> byte (index % 251))
    Array.blit needle 0 bytes offset needle.Length
    MockFileData bytes

let private runAt (offset: int64) : ByteSearch.Match = { Offset = offset; Length = 4 }

let private marksOf row first last =
    [ row, HexDump.hexColumn first, HexDump.hexColumn last + 2 - HexDump.hexColumn first
      row, HexDump.textColumn first, last - first + 1 ]

let private textQuery raw : WindowSearch.Query =
    { Wanted = WindowSearch.Literal(Expect.some (FindText.tryCreate raw), Anywhere)
      Case = IgnoringCase
      Direction = Forwards }

let private bytesQuery raw : ByteSearch.Query =
    { Bytes = WantedBytes.tryParse raw |> Expect.ok
      Direction = Forwards }

[<Fact>]
let ``a file opens at its first line and its first column`` () =
    let state = opened (numbered 100)

    Assert.Equal(0, state.Top)
    Assert.Equal(0, state.Left)

[<Fact>]
let ``a file opens as text, unwrapped, in the encoding it was detected in`` () =
    let state = opened (numbered 100)

    Assert.Equal(TextMode, state.Mode)
    Assert.Equal(Unwrapped, state.Wrap)
    Assert.Equal(Automatic, state.CodePage)
    Assert.Equal({ Charset = Utf8; Bom = WithoutBom }, state.Encoding)

[<Fact>]
let ``the rows are a screenful from the top`` () =
    let state = opened (numbered 100)

    let rows = ViewerState.rows screen state

    Assert.Equal("line 1", List.head rows)
    Assert.Equal(22, List.length rows)

[<Fact>]
let ``a file shorter than the screen fills what it can`` () =
    let state = opened (numbered 3)

    let rows = ViewerState.rows screen state

    Assert.Equal<string list>([ "line 1"; "line 2"; "line 3" ], rows)

[<Fact>]
let ``an empty file has no rows and is read to its end`` () =
    let state = opened (MockFileData "")

    let rows = ViewerState.rows screen state

    Assert.Empty rows
    Assert.Equal(100, ViewerState.percent screen state)

[<Fact>]
let ``a line down moves on by one`` () =
    let state = opened (numbered 100)

    let moved = ViewerState.run screen LineDown state

    Assert.Equal(1, moved.Top)

[<Fact>]
let ``a line up moves back by one`` () =
    let state = opened (numbered 100) |> after [ LineDown; LineDown ]

    let moved = ViewerState.run screen LineUp state

    Assert.Equal(1, moved.Top)

[<Fact>]
let ``a line up stops at the start of the file`` () =
    let state = opened (numbered 100)

    let moved = ViewerState.run screen LineUp state

    Assert.Equal(0, moved.Top)

[<Fact>]
let ``a line down stops on the last line of the file`` () =
    let state = opened (numbered 3)

    let moved = after (List.replicate 20 LineDown) state

    Assert.Equal(2, moved.Top)

[<Fact>]
let ``a page down moves on by a screenful`` () =
    let state = opened (numbered 100)

    let moved = ViewerState.run screen PageDown state

    Assert.Equal(22, moved.Top)

[<Fact>]
let ``a page up returns where the page down came from`` () =
    let state = opened (numbered 100) |> after [ PageDown ]

    let moved = ViewerState.run screen PageUp state

    Assert.Equal(0, moved.Top)

[<Fact>]
let ``a page on a smaller screen is a smaller step`` () =
    let state = opened (numbered 100)

    let moved = ViewerState.run (Size(80, 10)) PageDown state

    Assert.Equal(10, moved.Top)

[<Fact>]
let ``the end shows the last line at the bottom of the screen`` () =
    let state = opened (numbered 100)

    let moved = ViewerState.run screen ToEnd state

    Assert.Equal(78, moved.Top)
    Assert.Equal("line 100", List.last (ViewerState.rows screen moved))

[<Fact>]
let ``the start returns to the first line and the first column`` () =
    let state =
        opened (text [ String('x', 200); String('x', 200); "last" ])
        |> after [ ColumnRight; ColumnRight; LineDown ]

    let moved = ViewerState.run screen ToStart state

    Assert.Equal(0, moved.Top)
    Assert.Equal(0, moved.Left)

[<Fact>]
let ``a column right moves across a long line`` () =
    let state = opened (text [ String('x', 200) ])

    let moved = after [ ColumnRight; ColumnRight ] state

    Assert.Equal(2, moved.Left)
    Assert.Equal(80, (List.head (ViewerState.rows screen moved)).Length)

[<Fact>]
let ``a column left moves back across`` () =
    let state = opened (text [ String('x', 200) ]) |> after [ ColumnRight; ColumnRight ]

    let moved = ViewerState.run screen ColumnLeft state

    Assert.Equal(1, moved.Left)

[<Fact>]
let ``moving across stops where the longest line on the screen ends`` () =
    let state = opened (text [ String('x', 100) ])

    let moved = after (List.replicate 60 ColumnRight) state

    Assert.Equal(20, moved.Left)

[<Fact>]
let ``there is nowhere to move across a line that fits`` () =
    let state = opened (text [ "short" ])

    let moved = after (List.replicate 10 ColumnRight) state

    Assert.Equal(0, moved.Left)

[<Fact>]
let ``a page across moves by the width of the screen`` () =
    let state = opened (text [ String('x', 200) ])

    let moved = ViewerState.run screen PageRight state

    Assert.Equal(80, moved.Left)

[<Fact>]
let ``a page back across returns`` () =
    let state = opened (text [ String('x', 200) ]) |> after [ PageRight ]

    let moved = ViewerState.run screen PageLeft state

    Assert.Equal(0, moved.Left)

[<Fact>]
let ``wrapping lays a long line over several rows`` () =
    let state = opened (text [ String('x', 200); "after" ])

    let wrapped = ViewerState.run screen WrapToggled state

    Assert.Equal(Wrapped, wrapped.Wrap)

    Assert.Equal<string list>(
        [ String('x', 80); String('x', 80); String('x', 40); "after" ],
        ViewerState.rows screen wrapped
    )

[<Fact>]
let ``wrapping returns to the first column`` () =
    let state = opened (text [ String('x', 200) ]) |> after [ ColumnRight ]

    let wrapped = ViewerState.run screen WrapToggled state

    Assert.Equal(0, wrapped.Left)

[<Fact>]
let ``wrapping again lays the line back out flat`` () =
    let state = opened (text [ String('x', 200); "after" ]) |> after [ WrapToggled ]

    let flat = ViewerState.run screen WrapToggled state

    Assert.Equal(Unwrapped, flat.Wrap)
    Assert.Equal<string list>([ String('x', 80); "after" ], ViewerState.rows screen flat)

[<Fact>]
let ``a wrapped screen does not move across`` () =
    let state = opened (text [ String('x', 200) ]) |> after [ WrapToggled ]

    let moved = after [ ColumnRight; PageRight ] state

    Assert.Equal(0, moved.Left)

[<Fact>]
let ``a wrapped page moves on by the lines it showed`` () =
    let state =
        opened (text [ for _ in 1..40 -> String('y', 160) ]) |> after [ WrapToggled ]

    let moved = ViewerState.run screen PageDown state

    Assert.Equal(11, moved.Top)

[<Fact>]
let ``the mode switches the text into a hex dump`` () =
    let state = opened (text [ "first" ])

    let dumped = ViewerState.run screen ModeToggled state

    Assert.Equal(HexMode, dumped.Mode)
    Assert.StartsWith("0000000000  66 69 72 73 74", List.head (ViewerState.rows screen dumped))

[<Fact>]
let ``the hex dump keeps the place the text was at`` () =
    let state = opened (sixteenByteLines 100) |> after [ PageDown ]

    let dumped = ViewerState.run screen ModeToggled state

    Assert.Equal(22, dumped.Top)
    Assert.Equal(ViewerHeader.AtOffset 352L, ViewerState.place dumped)

[<Fact>]
let ``leaving the hex dump lands on the line the offset was in`` () =
    let state =
        opened (sixteenByteLines 100) |> after [ PageDown; ModeToggled; LineDown ]

    let read = ViewerState.run screen ModeToggled state

    Assert.Equal(TextMode, read.Mode)
    Assert.Equal(23, read.Top)

[<Fact>]
let ``switching the mode returns to the first column`` () =
    let state = opened (text [ String('x', 200) ]) |> after [ ColumnRight ]

    let dumped = ViewerState.run screen ModeToggled state

    Assert.Equal(0, dumped.Left)

[<Fact>]
let ``the keys that ask for something leave the viewing where it is`` () =
    let state = opened (numbered 100) |> after [ PageDown; ColumnRight ]

    let reached =
        [ SearchAsked; SearchRepeated; CodePageAsked; Closed ]
        |> List.map (fun command -> ViewerState.run screen command state)

    Assert.Equal<ViewerState list>(List.replicate 4 state, reached)

[<Fact>]
let ``in text the place is the line at the top`` () =
    let state = opened (numbered 100) |> after [ LineDown ]

    let place = ViewerState.place state

    Assert.Equal(ViewerHeader.AtLine 1, place)

[<Fact>]
let ``the percentage counts what the screen has reached`` () =
    let state = opened (numbered 100)

    let atStart = ViewerState.percent screen state

    Assert.InRange(atStart, 1, 99)

[<Fact>]
let ``at the end the whole file is read`` () =
    let state = opened (numbered 100) |> after [ ToEnd ]

    let atEnd = ViewerState.percent screen state

    Assert.Equal(100, atEnd)

[<Fact>]
let ``a code page chosen is the one the bytes are read in`` () =
    let state = opened latinBytes

    let read = ViewerState.readAs (Fixed Cp852) state

    Assert.Equal(Fixed Cp852, read.CodePage)
    Assert.Equal({ Charset = Cp852; Bom = WithoutBom }, read.Encoding)
    Assert.Equal<string list>([ "Aą" ], ViewerState.rows screen read)

[<Fact>]
let ``the automatic code page reads as the file was detected`` () =
    let state = opened latinBytes |> ViewerState.readAs (Fixed Cp852)

    let read = ViewerState.readAs Automatic state

    Assert.Equal(Automatic, read.CodePage)
    Assert.Equal<string list>([ "A¥" ], ViewerState.rows screen read)

[<Fact>]
let ``a new code page keeps the place in the text`` () =
    let state = opened (numbered 100) |> after [ PageDown ]

    let read = ViewerState.readAs (Fixed Windows1250) state

    Assert.Equal(22, read.Top)
    Assert.Equal("line 23", List.head (ViewerState.rows screen read))

[<Fact>]
let ``a new code page returns the text to the first column`` () =
    let state = opened (text [ String('x', 200) ]) |> after [ ColumnRight ]

    let read = ViewerState.readAs (Fixed Windows1250) state

    Assert.Equal(0, read.Left)

[<Fact>]
let ``a code page with a wider line feed finds the lines it holds`` () =
    let state = opened (MockFileData(Text.Encoding.Unicode.GetBytes "first\nsecond"))

    let read = ViewerState.readAs (Fixed Utf16Le) state

    Assert.Equal<string list>([ "first"; "second" ], ViewerState.rows screen read)

[<Fact>]
let ``in hex a new code page keeps the row`` () =
    let state = opened (sixteenByteLines 100) |> after [ ModeToggled; PageDown ]

    let read = ViewerState.readAs (Fixed Cp852) state

    Assert.Equal(HexMode, read.Mode)
    Assert.Equal(22, read.Top)

[<Fact>]
let ``in hex the character column follows the code page`` () =
    let state = opened latinBytes |> after [ ModeToggled ]

    let read = ViewerState.readAs (Fixed Cp852) state

    Assert.EndsWith("Aą", List.head (ViewerState.rows screen read))

[<Fact>]
let ``a match on the screen is marked and leaves the screen where it stands`` () =
    let state = opened (numbered 100)

    let shown = ViewerState.show screen lineFive state

    Assert.Equal(0, shown.Top)
    Assert.Equal<(int * int * int) list>([ 4, 0, 6 ], ViewerState.foundOnScreen screen shown)

[<Fact>]
let ``a match further down comes to the top of the screen`` () =
    let state = opened (numbered 100)

    let shown = ViewerState.show screen lineFifty state

    Assert.Equal(49, shown.Top)
    Assert.Equal<(int * int * int) list>([ 0, 0, 7 ], ViewerState.foundOnScreen screen shown)

[<Fact>]
let ``a match past the right edge moves the screen across to it`` () =
    let state = opened (text [ String('a', 100) + "needle" ])

    let shown = ViewerState.show screen (needleAfter 100) state

    Assert.Equal(26, shown.Left)
    Assert.Equal<(int * int * int) list>([ 0, 74, 6 ], ViewerState.foundOnScreen screen shown)

[<Fact>]
let ``a match in a wrapped line is marked on the row it falls on`` () =
    let state = opened (text [ String('a', 100) + "needle" ]) |> after [ WrapToggled ]

    let shown = ViewerState.show screen (needleAfter 100) state

    Assert.Equal(0, shown.Left)
    Assert.Equal<(int * int * int) list>([ 1, 20, 6 ], ViewerState.foundOnScreen screen shown)

[<Fact>]
let ``a match the wrap splits is marked on both rows`` () =
    let state = opened (text [ String('a', 77) + "needle" ]) |> after [ WrapToggled ]

    let shown = ViewerState.show screen (needleAfter 77) state

    Assert.Equal<(int * int * int) list>([ 0, 77, 3; 1, 0, 3 ], ViewerState.foundOnScreen screen shown)

[<Fact>]
let ``a match scrolled off the screen is marked no more`` () =
    let state = opened (numbered 100) |> ViewerState.show screen lineFive

    let moved = ViewerState.run screen PageDown state

    Assert.Empty(ViewerState.foundOnScreen screen moved)

[<Fact>]
let ``a match marked in text is not marked in hex`` () =
    let state = opened (numbered 100) |> ViewerState.show screen lineFive

    let dumped = ViewerState.run screen ModeToggled state

    Assert.Empty(ViewerState.foundOnScreen screen dumped)

[<Fact>]
let ``a new code page forgets the match that was marked`` () =
    let state = opened (numbered 100) |> ViewerState.show screen lineFive

    let read = ViewerState.readAs (Fixed Windows1250) state

    Assert.Empty(ViewerState.foundOnScreen screen read)
    Assert.Equal(ViewerState.screenPlace screen Forwards read, ViewerState.placeAfterMatch screen Forwards read)

[<Fact>]
let ``a search forwards starts from the line at the top`` () =
    let state = opened (text [ "needle"; "hay"; "hay"; "needle" ]) |> after [ LineDown ]

    let place = ViewerState.screenPlace screen Forwards state

    Assert.Equal(placed 7L 0, place)

[<Fact>]
let ``a search backwards starts from the end of the line at the bottom`` () =
    let state = opened (numbered 100)

    let place = ViewerState.screenPlace screen Backwards state

    Assert.Equal(placed 159L 7, place)

[<Fact>]
let ``with nothing found yet a search again starts from the screen`` () =
    let state = opened (numbered 100) |> after [ LineDown ]

    let place = ViewerState.placeAfterMatch screen Forwards state

    Assert.Equal(ViewerState.screenPlace screen Forwards state, place)

[<Fact>]
let ``a search again forwards goes on from the end of the match`` () =
    let state = opened (numbered 100) |> ViewerState.show screen lineFive

    let place = ViewerState.placeAfterMatch screen Forwards state

    Assert.Equal(placed 28L 6, place)

[<Fact>]
let ``a search again backwards goes on from the start of the match`` () =
    let state = opened (numbered 100) |> ViewerState.show screen lineFive

    let place = ViewerState.placeAfterMatch screen Backwards state

    Assert.Equal(placed 28L 0, place)

[<Fact>]
let ``a run of bytes on the screen is marked in both columns`` () =
    let state = opened (binaryWithNeedleAt 40 1000) |> after [ ModeToggled ]

    let shown = ViewerState.showBytesMatch screen (runAt 40L) state

    Assert.Equal(0, shown.Top)
    Assert.Equal<(int * int * int) list>(marksOf 2 8 11, ViewerState.foundOnScreen screen shown)

[<Fact>]
let ``a run of bytes further down comes to the top of the screen`` () =
    let state = opened (binaryWithNeedleAt 800 1000) |> after [ ModeToggled ]

    let shown = ViewerState.showBytesMatch screen (runAt 800L) state

    Assert.Equal(50, shown.Top)
    Assert.Equal<(int * int * int) list>(marksOf 0 0 3, ViewerState.foundOnScreen screen shown)

[<Fact>]
let ``a run split between two rows is marked on both`` () =
    let state = opened (binaryWithNeedleAt 46 1000) |> after [ ModeToggled ]

    let shown = ViewerState.showBytesMatch screen (runAt 46L) state

    Assert.Equal<(int * int * int) list>(marksOf 2 14 15 @ marksOf 3 0 1, ViewerState.foundOnScreen screen shown)

[<Fact>]
let ``a run marked in hex is not marked in text`` () =
    let state =
        opened (binaryWithNeedleAt 40 1000)
        |> after [ ModeToggled ]
        |> ViewerState.showBytesMatch screen (runAt 40L)

    let read = ViewerState.run screen ModeToggled state

    Assert.Empty(ViewerState.foundOnScreen screen read)

[<Fact>]
let ``a new code page keeps the run marked in hex`` () =
    let state =
        opened (binaryWithNeedleAt 40 1000)
        |> after [ ModeToggled ]
        |> ViewerState.showBytesMatch screen (runAt 40L)

    let read = ViewerState.readAs (Fixed Windows1250) state

    Assert.Equal<(int * int * int) list>(marksOf 2 8 11, ViewerState.foundOnScreen screen read)

[<Fact>]
let ``bytes searched forwards start from the row at the top`` () =
    let state = opened (binaryWithNeedleAt 40 1000) |> after [ ModeToggled; LineDown ]

    let from = ViewerState.bytesScreenPlace screen Forwards state

    Assert.Equal(16L, from)

[<Fact>]
let ``bytes searched backwards start from under the row at the bottom`` () =
    let state = opened (binaryWithNeedleAt 40 1000) |> after [ ModeToggled ]

    let from = ViewerState.bytesScreenPlace screen Backwards state

    Assert.Equal(352L, from)

[<Fact>]
let ``bytes searched backwards on the last screen start from the end of the file`` () =
    let state = opened (binaryWithNeedleAt 40 100) |> after [ ModeToggled ]

    let from = ViewerState.bytesScreenPlace screen Backwards state

    Assert.Equal(100L, from)

[<Fact>]
let ``with no run found yet bytes searched again start from the screen`` () =
    let state = opened (binaryWithNeedleAt 40 1000) |> after [ ModeToggled; LineDown ]

    let from = ViewerState.placeAfterBytesMatch screen Forwards state

    Assert.Equal(16L, from)

[<Fact>]
let ``bytes searched again forwards go on from the end of the run`` () =
    let state =
        opened (binaryWithNeedleAt 40 1000)
        |> after [ ModeToggled ]
        |> ViewerState.showBytesMatch screen (runAt 40L)

    let from = ViewerState.placeAfterBytesMatch screen Forwards state

    Assert.Equal(44L, from)

[<Fact>]
let ``bytes searched again backwards go on from the start of the run`` () =
    let state =
        opened (binaryWithNeedleAt 40 1000)
        |> after [ ModeToggled ]
        |> ViewerState.showBytesMatch screen (runAt 40L)

    let from = ViewerState.placeAfterBytesMatch screen Backwards state

    Assert.Equal(40L, from)

[<Fact>]
let ``before any search there is nothing searched for to offer again`` () =
    let state = opened (numbered 10)

    Assert.Equal(None, state.LastTextQuery)
    Assert.Equal(None, state.LastBytesQuery)

[<Fact>]
let ``a text searched for is the one a search again repeats`` () =
    let state = opened (numbered 10)

    let searched = ViewerState.searchedFor (textQuery "line 5") state

    Assert.Equal(Some(textQuery "line 5"), searched.LastTextQuery)

[<Fact>]
let ``a text searched for after another takes its place`` () =
    let state = opened (numbered 10) |> ViewerState.searchedFor (textQuery "line 5")

    let searched = ViewerState.searchedFor (textQuery "line 7") state

    Assert.Equal(Some(textQuery "line 7"), searched.LastTextQuery)

[<Fact>]
let ``bytes searched for are kept beside the text and not in its place`` () =
    let state = opened (numbered 10) |> ViewerState.searchedFor (textQuery "line 5")

    let searched = ViewerState.searchedForBytes (bytesQuery "FC FD") state

    Assert.Equal(Some(bytesQuery "FC FD"), searched.LastBytesQuery)
    Assert.Equal(Some(textQuery "line 5"), searched.LastTextQuery)
