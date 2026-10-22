module Marbas.Ui.Help.HelpView

open System.Drawing
open Terminal.Gui.Drawing
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Guide
open Marbas.Ui
open Marbas.Ui.Theme
open Marbas.Ui.Format
open Marbas.Ui.Keys

[<Literal>]
let ContentId = "helpContent"

[<Literal>]
let HeaderId = "helpHeader"

[<Literal>]
let KeyBarId = "helpKeyBar"

type Ports =
    { Close: Window -> unit
      Read: ChapterName -> Result<Chapter, GuideFault>
      Report: GuideFault -> unit }

let private textOf (line: LaidLine) =
    line.Pieces |> List.map _.Text |> String.concat ""

type ContentView(chapter: Chapter) as this =
    inherit View()

    let mutable state = HelpState.start chapter

    do
        this.Id <- ContentId
        this.CanFocus <- true

    let screen () =
        Size(max 1 this.Viewport.Width, max 1 this.Viewport.Height)

    let fit () =
        state <- HelpState.fitted (screen ()) state

    member _.State =
        fit ()
        state

    member _.Change(change: HelpState -> HelpState) =
        fit ()
        state <- change state

    member this.Rows() =
        HelpState.shown this.State |> List.map textOf

    override this.OnDrawingContent(_: DrawContext) =
        let shown = HelpState.shown this.State
        let width = screen().Width

        for row in 0 .. screen().Height - 1 do
            this.Move(0, row) |> ignore

            let pieces =
                shown |> List.tryItem row |> Option.map _.Pieces |> Option.defaultValue []

            for piece in pieces do
                this.SetAttribute(HelpColours.attributeOf state.Page.Link piece) |> ignore
                this.AddStr piece.Text

            this.SetAttribute ClassicScheme.helpText |> ignore
            this.AddStr(String.replicate (max 0 (width - (pieces |> List.sumBy _.Text.Length))) " ")

        true

let private following (ports: Ports) (content: ContentView) =
    match HelpState.follow ports.Read content.State with
    | Ok followed -> content.Change(fun _ -> followed)
    | Error fault -> ports.Report fault

let create (ports: Ports) (chapter: Chapter) =
    let window = new Window(Width = Dim.Fill(), Height = Dim.Fill())

    window.BorderStyle <- LineStyle.None
    window.SetScheme ClassicScheme.panel |> ignore

    let header =
        new Label(Id = HeaderId, X = 0, Y = 0, Width = Dim.Fill(), Height = 1)
        |> LiteralText.showing

    header.SetScheme ClassicScheme.keyBar |> ignore

    let content =
        new ContentView(chapter, X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill 1)

    let keyBar =
        KeyMap.functionKeys
        |> List.mapi (fun index _ -> $"F{index + 1}")
        |> FunctionKeyBar.build KeyBarId

    let refresh () =
        header.Text <- HelpState.title content.State
        FunctionKeyBar.label keyBar HelpKeys.bar
        content.SetNeedsDraw()

    content.KeyDown.Add(fun (key: Key) ->
        match HelpKeys.commandFor key with
        | Some HelpKeys.Closed ->
            key.Handled <- true
            ports.Close window
        | Some HelpKeys.Follow ->
            key.Handled <- true
            following ports content
            refresh ()
        | Some command ->
            key.Handled <- true
            content.Change(HelpState.run command)
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
