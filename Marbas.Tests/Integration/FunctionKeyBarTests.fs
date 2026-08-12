module Marbas.Tests.Integration.FunctionKeyBarTests

open System.Drawing
open Xunit
open Terminal.Gui.App
open Terminal.Gui.Input
open Terminal.Gui.Views
open Marbas.State.Update
open Marbas.Ui
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private bar window = panel FunctionKeyBar.Id window

[<Fact>]
let ``main window contains the function key bar`` () =
    use window = createInitialized ()

    Assert.NotNull(bar window)

[<Fact>]
let ``bar lists norton style function keys`` () =
    use window = createInitialized ()

    let shortcuts =
        (bar window).SubViews
        |> Seq.choose (function
            | :? Shortcut as s -> Some(s.Key, s.Title)
            | _ -> None)
        |> List.ofSeq

    for expected in
        [ Key.F3, "View"
          Key.F4, "Edit"
          Key.F5, "Copy"
          Key.F6, "RenMov"
          Key.F7, "Mkdir"
          Key.F8, "Delete"
          Key.F10, "Quit" ] do
        Assert.Contains(expected, shortcuts)

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

[<Fact>]
let ``every function key carries the message it stands for`` () =
    let bindings = FunctionKeyBar.bindings

    Assert.Equal<(Key * string * Message) list>(
        [ Key.F3, "View", ViewRequested
          Key.F4, "Edit", EditRequested
          Key.F5, "Copy", CopyRequested
          Key.F6, "RenMov", MoveRequested
          Key.F7, "Mkdir", MakeDirectoryRequested
          Key.F8, "Delete", DeleteRequested
          Key.F10, "Quit", QuitRequested ],
        bindings
    )

[<Fact>]
let ``pressing a function key dispatches its message`` () =
    let messages = ResizeArray<Message>()
    use bar = FunctionKeyBar.create messages.Add

    press bar Key.F5

    Assert.Contains(CopyRequested, messages)

[<Fact>]
let ``F10 key triggers the quit action`` () =
    let mutable quitRequested = false
    use window = createInitializedWith (fun () -> quitRequested <- true)

    press window Key.F10

    Assert.True quitRequested

[<Fact>]
let ``F10 is also bound to quit`` () =
    MainWindow.configureQuitKey ()

    let keys = Application.GetDefaultKeys Command.Quit

    Assert.Contains(Key.F10, keys)
    Assert.Contains(Key.Q.WithCtrl, keys)
