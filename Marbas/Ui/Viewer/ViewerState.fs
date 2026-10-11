namespace Marbas.Ui.Viewer

open System.Drawing
open Marbas.Domain
open Marbas.FileSystem

type ViewerState =
    { Source: FileWindow.Source
      CodePage: CodePage
      Encoding: TextEncoding
      Index: LineIndex.Index
      Mode: ViewerMode
      Wrap: ViewerWrap
      Top: int
      Left: int
      Found: FoundSpan option
      LastTextMatch: WindowSearch.Match option
      LastBytesMatch: ByteSearch.Match option
      LastTextQuery: WindowSearch.Query option
      LastBytesQuery: ByteSearch.Query option }

module ViewerState =

    let start (source: FileWindow.Source) =
        { Source = source
          CodePage = Automatic
          Encoding = source.Encoding
          Index = LineIndex.Index(source, source.Encoding)
          Mode = TextMode
          Wrap = Unwrapped
          Top = 0
          Left = 0
          Found = None
          LastTextMatch = None
          LastBytesMatch = None
          LastTextQuery = None
          LastBytesQuery = None }

    let private hexRow (state: ViewerState) row =
        HexDump.bytesAt state.Source row
        |> Option.map (HexDump.row state.Encoding (HexDump.offsetOf row))

    let private textLine (state: ViewerState) line =
        state.Index.Text line |> Option.map PrintableText.describe

    let private rowAt (state: ViewerState) row =
        match state.Mode with
        | HexMode -> hexRow state row
        | TextMode -> textLine state row

    let private lineHeight (screen: Size) (state: ViewerState) line =
        textLine state line
        |> Option.map (LineWrap.height screen.Width)
        |> Option.defaultValue 1

    let private segmentsOf (screen: Size) line text =
        LineWrap.segments screen.Width text
        |> List.mapi (fun segment piece ->
            ({ Line = line
               From = segment * screen.Width
               Text = piece }
            : ShownRow))

    let private wrappedRows (screen: Size) (state: ViewerState) =
        let rec gather line count (wrapped: ShownRow list list) =
            match count >= screen.Height, textLine state line with
            | true, _
            | _, None -> wrapped, line - state.Top
            | false, Some text ->
                let segments = segmentsOf screen line text
                gather (line + 1) (count + List.length segments) (segments :: wrapped)

        let wrapped, lines = gather state.Top 0 []
        wrapped |> List.rev |> List.concat |> List.truncate screen.Height, max 1 lines

    let private plainRows (screen: Size) (state: ViewerState) =
        let rows =
            [ for row in state.Top .. state.Top + screen.Height - 1 do
                  match rowAt state row with
                  | Some text ->
                      yield
                          ({ Line = row
                             From = state.Left
                             Text = LineWrap.visible screen.Width state.Left text }
                          : ShownRow)
                  | None -> () ]

        rows, max 1 (List.length rows)

    let private visible (screen: Size) (state: ViewerState) =
        match state.Mode, state.Wrap with
        | TextMode, Wrapped -> wrappedRows screen state
        | _ -> plainRows screen state

    let shown (screen: Size) (state: ViewerState) = visible screen state |> fst

    let rows (screen: Size) (state: ViewerState) = shown screen state |> List.map _.Text

    let private lastRow (state: ViewerState) =
        match state.Mode with
        | HexMode -> HexDump.rowCount state.Source.Length - 1
        | TextMode ->
            state.Index.ScanAll()
            state.Index.Known - 1

    let private shows (state: ViewerState) row =
        match state.Mode with
        | HexMode -> HexDump.offsetOf row < state.Source.Length
        | TextMode -> state.Index.Start row |> Option.isSome

    let private goTo row (state: ViewerState) =
        let wanted = max 0 row

        { state with
            Top = if shows state wanted then wanted else max 0 (lastRow state) }

    let private pageStep (screen: Size) (state: ViewerState) lineAt =
        match state.Mode, state.Wrap with
        | TextMode, Wrapped ->
            Seq.initInfinite (lineAt >> lineHeight screen state)
            |> ViewerScroll.filling screen.Height
        | _ -> screen.Height

    let private widest (screen: Size) (state: ViewerState) =
        [ yield 0
          for row in state.Top .. state.Top + screen.Height - 1 do
              match rowAt state row with
              | Some text -> yield text.Length
              | None -> () ]
        |> List.max

    let private scrollAcross (screen: Size) delta (state: ViewerState) =
        match state.Wrap with
        | Wrapped -> state
        | Unwrapped ->
            { state with
                Left = ViewerScroll.clamped (max 0 (widest screen state - screen.Width)) (state.Left + delta) }

    let private toEnd (screen: Size) (state: ViewerState) =
        let last = max 0 (lastRow state)

        { state with
            Top = max 0 (last - pageStep screen state (fun step -> last - step) + 1) }

    let private toggledWrap (state: ViewerState) =
        match state.Wrap with
        | Wrapped ->
            { state with
                Wrap = Unwrapped
                Left = 0 }
        | Unwrapped -> { state with Wrap = Wrapped; Left = 0 }

    let private switchedMode (state: ViewerState) =
        match state.Mode with
        | TextMode ->
            let offset = state.Index.Start state.Top |> Option.defaultValue 0L

            { state with
                Mode = HexMode
                Top = int (offset / int64 HexDump.BytesPerLine)
                Left = 0 }
        | HexMode ->
            { state with
                Mode = TextMode
                Top = state.Index.LineAt(HexDump.offsetOf state.Top)
                Left = 0 }

    let run (screen: Size) (command: ViewerKeys.Command) (state: ViewerState) =
        let top = state.Top

        match command with
        | ViewerKeys.LineUp -> goTo (top - 1) state
        | ViewerKeys.LineDown -> goTo (top + 1) state
        | ViewerKeys.PageDown -> goTo (top + pageStep screen state (fun step -> top + step)) state
        | ViewerKeys.PageUp -> goTo (top - pageStep screen state (fun step -> top - 1 - step)) state
        | ViewerKeys.ColumnLeft -> scrollAcross screen -1 state
        | ViewerKeys.ColumnRight -> scrollAcross screen 1 state
        | ViewerKeys.PageLeft -> scrollAcross screen (-screen.Width) state
        | ViewerKeys.PageRight -> scrollAcross screen screen.Width state
        | ViewerKeys.ToStart -> { state with Top = 0; Left = 0 }
        | ViewerKeys.ToEnd -> toEnd screen state
        | ViewerKeys.WrapToggled -> toggledWrap state
        | ViewerKeys.ModeToggled -> switchedMode state
        | ViewerKeys.SearchAsked
        | ViewerKeys.SearchRepeated
        | ViewerKeys.CodePageAsked
        | ViewerKeys.Closed -> state

    let private reindexed (page: CodePage) (state: ViewerState) =
        let probe = state.Source.Read(0L, TextEncoding.ProbeLength)
        let encoding = CodePage.encoding state.Source.Encoding probe page

        { state with
            CodePage = page
            Encoding = encoding
            Index = LineIndex.Index(state.Source, encoding) }

    let readAs (page: CodePage) (state: ViewerState) =
        let read =
            reindexed
                page
                { state with
                    Found = None
                    LastTextMatch = None }

        match state.Mode with
        | TextMode ->
            let offset = state.Index.Start state.Top |> Option.defaultValue 0L

            { read with
                Top = read.Index.LineAt offset
                Left = 0 }
        | HexMode -> read

    let place (state: ViewerState) =
        match state.Mode with
        | TextMode -> ViewerHeader.AtLine state.Top
        | HexMode -> ViewerHeader.AtOffset(HexDump.offsetOf state.Top)

    let percent (screen: Size) (state: ViewerState) =
        let lines = visible screen state |> snd

        let reached =
            match state.Mode with
            | HexMode -> min state.Source.Length (HexDump.offsetOf (state.Top + lines))
            | TextMode -> state.Index.Start(state.Top + lines) |> Option.defaultValue state.Source.Length

        ViewerScroll.percent reached state.Source.Length

    let private spansOn (screenful: ShownRow list) (spansOf: ShownRow -> FoundSpan list) =
        screenful
        |> List.indexed
        |> List.collect (fun (row, shown) ->
            spansOf shown
            |> List.choose (fun span ->
                FoundSpan.onRow shown span
                |> Option.map (fun (start, length) -> row, start, length)))

    let private bytesMatchSpans (hit: ByteSearch.Match) (shown: ShownRow) =
        HexMark.spans hit shown.Line
        |> List.map (fun (first, until) ->
            ({ Line = shown.Line
               From = first
               Until = until }
            : FoundSpan))

    let foundOn (screenful: ShownRow list) (state: ViewerState) =
        match state.Mode, state.Found, state.LastBytesMatch with
        | TextMode, Some span, _ -> spansOn screenful (fun _ -> [ span ])
        | HexMode, _, Some hit -> spansOn screenful (bytesMatchSpans hit)
        | TextMode, None, _
        | HexMode, _, None -> []

    let foundOnScreen (screen: Size) (state: ViewerState) = foundOn (shown screen state) state

    let private reaches (state: ViewerState) (span: FoundSpan) (row: ShownRow) =
        match state.Wrap with
        | Unwrapped -> row.Line = span.Line
        | Wrapped -> FoundSpan.startsOn row span

    let private showingAcross (screen: Size) (span: FoundSpan) (state: ViewerState) =
        { state with
            Left = FoundSpan.leftShowing state.Left screen.Width span }

    let show (screen: Size) (hit: WindowSearch.Match) (state: ViewerState) =
        let line = state.Index.LineAt hit.LineStart
        let raw = state.Index.Text line |> Option.defaultValue ""
        let span = FoundSpan.ofMatch line raw hit.Column hit.Length

        let marked =
            { state with
                Found = Some span
                LastTextMatch = Some hit }

        let reached =
            if shown screen marked |> List.exists (reaches marked span) then
                marked
            else
                goTo line marked

        match reached.Wrap with
        | Unwrapped -> showingAcross screen span reached
        | Wrapped -> reached

    let private lineStart (state: ViewerState) line =
        state.Index.Start line |> Option.defaultValue state.Source.Length

    let screenPlace (screen: Size) (direction: SearchDirection) (state: ViewerState) : WindowSearch.Place =
        match direction with
        | Forwards ->
            { LineStart = lineStart state state.Top
              Column = 0 }
        | Backwards ->
            let bottom =
                shown screen state
                |> List.tryLast
                |> Option.map _.Line
                |> Option.defaultValue state.Top

            { LineStart = lineStart state bottom
              Column = state.Index.Text bottom |> Option.map String.length |> Option.defaultValue 0 }

    let placeAfterMatch (screen: Size) (direction: SearchDirection) (state: ViewerState) : WindowSearch.Place =
        match state.LastTextMatch, direction with
        | Some hit, Forwards ->
            { LineStart = hit.LineStart
              Column = hit.Column + hit.Length }
        | Some hit, Backwards ->
            { LineStart = hit.LineStart
              Column = hit.Column }
        | None, _ -> screenPlace screen direction state

    let showBytesMatch (screen: Size) (hit: ByteSearch.Match) (state: ViewerState) =
        let row = int (hit.Offset / int64 HexDump.BytesPerLine)
        let marked = { state with LastBytesMatch = Some hit }

        let reached =
            if shown screen marked |> List.exists (fun onScreen -> onScreen.Line = row) then
                marked
            else
                goTo row marked

        bytesMatchSpans hit { Line = row; From = 0; Text = "" }
        |> List.tryHead
        |> Option.map (fun span -> showingAcross screen span reached)
        |> Option.defaultValue reached

    let bytesScreenPlace (screen: Size) (direction: SearchDirection) (state: ViewerState) =
        match direction with
        | Forwards -> HexDump.offsetOf state.Top
        | Backwards -> min state.Source.Length (HexDump.offsetOf (state.Top + List.length (shown screen state)))

    let placeAfterBytesMatch (screen: Size) (direction: SearchDirection) (state: ViewerState) =
        match state.LastBytesMatch, direction with
        | Some hit, Forwards -> hit.Offset + int64 hit.Length
        | Some hit, Backwards -> hit.Offset
        | None, _ -> bytesScreenPlace screen direction state

    let searchedFor (query: WindowSearch.Query) (state: ViewerState) =
        { state with
            LastTextQuery = Some query }

    let searchedForBytes (query: ByteSearch.Query) (state: ViewerState) =
        { state with
            LastBytesQuery = Some query }
