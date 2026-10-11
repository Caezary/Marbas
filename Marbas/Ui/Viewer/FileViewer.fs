module Marbas.Ui.Viewer.FileViewer

open System.Drawing
open Terminal.Gui.Drawing
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui
open Marbas.Ui.Theme
open Marbas.Ui.Format
open Marbas.Ui.Keys

[<Literal>]
let ContentId = "viewerContent"

[<Literal>]
let HeaderId = "viewerHeader"

[<Literal>]
let KeyBarId = "viewerKeyBar"

type Ports =
    { Close: Window -> unit
      ChooseCodePage: CodePageOffer -> CodePage option
      AskSearch: SearchOffer -> SearchAnswer option
      Searches: Marbas.State.Ports.SearchHistory
      ByteSearches: Marbas.State.Ports.SearchHistory
      RunSearch:
          (WindowSearch.Control -> WindowSearch.Answer<WindowSearch.Match>) -> WindowSearch.Answer<WindowSearch.Match>
      RunByteSearch:
          (WindowSearch.Control -> WindowSearch.Answer<ByteSearch.Match>) -> WindowSearch.Answer<ByteSearch.Match>
      Report: SearchReport -> unit }

type ContentView(source: FileWindow.Source) as this =
    inherit View()

    let mutable state = ViewerState.start source

    do
        this.Id <- ContentId
        this.CanFocus <- true

    let screen () =
        Size(max 1 this.Viewport.Width, max 1 this.Viewport.Height)

    member _.State = state

    member _.Screen = screen ()

    member _.Change(change: ViewerState -> ViewerState) = state <- change state

    member _.CodePage = state.CodePage

    member _.Encoding = state.Encoding

    member _.Mode = state.Mode

    member _.Wrap = state.Wrap

    member _.Top = state.Top

    member _.Left = state.Left

    member _.Rows() = ViewerState.rows (screen ()) state

    member _.FoundOnScreen() =
        ViewerState.foundOnScreen (screen ()) state

    member _.Place = ViewerState.place state

    member _.Percent = ViewerState.percent (screen ()) state

    override this.OnDrawingContent(_: DrawContext) =
        let room = screen ()
        let screenful = ViewerState.shown room state
        let rows = screenful |> List.map _.Text
        let normal = this.GetCurrentAttribute()

        for row in 0 .. room.Height - 1 do
            let text = rows |> List.tryItem row |> Option.defaultValue ""
            this.Move(0, row) |> ignore
            this.AddStr(text.PadRight room.Width)

        this.SetAttribute ClassicScheme.foundText |> ignore

        for row, start, length in ViewerState.foundOn screenful state do
            this.Move(start, row) |> ignore
            this.AddStr(rows[row].Substring(start, length))

        this.SetAttribute normal |> ignore
        true

let private searching (ports: Ports) (source: FileWindow.Source) (content: ContentView) query place =
    let encoding = content.Encoding

    match ports.RunSearch(fun control -> WindowSearch.run control source encoding query place) with
    | WindowSearch.Found hit -> content.Change(ViewerState.show content.Screen hit)
    | WindowSearch.NotFound -> ports.Report(TextNotFound query.Wanted)
    | WindowSearch.Stopped -> ()
    | WindowSearch.Unreadable error -> ports.Report(SearchFailed error)

let private searchingBytes (ports: Ports) (source: FileWindow.Source) (content: ContentView) query from =
    match ports.RunByteSearch(fun control -> ByteSearch.run control source query from) with
    | WindowSearch.Found hit -> content.Change(ViewerState.showBytesMatch content.Screen hit)
    | WindowSearch.NotFound -> ports.Report(BytesNotFound query.Bytes)
    | WindowSearch.Stopped -> ()
    | WindowSearch.Unreadable error -> ports.Report(SearchFailed error)

let private remembering (history: Marbas.State.Ports.SearchHistory) (answer: SearchAnswer) =
    EnteredText.tryCreate answer.Typed |> Option.iter history.Remember

