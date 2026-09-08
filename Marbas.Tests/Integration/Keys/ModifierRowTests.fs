module Marbas.Tests.Integration.Keys.ModifierRowTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Marbas.Ui.Keys
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start")
    fs

let private watchedWindow () =
    let watchers = ResizeArray<Set<Modifier> -> unit>()

    let window =
        focusedWindow
            { Fakes.dependencies (mockFs ()) (at "start") with
                WatchModifiers = watchers.Add }

    let hold modifiers =
        for watcher in watchers do
            watcher modifiers

    window, hold

let private slots window = FunctionKeyBar.slots (panel FunctionKeyBar.Id window)

let private commands window = slots window |> List.map _.CommandName.Text

let private names window = slots window |> List.map _.KeyName.Text

let private functionKeys =
    [ "F1"; "F2"; "F3"; "F4"; "F5"; "F6"; "F7"; "F8"; "F9"; "F10"; "F11"; "F12" ]

let private classic =
    [ ""; ""; "View"; "Edit"; "Copy"; "RenMov"; "Mkdir"; "Delete"; ""; "Quit"; "Tabs"; "Setup" ]

[<Fact>]
let ``the bar starts on the classic function keys`` () =
    let window, _ = watchedWindow ()
    use window = window

    Assert.Equal<string list>(classic, commands window)

[<Fact>]
let ``holding Ctrl turns the bar into the sort commands`` () =
    let window, hold = watchedWindow ()
    use window = window

    hold (Modifiers.held [ Ctrl ])

    Assert.Equal<string list>([ ""; ""; "Name"; "Ext"; "Date"; "Size"; ""; ""; ""; ""; ""; "" ], commands window)

[<Fact>]
let ``letting Ctrl go brings the classic keys back`` () =
    let window, hold = watchedWindow ()
    use window = window
    hold (Modifiers.held [ Ctrl ])

    hold Modifiers.none

    Assert.Equal<string list>(classic, commands window)

[<Fact>]
let ``holding Alt shows the drive choosers`` () =
    let window, hold = watchedWindow ()
    use window = window

    hold (Modifiers.held [ Alt ])

    Assert.Equal<string list>([ "Left"; "Right"; ""; ""; ""; ""; ""; ""; ""; ""; "NewTab"; "" ], commands window)

[<Fact>]
let ``holding Shift shows the permanent delete on the delete slot`` () =
    let window, hold = watchedWindow ()
    use window = window

    hold (Modifiers.held [ Shift ])

    Assert.Equal<string list>([ "Pack"; "Unpack"; ""; "NewFile"; ""; ""; ""; "Erase"; "Save"; ""; ""; "" ], commands window)

[<Fact>]
let ``a key keeps its name whatever modifier is held`` () =
    let window, hold = watchedWindow ()
    use window = window

    for modifiers in [ Modifiers.held [ Ctrl ]; Modifiers.held [ Alt ]; Modifiers.held [ Shift ]; Modifiers.none ] do
        hold modifiers

        Assert.Equal<string list>(functionKeys, names window)

[<Fact>]
let ``the slots on show carry the commands of the modifiers held`` () =
    let window, hold = watchedWindow ()
    use window = window

    hold (Modifiers.held [ Ctrl ])

    let carried = slots window |> List.map _.Message

    Assert.Equal<Marbas.State.Update.Message option list>(
        KeyMap.bar (Modifiers.held [ Ctrl ]) |> List.map _.Message,
        carried
    )
