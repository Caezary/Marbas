module Marbas.Tests.PanelWiringTests

open System.Collections.ObjectModel
open System.Drawing
open Xunit
open Terminal.Gui.Input
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Ui
open Marbas.Tests.TestHelpers

type private Probe =
    { Host: PanelWiring.Host
      Messages: ResizeArray<Message>
      Refreshes: int ref
      Rendering: bool ref }

let private probe () =
    let messages = ResizeArray<Message>()
    let refreshes = ref 0
    let rendering = ref false

    { Host =
        { Dispatch = messages.Add
          Refresh = fun () -> refreshes.Value <- refreshes.Value + 1
          IsRendering = fun () -> rendering.Value }
      Messages = messages
      Refreshes = refreshes
      Rendering = rendering }

let private wiredTo side =
    let probe = probe ()
    let panels = FilePanel.createPair ()
    let view = if side = Left then panels.Left else panels.Right
    PanelWiring.attach probe.Host side view
    let list = FilePanel.listView view
    list.SetSource(ObservableCollection [ ".."; "docs"; "readme.txt" ])
    probe.Messages.Clear()
    probe, view, list

let private wired () = wiredTo Left

[<Fact>]
let ``moving the cursor is dispatched`` () =
    let probe, _, list = wired ()

    list.SelectedItem <- 2

    Assert.Contains(CursorMoved(Left, 2), probe.Messages)

[<Fact>]
let ``a cursor change raised while rendering is ignored`` () =
    let probe, _, list = wired ()
    probe.Rendering.Value <- true

    list.SelectedItem <- 2

    Assert.Empty probe.Messages

[<Fact>]
let ``accepting an entry opens it at the cursor`` () =
    let probe, _, list = wired ()
    list.SelectedItem <- 1
    probe.Messages.Clear()

    press list Key.Enter

    Assert.Contains(EntryOpened(Left, 1), probe.Messages)

[<Fact>]
let ``panel keys are translated by PanelInput`` () =
    let probe, _, list = wired ()
    list.SelectedItem <- 1
    probe.Messages.Clear()

    press list PanelInput.refreshKey

    Assert.Contains(RefreshRequested(Left, 1), probe.Messages)

[<Fact>]
let ``an unknown key is left to the view`` () =
    let probe, _, list = wired ()

    press list (Key 'z')

    Assert.Empty probe.Messages

[<Fact>]
let ``gaining focus activates the panel`` () =
    let probe, view, _ = wired ()

    view.SetFocus() |> ignore

    Assert.Contains(PanelFocused Left, probe.Messages)

[<Fact>]
let ``a frame change asks the host to repaint`` () =
    let probe, view, _ = wired ()

    view.Layout(Size(40, 12)) |> ignore

    Assert.True(probe.Refreshes.Value > 0)

[<Fact>]
let ``messages carry the side the panel was wired to`` () =
    let probe, _, list = wiredTo Right

    list.SelectedItem <- 2

    Assert.Contains(CursorMoved(Right, 2), probe.Messages)