let create (ports: Ports) (source: FileWindow.Source) =
    let window = new Window(Width = Dim.Fill(), Height = Dim.Fill())

    window.BorderStyle <- LineStyle.None
    window.SetScheme ClassicScheme.panel |> ignore

    let header =
        new Label(Id = HeaderId, X = 0, Y = 0, Width = Dim.Fill(), Height = 1)
        |> LiteralText.showing

    header.SetScheme ClassicScheme.keyBar |> ignore

    let content =
        new ContentView(source, X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill 1)

    let keyBar =
        KeyMap.functionKeys
        |> List.mapi (fun index _ -> $"F{index + 1}")
        |> FunctionKeyBar.build KeyBarId

    let refresh () =
        header.Text <-
            ViewerHeader.describe
                (max 1 header.Viewport.Width)
                { Name = FileSystemPath.value source.Path
                  Mode = content.Mode
                  Encoding = TextEncoding.name content.Encoding
                  Length = source.Length
                  Place = content.Place
                  Percent = content.Percent }

        FunctionKeyBar.label keyBar (ViewerKeys.bar content.Mode content.Wrap)
        content.SetNeedsDraw()

    let textFromScreen (query: WindowSearch.Query) =
        content.Change(ViewerState.searchedFor query)

        ViewerState.screenPlace content.Screen query.Direction content.State
        |> searching ports source content query

    let bytesFromScreen (query: ByteSearch.Query) =
        content.Change(ViewerState.searchedForBytes query)

        ViewerState.bytesScreenPlace content.Screen query.Direction content.State
        |> searchingBytes ports source content query

    let askedFor (answer: SearchAnswer) =
        match ViewerSearch.query answer with
        | Some(Ok(TextAsked query)) ->
            remembering ports.Searches answer
            textFromScreen query
        | Some(Ok(BytesAsked query)) ->
            remembering ports.ByteSearches answer
            bytesFromScreen query
        | Some(Error refusal) -> ports.Report(Refused refusal)
        | None -> ()

    let offer () =
        match content.Mode with
        | TextMode ->
            { Remembered = ports.Searches.Texts()
              Last = LastText content.State.LastTextQuery }
        | HexMode ->
            { Remembered = ports.ByteSearches.Texts()
              Last = LastBytes content.State.LastBytesQuery }

    let askSearch () =
        offer () |> ports.AskSearch |> Option.iter askedFor

    let searchAgain () =
        let state = content.State

        match state.Mode, state.LastTextQuery, state.LastBytesQuery with
        | TextMode, Some query, _ ->
            ViewerState.placeAfterMatch content.Screen query.Direction state
            |> searching ports source content query
        | HexMode, _, Some query ->
            ViewerState.placeAfterBytesMatch content.Screen query.Direction state
            |> searchingBytes ports source content query
        | TextMode, None, _
        | HexMode, _, None -> askSearch ()

    content.KeyDown.Add(fun (key: Key) ->
        match ViewerKeys.commandFor key with
        | Some ViewerKeys.Closed ->
            key.Handled <- true
            ports.Close window
        | Some ViewerKeys.CodePageAsked ->
            key.Handled <- true

            ports.ChooseCodePage
                { Detected = source.Encoding
                  Current = content.CodePage }
            |> Option.iter (fun page -> content.Change(ViewerState.readAs page))

            refresh ()
        | Some ViewerKeys.SearchAsked ->
            key.Handled <- true
            askSearch ()
            refresh ()
        | Some ViewerKeys.SearchRepeated ->
            key.Handled <- true
            searchAgain ()
            refresh ()
        | Some command ->
            key.Handled <- true
            content.Change(ViewerState.run content.Screen command)
            refresh ()
        | None -> ())

    window.SubViewsLaidOut.Add(fun _ -> refresh ())
    window.Add(header, content, keyBar) |> ignore
    content.SetFocus() |> ignore
    refresh ()
    window

let content (window: Window) = SubView.byType<ContentView> window

let header (window: Window) = SubView.byId<View> HeaderId window

let keyBar (window: Window) = SubView.byId<View> KeyBarId window
