module Marbas.Ui.FunctionKeyBar

open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.State.Update

[<Literal>]
let Id = "functionKeyBar"

type SlotView(name: string) as this =
    inherit View()

    let keyName =
        new Label(Text = name, Width = Dim.Absolute name.Length, Height = 1)

    let commandName =
        new Label(X = Pos.Right keyName, Width = Dim.Fill(), Height = 1)

    do
        this.Height <- Dim.Absolute 1
        keyName.SetScheme ClassicScheme.keyName |> ignore
        this.Add(keyName, commandName) |> ignore

    member val Message: Message option = None with get, set
    member _.KeyName = keyName
    member _.CommandName = commandName

let slots (bar: View) =
    bar.SubViews
    |> Seq.choose (function
        | :? SlotView as slot -> Some slot
        | _ -> None)
    |> List.ofSeq

let build id (names: string list) =
    let bar =
        new View(Id = id, Y = Pos.AnchorEnd 1, Width = Dim.Fill(), Height = 1)

    bar.SetScheme ClassicScheme.keyBar |> ignore
    let count = List.length names

    names
    |> List.iteri (fun index name ->
        let slot = new SlotView(name)
        slot.X <- Pos.Func((fun (view: View) -> (KeyBarLayout.cell count index view.Viewport.Width).X), bar)
        slot.Width <- Dim.Func((fun (view: View) -> (KeyBarLayout.cell count index view.Viewport.Width).Width), bar)
        bar.Add slot |> ignore)

    bar

let label (bar: View) (labels: string list) =
    List.iter2 (fun (view: SlotView) text -> view.CommandName.Text <- text) (slots bar) labels
    bar.SetNeedsDraw()

let show (bar: View) modifiers =
    List.iter2
        (fun (view: SlotView) (slot: KeyMap.BarSlot) ->
            view.CommandName.Text <- slot.Label
            view.Message <- slot.Message)
        (slots bar)
        (KeyMap.bar modifiers)

    bar.SetNeedsDraw()

let private dispatching (dispatch: Message -> unit) (slot: SlotView) =
    let clicked (mouse: Mouse) =
        if mouse.Flags.HasFlag MouseFlags.LeftButtonClicked then
            slot.Message |> Option.iter dispatch
            mouse.Handled <- true

    for view in (slot :> View) :: List.ofSeq slot.SubViews do
        view.MouseEvent.Add clicked

let create (dispatch: Message -> unit) =
    let bar =
        KeyMap.bar Modifiers.none |> List.map _.Name |> build Id

    for slot in slots bar do
        dispatching dispatch slot

    show bar Modifiers.none
    bar
