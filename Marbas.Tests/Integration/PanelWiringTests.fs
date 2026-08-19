module Marbas.Tests.Integration.PanelWiringTests

open System.Collections.ObjectModel
open System.Drawing
open Xunit
open Terminal.Gui.Input
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Ui
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private cursor side index = { Side = side; Index = index }

type private Probe =
    { Host: PanelWiring.Host
      Messages: ResizeArray<Message>
      Refreshes: int ref
      Rendering: bool ref
      Searching: bool ref }

let private probe () =
    let messages = ResizeArray<Message>()
    let refreshes = ref 0
    let rendering = ref false
    let searching = ref false

    { Host =
        { Dispatch = messages.Add
          Refresh = fun () -> refreshes.Value <- refreshes.Value + 1
          IsRendering = fun () -> rendering.Value
          IsSearching = fun _ -> searching.Value }
      Messages = messages
      Refreshes = refreshes
      Rendering = rendering
      Searching = searching }

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

    Assert.Contains(CursorMoved(cursor Left 2), probe.Messages)

[<Fact>]
let ``a cursor change raised while rendering is ignored`` () =
    let probe, _, list = wired ()
    probe.Rendering.Value <- true

    list.SelectedItem <- 2

    Assert.Empty probe.Messages

[<Fact>]
let ``accepting an entry opens it`` () =
    let probe, _, list = wired ()
    list.SelectedItem <- 1
    probe.Messages.Clear()

    press list Key.Enter

    Assert.Contains(EntryOpened, probe.Messages)

[<Fact>]
let ``panel keys are translated by KeyMap`` () =
    let probe, _, list = wired ()
    list.SelectedItem <- 1
    probe.Messages.Clear()

    press list KeyMap.refreshKey

    Assert.Contains(RefreshRequested, probe.Messages)

[<Fact>]
let ``a function key is translated by KeyMap too`` () =
    let probe, _, list = wired ()
    probe.Messages.Clear()

    press list Key.F5

    Assert.Contains(CopyRequested, probe.Messages)

[<Fact>]
let ``a key bound to nothing at all is left to the view`` () =
    let probe, _, list = wired ()

    let key = pressReturning list Key.Backspace

    Assert.Empty probe.Messages
    Assert.False key.Handled

[<Fact>]
let ``a printable key starts a search`` () =
    let probe, _, list = wired ()

    press list (Key 'z')

    Assert.Contains(SearchTyped 'z', probe.Messages)

[<Fact>]
let ``keys bound to a command never reach the search`` () =
    let probe, _, list = wired ()
    probe.Messages.Clear()

    press list KeyMap.spaceKey

    Assert.Contains(SelectionToggled, probe.Messages)
    Assert.DoesNotContain(SearchTyped ' ', probe.Messages)

[<Fact>]
let ``an open search takes every printable key, bound or not`` () =
    let probe, _, list = wired ()
    probe.Searching.Value <- true

    press list KeyMap.spaceKey
    press list (Key '+')

    Assert.Equal<Message list>([ SearchTyped ' '; SearchTyped '+' ], List.ofSeq probe.Messages)

[<Fact>]
let ``backspace and escape edit and close an open search`` () =
    let probe, _, list = wired ()
    probe.Searching.Value <- true

    press list Key.Backspace
    press list Key.Esc

    Assert.Equal<Message list>([ SearchBackspaced; SearchCancelled ], List.ofSeq probe.Messages)

[<Fact>]
let ``any other key closes an open search and then does its own job`` () =
    let probe, _, list = wired ()
    probe.Searching.Value <- true

    press list KeyMap.refreshKey

    Assert.Equal<Message list>([ SearchCancelled; RefreshRequested ], List.ofSeq probe.Messages)

[<Fact>]
let ``the cursor stops at the top of the listing`` () =
    let probe, _, list = wired ()
    list.SelectedItem <- 0
    probe.Messages.Clear()

    let keys = [ Key.CursorUp; Key.PageUp; Key.Home ] |> List.map (pressReturning list)

    Assert.All(keys, fun key -> Assert.True(key.Handled, $"%A{key} should not leave the panel"))
    Assert.Empty probe.Messages

[<Fact>]
let ``the cursor stops at the bottom of the listing`` () =
    let probe, _, list = wired ()
    list.SelectedItem <- 2
    probe.Messages.Clear()

    let keys = [ Key.CursorDown; Key.PageDown; Key.End ] |> List.map (pressReturning list)

    Assert.All(keys, fun key -> Assert.True(key.Handled, $"%A{key} should not leave the panel"))
    Assert.Empty probe.Messages

[<Fact>]
let ``a cursor key away from the edge is left to the list`` () =
    let probe, _, list = wired ()
    list.SelectedItem <- 1
    probe.Messages.Clear()

    let key = pressReturning list Key.CursorDown

    Assert.False key.Handled

[<Fact>]
let ``the right arrow jumps to the end of the listing instead of changing panel`` () =
    let probe, view, list = wired ()
    view.Layout(Size(40, 12)) |> ignore
    list.SelectedItem <- 0
    probe.Messages.Clear()

    let key = pressReturning list Key.CursorRight

    Assert.True key.Handled
    Assert.Contains(CursorMoved(cursor Left 2), probe.Messages)

[<Fact>]
let ``a jump measures the page that is actually on screen`` () =
    let probe = probe ()
    let panels = FilePanel.createPair ()
    PanelWiring.attach probe.Host Left panels.Left
    let list = FilePanel.listView panels.Left
    list.SetSource(ObservableCollection [ for i in 1..100 -> $"entry{i}" ])
    panels.Left.Layout(Size(40, 12)) |> ignore
    list.SelectedItem <- 99
    let height = list.Viewport.Height
    probe.Messages.Clear()

    press list Key.CursorLeft

    Assert.True(list.Viewport.Y > 0, "a list scrolled to its end should have scrolled its viewport")
    Assert.Contains(CursorMoved(cursor Left (100 - height)), probe.Messages)

[<Fact>]
let ``the left arrow jumps back instead of changing panel`` () =
    let probe, view, list = wired ()
    view.Layout(Size(40, 12)) |> ignore
    list.SelectedItem <- 2
    probe.Messages.Clear()

    let key = pressReturning list Key.CursorLeft

    Assert.True key.Handled
    Assert.Contains(CursorMoved(cursor Left 0), probe.Messages)

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

    Assert.Contains(CursorMoved(cursor Right 2), probe.Messages)
