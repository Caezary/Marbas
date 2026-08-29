module Marbas.Ui.App.AppView

open System.IO.Abstractions
open Terminal.Gui.Drawing
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.State
open Marbas.Ui.Theme
open Marbas.Ui.Panels

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

let render (fs: IFileSystem) (view: AppView) (state: AppState.AppState) =
    for side, panel in panels view do
        FilePanel.render fs (AppState.panel side state) panel

    let entry = state |> AppState.activePanel |> AppState.cursorEntry
    view.StatusLine |> StatusLine.render entry
