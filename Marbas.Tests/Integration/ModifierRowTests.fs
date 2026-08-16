module Marbas.Tests.Integration.ModifierRowTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Views
open Marbas.Ui
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start"
    fs

let private watchedWindow () =
    let watchers = ResizeArray<Set<Modifier> -> unit>()

    let window =
        focusedWindow
            { Fakes.dependencies (mockFs ()) @"C:\start" with
                WatchModifiers = watchers.Add }

    let hold modifiers =
        for watcher in watchers do
            watcher modifiers

    window, hold

let private labels window =
    (panel FunctionKeyBar.Id window).SubViews
    |> Seq.choose (function
        | :? Shortcut as shortcut -> Some shortcut.Title
        | _ -> None)
    |> List.ofSeq

let private classic =
    [ "View"; "Edit"; "Copy"; "RenMov"; "Mkdir"; "Delete"; "Quit" ]

[<Fact>]
let ``the bar starts on the classic function keys`` () =
    let window, _ = watchedWindow ()
    use window = window

    Assert.Equal<string list>(classic, labels window)

[<Fact>]
let ``holding Ctrl turns the bar into the sort commands`` () =
    let window, hold = watchedWindow ()
    use window = window

    hold (Modifiers.held [ Ctrl ])

    Assert.Equal<string list>([ "Name"; "Ext"; "Date"; "Size" ], labels window)

[<Fact>]
let ``letting Ctrl go brings the classic keys back`` () =
    let window, hold = watchedWindow ()
    use window = window
    hold (Modifiers.held [ Ctrl ])

    hold Modifiers.none

    Assert.Equal<string list>(classic, labels window)

[<Fact>]
let ``holding Alt shows the drive choosers`` () =
    let window, hold = watchedWindow ()
    use window = window

    hold (Modifiers.held [ Alt ])

    Assert.Equal<string list>([ "Left"; "Right" ], labels window)

[<Fact>]
let ``a modifier no command asks for empties the bar`` () =
    let window, hold = watchedWindow ()
    use window = window

    hold (Modifiers.held [ Shift ])

    Assert.Empty(labels window)

[<Fact>]
let ``the keys of the row on show carry their commands`` () =
    let window, hold = watchedWindow ()
    use window = window

    hold (Modifiers.held [ Ctrl ])

    let keys =
        (panel FunctionKeyBar.Id window).SubViews
        |> Seq.choose (function
            | :? Shortcut as shortcut -> Some shortcut.Key
            | _ -> None)
        |> List.ofSeq

    Assert.Equal<Terminal.Gui.Input.Key list>(KeyMap.row (Modifiers.held [ Ctrl ]) |> List.map (fun (key, _, _) -> key), keys)
