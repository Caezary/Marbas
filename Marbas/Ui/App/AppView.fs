module Marbas.Ui.App.AppView

open System.IO.Abstractions
open Terminal.Gui.Drawing
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Domain
open Marbas.State
open Marbas.Ui.Theme
open Marbas.Ui.Panels

type AppView =
    { Window: Window
      Panels: FilePanel.Panels
      StatusLine: Label
      TabCounter: Label }

let create () =
    let window = new Window(Title = "Marbas", Width = Dim.Fill(), Height = Dim.Fill())
    window.BorderStyle <- LineStyle.None
    window.SetScheme ClassicScheme.screen |> ignore
    let panels = FilePanel.createPair ()
    let statusLine = StatusLine.create ()
    let tabCounter = TabCounter.create ()
    window.Add(panels.Left, panels.Right, statusLine, tabCounter) |> ignore

    { Window = window
      Panels = panels
      StatusLine = statusLine
      TabCounter = tabCounter }

let panels (view: AppView) = FilePanel.pairs view.Panels

let render (fs: IFileSystem) (view: AppView) (state: AppState.AppState) =
    view.TabCounter |> TabCounter.render state

    let taken side =
        match side with
        | Left -> 0
        | Right -> (TabCounter.describe state).Length

    for side, panel in panels view do
        FilePanel.render fs (taken side) (AppState.panel side state) panel

    let entry = state |> AppState.activePanel |> PanelState.cursorEntry
    view.StatusLine |> StatusLine.render entry
