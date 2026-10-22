module Marbas.Tests.Integration.Viewer.FileViewerFixture

open System.Drawing
open System.IO.Abstractions.TestingHelpers
open Terminal.Gui.Input
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Viewing
open Marbas.Ui.Viewer
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private fp = FileSystemPath.create

let notes = at "start/notes.txt"

let private sourceOf (data: MockFileData) =
    let fs = MockFileSystem()
    fs.AddFile(notes, data)
    FileWindow.openFile fs (fp notes) |> Expect.ok

let quietPorts: FileViewer.Ports =
    { Close = ignore
      ChooseCodePage = fun _ -> None
      AskSearch = fun _ -> None
      Searches =
        { Texts = fun () -> []
          Remember = ignore }
      ByteSearches =
        { Texts = fun () -> []
          Remember = ignore }
      RunSearch =
        fun work ->
            work
                { Reached = ignore
                  IsCancelled = fun () -> false }
      RunByteSearch =
        fun work ->
            work
                { Reached = ignore
                  IsCancelled = fun () -> false }
      Report = ignore
      ShowHelp = ignore }

let viewingThrough ports size (data: MockFileData) =
    let window = FileViewer.create ports (sourceOf data)
    window.BeginInit()
    window.EndInit()
    window.Layout(size) |> ignore
    window

let viewingWith close choose size data =
    viewingThrough
        { quietPorts with
            Close = close
            ChooseCodePage = choose }
        size
        data

let viewing close size data =
    viewingThrough { quietPorts with Close = close } size data

let choosing page data =
    viewingWith ignore (fun _ -> Some page) (Size(80, 24)) data

let laidOut data = viewing ignore (Size(80, 24)) data

let text (lines: string seq) = MockFileData(String.concat "\n" lines)

let numbered count =
    text [ for line in 1..count -> $"line {line}" ]

let sixteenByteLines count =
    text [ for line in 1..count -> line.ToString "000000000000000" ]

let latinBytes = MockFileData [| 0x41uy; 0xA5uy |]

let typed raw =
    EnteredText.tryCreate raw |> Expect.some

let wanted raw = FindText.tryCreate raw |> Expect.some

let plainAnswer raw : SearchAnswer =
    { Typed = raw
      Case = IgnoringCase
      Words = Anywhere
      TypedAs = AsText
      Direction = Forwards }

let literally raw =
    WindowSearch.Literal(wanted raw, Anywhere)

let plainQuery raw : WindowSearch.Query =
    { Wanted = literally raw
      Case = IgnoringCase
      Direction = Forwards }

let answeringWith answer =
    { quietPorts with
        AskSearch = fun _ -> Some answer }

let answering raw = answeringWith (plainAnswer raw)

let searching ports data =
    viewingThrough ports (Size(80, 24)) data

let searched ports data =
    let window = searching ports data
    press (FileViewer.content window) Key.F7
    window

let textOffered (offer: SearchOffer) =
    match offer.Last with
    | LastText last -> last
    | LastBytes _ -> None

let recordingOffers (offers: ResizeArray<SearchOffer>) answer offer =
    offers.Add offer
    answer

let counting (runs: ResizeArray<unit>) (_: WindowSearch.Control -> WindowSearch.Answer<WindowSearch.Match>) =
    runs.Add()
    WindowSearch.NotFound

let again (content: FileViewer.ContentView) = press content Key.F7.WithShift
