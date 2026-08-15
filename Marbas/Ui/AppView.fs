module Marbas.Ui.AppView

open Terminal.Gui.Drawing
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.State

type AppView =
    { Window: Window
      Panels: FilePanel.Panels
      StatusLine: Label }

let create () =
    let window = new Window(Title = "Marbas", Width = Dim.Fill(), Height = Dim.Fill())
    window.BorderStyle <- LineStyle.None
    window.SetScheme ClassicScheme.screen |> ignore
    let panels = FilePanel.createPair ()
    let statusLine = StatusLine.create ()
    window.Add(panels.Left, panels.Right, statusLine) |> ignore

    { Window = window
      Panels = panels
      StatusLine = statusLine }

let panels (view: AppView) = FilePanel.pairs view.Panels

let render (view: AppView) (state: AppState.AppState) =
    for side, panel in panels view do
        FilePanel.render (AppState.panel side state) panel

    let entry = state |> AppState.activePanel |> AppState.cursorEntry
    view.StatusLine |> StatusLine.render entry
