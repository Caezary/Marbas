module Marbas.Ui.FilePanel

open System.Collections.ObjectModel
open System.IO.Abstractions
open Terminal.Gui.Drawing
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.FileSystem

[<Literal>]
let LeftId = "leftPanel"

[<Literal>]
let RightId = "rightPanel"

let private focusable (panel: FrameView) =
    panel.CanFocus <- true
    panel.TabStop <- TabBehavior.TabStop
    panel.BorderStyle <- LineStyle.Single

    panel.HasFocusChanged.Add(fun _ ->
        panel.BorderStyle <- if panel.HasFocus then LineStyle.Double else LineStyle.Single)

    panel.Add(new ListView(Width = Dim.Fill(), Height = Dim.Fill())) |> ignore
    panel

let listView (panel: View) =
    panel.SubViews
    |> Seq.pick (function
        | :? ListView as list -> Some list
        | _ -> None)

let load (fs: IFileSystem) (path: string) (panel: FrameView) =
    DirectoryListing.listForDisplay fs path
    |> Result.map (fun entries ->
        panel.Title <- path
        let list = listView panel

        let refresh () =
            let width = list.Viewport.Width

            let rows =
                if width > 0 then
                    entries |> List.map (EntryFormat.format width)
                else
                    entries |> List.map (fun e -> e.Name)

            list.SetSource(ObservableCollection rows)

        refresh ()
        list.FrameChanged.Add(fun _ -> refresh ()))

let createPair () =
    let left =
        new FrameView(Id = LeftId, Width = Dim.Percent 50, Height = Dim.Fill 1)

    let right =
        new FrameView(Id = RightId, X = Pos.Right left, Width = Dim.Fill(), Height = Dim.Fill 1)

    focusable left, focusable right
