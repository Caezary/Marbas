module Marbas.Ui.Viewer.FileViewer

open Terminal.Gui.Drawing
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
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

type ContentView(source: FileWindow.Source) as this =
    inherit View()

    let index = LineIndex.Index source
    let mutable mode = TextMode
    let mutable wrap = Unwrapped
    let mutable top = 0
    let mutable left = 0

    do
        this.Id <- ContentId
        this.CanFocus <- true

    let width () = max 1 this.Viewport.Width

    let height () = max 1 this.Viewport.Height

    let hexRow row =
        let offset = HexDump.offsetOf row

        if offset >= source.Length then
            None
        else
            Some(HexDump.row offset (source.Read(offset, HexDump.BytesPerLine)))

    let textLine line =
        index.Text line |> Option.map PrintableText.describe

    let rowAt row =
        match mode with
        | HexMode -> hexRow row
        | TextMode -> textLine row

    let lineHeight line =
        textLine line |> Option.map (LineWrap.height (width ())) |> Option.defaultValue 1

    let visible () =
        let room = height ()

        match mode, wrap with
        | TextMode, Wrapped ->
            let rec gather line rows =
                if List.length rows >= room then
                    rows, line - top
                else
                    match textLine line with
                    | None -> rows, line - top
                    | Some text -> gather (line + 1) (rows @ LineWrap.segments (width ()) text)

            let rows, lines = gather top []
            List.truncate room rows, max 1 lines
        | _ ->
            let rows =
                [ for row in top .. top + room - 1 do
                      match rowAt row with
                      | Some text -> yield LineWrap.visible (width ()) left text
                      | None -> () ]

            rows, max 1 (List.length rows)

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

    member _.Source = source

    member _.Mode = mode

    member _.Wrap = wrap

    member _.Top = top

    member _.Left = left

    member _.Rows() = visible () |> fst

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
        | ViewerKeys.Closed -> ()

    override this.OnDrawingContent(_: DrawContext) =
        let rows = this.Rows()
        let room = width ()

        for row in 0 .. height () - 1 do
            let text = rows |> List.tryItem row |> Option.defaultValue ""
            this.Move(0, row) |> ignore
            this.AddStr(text.PadRight room)

        true

let create (close: Window -> unit) (source: FileWindow.Source) =
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
                  Encoding = TextEncoding.name source.Encoding
                  Length = source.Length
                  Place = content.Place
                  Percent = content.Percent }

        FunctionKeyBar.label keyBar (ViewerKeys.bar content.Mode content.Wrap)
        content.SetNeedsDraw()

    content.KeyDown.Add(fun (key: Key) ->
        match ViewerKeys.commandFor key with
        | Some ViewerKeys.Closed ->
            key.Handled <- true
            close window
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
