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
        new View(Id = Id, Y = Pos.AnchorEnd 1, Width = Dim.Fill(), Height = 1)

    bar.SetScheme ClassicScheme.keyBar |> ignore
    let count = List.length KeyMap.functionKeys

    KeyMap.bar Modifiers.none
    |> List.iteri (fun index barSlot ->
        let slot = new SlotView(barSlot.Name)
        slot.X <- Pos.Func((fun (view: View) -> (KeyBarLayout.cell count index view.Viewport.Width).X), bar)
        slot.Width <- Dim.Func((fun (view: View) -> (KeyBarLayout.cell count index view.Viewport.Width).Width), bar)
        dispatching dispatch slot
        bar.Add slot |> ignore)

    show bar Modifiers.none
    bar
