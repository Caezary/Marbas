module Marbas.Ui.Viewer.FileViewer

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
      Searches: SearchHistory
      RunSearch: (WindowSearch.Control -> WindowSearch.Answer) -> WindowSearch.Answer
      Report: SearchReport -> unit }

type ContentView(source: FileWindow.Source) as this =
    inherit View()

    let mutable codePage = Automatic
    let mutable encoding = source.Encoding
    let mutable index = LineIndex.Index(source, encoding)
    let mutable mode = TextMode
    let mutable wrap = Unwrapped
    let mutable top = 0
    let mutable left = 0
    let mutable found: FoundSpan option = None
    let mutable lastMatch: WindowSearch.Match option = None

    do
        this.Id <- ContentId
        this.CanFocus <- true

    let width () = max 1 this.Viewport.Width

    let height () = max 1 this.Viewport.Height

    let hexRow row =
        HexDump.bytesAt source row
        |> Option.map (HexDump.row encoding (HexDump.offsetOf row))

    let textLine line =
        index.Text line |> Option.map PrintableText.describe

    let rowAt row =
        match mode with
        | HexMode -> hexRow row
        | TextMode -> textLine row

    let lineHeight line =
        textLine line |> Option.map (LineWrap.height (width ())) |> Option.defaultValue 1

    let segmentsOf line text =
        LineWrap.segments (width ()) text
        |> List.mapi (fun segment piece ->
            ({ Line = line
               From = segment * width ()
               Text = piece }
            : ShownRow))

    let wrappedRows room =
        let rec gather line count (wrapped: ShownRow list list) =
            match count >= room, textLine line with
            | true, _
            | _, None -> wrapped, line - top
            | false, Some text ->
                let segments = segmentsOf line text
                gather (line + 1) (count + List.length segments) (segments :: wrapped)

        let wrapped, lines = gather top 0 []
        wrapped |> List.rev |> List.concat |> List.truncate room, max 1 lines

    let plainRows room =
        let rows =
            [ for row in top .. top + room - 1 do
                  match rowAt row with
                  | Some text ->
                      yield
                          ({ Line = row
                             From = left
                             Text = LineWrap.visible (width ()) left text }
                          : ShownRow)
                  | None -> () ]

        rows, max 1 (List.length rows)

    let mutable shown = None

    let visible () =
        let place = encoding, mode, wrap, top, left, width (), height ()

        match shown with
        | Some(shownAt, rows) when shownAt = place -> rows
        | _ ->
            let rows =
                match mode, wrap with
                | TextMode, Wrapped -> wrappedRows (height ())
                | _ -> plainRows (height ())

            shown <- Some(place, rows)
            rows

    let lastRow () =
        match mode with
        | HexMode -> HexDump.rowCount source.Length - 1
        | TextMode ->
            index.ScanAll()
            index.Known - 1

    let shows row =
        match mode with
        | HexMode -> HexDump.offsetOf row < source.Length
        | TextMode -> index.Start row |> Option.isSome

    let goTo row =
        let wanted = max 0 row
        top <- if shows wanted then wanted else max 0 (lastRow ())

    let pageStep lineAt =
        match mode, wrap with
        | TextMode, Wrapped -> ViewerScroll.filling (height ()) (Seq.initInfinite (lineAt >> lineHeight))
        | _ -> height ()

    let widest () =
        [ yield 0
          for row in top .. top + height () - 1 do
              match rowAt row with
              | Some text -> yield text.Length
              | None -> () ]
        |> List.max

    let scrollAcross delta =
        if wrap = Unwrapped then
            left <- ViewerScroll.clamped (max 0 (widest () - width ())) (left + delta)

    let switchMode () =
        match mode with
        | TextMode ->
            let offset = index.Start top |> Option.defaultValue 0L
            mode <- HexMode
            top <- int (offset / int64 HexDump.BytesPerLine)
        | HexMode ->
            let offset = HexDump.offsetOf top
            mode <- TextMode
            top <- index.LineAt offset

        left <- 0

    let reindex page =
        let probe = source.Read(0L, TextEncoding.ProbeLength)
        codePage <- page
        encoding <- CodePage.encoding source.Encoding probe page
        index <- LineIndex.Index(source, encoding)

    let shownRows () = visible () |> fst

    let foundOnScreen () =
        match mode, found with
        | TextMode, Some span ->
            shownRows ()
            |> List.indexed
            |> List.choose (fun (row, shown) ->
                FoundSpan.onRow shown span
                |> Option.map (fun (start, length) -> row, start, length))
        | TextMode, None
        | HexMode, _ -> []

    let reaches (span: FoundSpan) (row: ShownRow) =
        match wrap with
        | Unwrapped -> row.Line = span.Line
        | Wrapped -> FoundSpan.startsOn row span

    let show (hit: WindowSearch.Match) =
        let line = index.LineAt hit.LineStart
        let raw = index.Text line |> Option.defaultValue ""
        let span = FoundSpan.ofMatch line raw hit.Column hit.Length
        found <- Some span
        lastMatch <- Some hit

        if not (shownRows () |> List.exists (reaches span)) then
            goTo line

        if wrap = Unwrapped then
            left <- FoundSpan.leftShowing left (width ()) span

    let lineStart line =
        index.Start line |> Option.defaultValue source.Length

    let screenPlace direction : WindowSearch.Place =
        match direction with
        | Forwards -> { LineStart = lineStart top; Column = 0 }
        | Backwards ->
            let bottom = shownRows () |> List.tryLast |> Option.map _.Line |> Option.defaultValue top

            { LineStart = lineStart bottom
              Column = index.Text bottom |> Option.map String.length |> Option.defaultValue 0 }

    let placeAfterMatch direction : WindowSearch.Place =
        match lastMatch, direction with
        | Some hit, Forwards ->
            { LineStart = hit.LineStart
              Column = hit.Column + hit.Length }
        | Some hit, Backwards ->
            { LineStart = hit.LineStart
              Column = hit.Column }
        | None, _ -> screenPlace direction

    let readAs page =
        found <- None
        lastMatch <- None

        match mode with
        | TextMode ->
            let offset = index.Start top |> Option.defaultValue 0L
            reindex page
            top <- index.LineAt offset
            left <- 0
        | HexMode -> reindex page

    member _.Source = source

    member _.CodePage = codePage

    member _.Encoding = encoding

    member _.Mode = mode

    member _.Wrap = wrap

    member _.Top = top

    member _.Left = left

    member _.Rows() = shownRows () |> List.map _.Text

    member _.FoundOnScreen() = foundOnScreen ()

    member _.ScreenPlace(direction: SearchDirection) = screenPlace direction

    member _.PlaceAfterMatch(direction: SearchDirection) = placeAfterMatch direction

    member _.Show(hit: WindowSearch.Match) = show hit

    member _.Place =
        match mode with
        | TextMode -> ViewerHeader.AtLine top
        | HexMode -> ViewerHeader.AtOffset(HexDump.offsetOf top)

    member _.Percent =
        let lines = visible () |> snd

        let reached =
            match mode with
            | HexMode -> min source.Length (HexDump.offsetOf (top + lines))
            | TextMode -> index.Start(top + lines) |> Option.defaultValue source.Length

        ViewerScroll.percent reached source.Length

    member _.Run(command: ViewerKeys.Command) =
        match command with
        | ViewerKeys.LineUp -> goTo (top - 1)
        | ViewerKeys.LineDown -> goTo (top + 1)
        | ViewerKeys.PageDown -> goTo (top + pageStep (fun step -> top + step))
        | ViewerKeys.PageUp -> goTo (top - pageStep (fun step -> top - 1 - step))
        | ViewerKeys.ColumnLeft -> scrollAcross -1
        | ViewerKeys.ColumnRight -> scrollAcross 1
        | ViewerKeys.PageLeft -> scrollAcross -(width ())
        | ViewerKeys.PageRight -> scrollAcross (width ())
        | ViewerKeys.ToStart ->
            top <- 0
            left <- 0
        | ViewerKeys.ToEnd ->
            let last = max 0 (lastRow ())
            top <- max 0 (last - pageStep (fun step -> last - step) + 1)
        | ViewerKeys.WrapToggled ->
            wrap <- (if wrap = Wrapped then Unwrapped else Wrapped)
            left <- 0
        | ViewerKeys.ModeToggled -> switchMode ()
        | ViewerKeys.SearchAsked
        | ViewerKeys.SearchRepeated
        | ViewerKeys.CodePageAsked
        | ViewerKeys.Closed -> ()

    member _.ReadAs(page: CodePage) = readAs page

    override this.OnDrawingContent(_: DrawContext) =
        let rows = this.Rows()
        let room = width ()
        let normal = this.GetCurrentAttribute()

        for row in 0 .. height () - 1 do
            let text = rows |> List.tryItem row |> Option.defaultValue ""
            this.Move(0, row) |> ignore
            this.AddStr(text.PadRight room)

        this.SetAttribute ClassicScheme.foundText |> ignore

        for row, start, length in foundOnScreen () do
            this.Move(start, row) |> ignore
            this.AddStr(rows[row].Substring(start, length))

        this.SetAttribute normal |> ignore
        true

