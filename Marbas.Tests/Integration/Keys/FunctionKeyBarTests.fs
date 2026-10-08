module Marbas.Tests.Integration.Keys.FunctionKeyBarTests

open System.Drawing
open Xunit
open Terminal.Gui.App
open Terminal.Gui.Input
open Marbas.State.Update
open Marbas.Ui.Keys
open Marbas.Ui.App
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private bar window = panel FunctionKeyBar.Id window

let private click (slot: FunctionKeyBar.SlotView) =
    slot.NewMouseEvent(Mouse(Flags = MouseFlags.LeftButtonClicked)) |> ignore

[<Fact>]
let ``main window contains the function key bar`` () =
    use window = createInitialized ()

    Assert.NotNull(bar window)

[<Fact>]
let ``the bar has a slot for every function key`` () =
    use window = createInitialized ()

    let names = FunctionKeyBar.slots (bar window) |> List.map _.KeyName.Text

    Assert.Equal<string list>([ "F1"; "F2"; "F3"; "F4"; "F5"; "F6"; "F7"; "F8"; "F9"; "F10"; "F11"; "F12" ], names)

[<Fact>]
let ``the classic commands sit on the slots of their keys`` () =
    use window = createInitialized ()

    let commands = FunctionKeyBar.slots (bar window) |> List.map _.CommandName.Text

    Assert.Equal<string list>(
        [ ""
          "Menu"
          "View"
          "Edit"
          "Copy"
          "RenMov"
          "Mkdir"
          "Delete"
          ""
          "Quit"
          "Tabs"
          "Setup" ],
        commands
    )

[<Theory>]
[<InlineData(80, 24)>]
[<InlineData(120, 30)>]
let ``bar sticks to the bottom edge`` (width: int) (height: int) =
    use window = createInitialized ()

    window.Layout(Size(width, height)) |> ignore
    let content = window.Viewport
    let frame = (bar window).Frame

    Assert.Equal(content.Height - 1, frame.Y)
    Assert.Equal(0, frame.X)
    Assert.Equal(content.Width, frame.Width)
    Assert.Equal(1, frame.Height)

[<Theory>]
[<InlineData(80)>]
[<InlineData(97)>]
[<InlineData(120)>]
let ``the slots share the whole width of the bar between them`` (width: int) =
    use window = createInitialized ()

    window.Layout(Size(width, 24)) |> ignore
    let frames = FunctionKeyBar.slots (bar window) |> List.map _.Frame

    Assert.Equal(0, frames.Head.X)
    Assert.Equal(width, frames |> List.sumBy _.Width)

    for previous, next in List.pairwise frames do
        Assert.Equal(previous.X + previous.Width, next.X)

[<Theory>]
[<InlineData(80)>]
[<InlineData(97)>]
[<InlineData(120)>]
let ``no slot is more than a column wider than another`` (width: int) =
    use window = createInitialized ()

    window.Layout(Size(width, 24)) |> ignore
    let widths = FunctionKeyBar.slots (bar window) |> List.map _.Frame.Width

    Assert.True(List.max widths - List.min widths <= 1, string widths)

[<Fact>]
let ``a command follows the key name inside the slot`` () =
    use window = createInitialized ()

    window.Layout(Size(120, 24)) |> ignore
    let copy = FunctionKeyBar.slots (bar window) |> List.item 4

    Assert.Equal(0, copy.KeyName.Frame.X)
    Assert.Equal(copy.KeyName.Frame.Width, copy.CommandName.Frame.X)

[<Fact>]
let ``the bar shows the commands of the modifiers it is asked for`` () =
    use bar = FunctionKeyBar.create ignore

    FunctionKeyBar.show bar (Modifiers.held [ Ctrl ])

    let shown =
        FunctionKeyBar.slots bar
        |> List.map (fun slot -> slot.KeyName.Text, slot.CommandName.Text)

    Assert.Equal<(string * string) list>(
        [ "F1", ""
          "F2", ""
          "F3", "Name"
          "F4", "Ext"
          "F5", "Date"
          "F6", "Size"
          "F7", ""
          "F8", ""
          "F9", ""
          "F10", ""
          "F11", ""
          "F12", "" ],
        shown
    )

[<Fact>]
let ``clicking a slot dispatches the command standing on it`` () =
    let messages = ResizeArray<Message>()
    use bar = FunctionKeyBar.create messages.Add

    click (FunctionKeyBar.slots bar |> List.item 4)

    Assert.Contains(CopyRequested, messages)

[<Fact>]
let ``clicking a slot dispatches the command the modifiers put there`` () =
    let messages = ResizeArray<Message>()
    use bar = FunctionKeyBar.create messages.Add
    FunctionKeyBar.show bar (Modifiers.held [ Ctrl ])

    click (FunctionKeyBar.slots bar |> List.item 4)

    Assert.Contains(SortRequested Marbas.Domain.SortKey.ByDate, messages)

[<Fact>]
let ``clicking a slot with no command dispatches nothing`` () =
    let messages = ResizeArray<Message>()
    use bar = FunctionKeyBar.create messages.Add

    click (FunctionKeyBar.slots bar |> List.head)

    Assert.Empty messages

[<Fact>]
let ``the bar takes no key, so a function key is handled where the focus is`` () =
    let messages = ResizeArray<Message>()
    use bar = FunctionKeyBar.create messages.Add

    let handled = bar.NewKeyDownEvent(Key Key.F5.KeyCode)

    Assert.False handled
    Assert.Empty messages

[<Fact>]
let ``F10 key triggers the quit action`` () =
    let mutable quitRequested = false
    use window = createInitializedWith (fun () -> quitRequested <- true)

    pressKey window Key.F10

    Assert.True quitRequested

[<Fact>]
let ``F10 is also bound to quit`` () =
    MainWindow.configureQuitKey ()

    let keys = Application.GetDefaultKeys Command.Quit

    Assert.Contains(Key.F10, keys)
    Assert.Contains(Key.Q.WithCtrl, keys)