let private searching (ports: Ports) (source: FileWindow.Source) (content: ContentView) query place =
    let encoding = content.Encoding

    match ports.RunSearch(fun control -> WindowSearch.run control source encoding query place) with
    | WindowSearch.Found hit -> content.Show hit
    | WindowSearch.NotFound -> ports.Report(TextNotFound query.Wanted)
    | WindowSearch.Stopped -> ()
    | WindowSearch.Unreadable error -> ports.Report(SearchFailed error)

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

    let lastSearch = ref None

    let searchFromScreen (query: WindowSearch.Query) =
        lastSearch.Value <- Some query
        searching ports source content query (content.ScreenPlace query.Direction)

    let searchAsTyped (answer: SearchAnswer) query =
        EnteredText.tryCreate answer.Typed |> Option.iter ports.Searches.Remember
        searchFromScreen query

    let askedFor (answer: SearchAnswer) =
        match ViewerSearch.query answer with
        | Some(Ok query) -> searchAsTyped answer query
        | Some(Error error) -> ports.Report(PatternRefused error)
        | None -> ()

    let askSearch () =
        ports.AskSearch
            { Remembered = ports.Searches.Texts()
              Last = lastSearch.Value }
        |> Option.iter askedFor

    let searchAgain () =
        match lastSearch.Value with
        | Some(query: WindowSearch.Query) ->
            searching ports source content query (content.PlaceAfterMatch query.Direction)
        | None -> askSearch ()

    let inText act =
        match content.Mode with
        | TextMode -> act ()
        | HexMode -> ()

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
            |> Option.iter content.ReadAs

            refresh ()
        | Some ViewerKeys.SearchAsked ->
            key.Handled <- true
            inText askSearch
            refresh ()
        | Some ViewerKeys.SearchRepeated ->
            key.Handled <- true
            inText searchAgain
            refresh ()
        | Some command ->
            key.Handled <- true
            content.Run command
            refresh ()
        | None -> ())

    window.SubViewsLaidOut.Add(fun _ -> refresh ())
    window.Add(header, content, keyBar) |> ignore
    content.SetFocus() |> ignore
    refresh ()
    window

let content (window: Window) =
    SubView.byType<ContentView> window

let header (window: Window) =
    SubView.byId<View> HeaderId window

let keyBar (window: Window) =
    SubView.byId<View> KeyBarId window
