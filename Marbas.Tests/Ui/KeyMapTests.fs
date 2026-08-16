module Marbas.Tests.Ui.KeyMapTests

open Xunit
open Terminal.Gui.Input
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Ui

[<Fact>]
let ``the bar row of no modifier is the classic function key strip`` () =
    let row = KeyMap.row Modifiers.none

    Assert.Equal<(Key * string * Message) list>(
        [ Key.F3, "View", ViewRequested
          Key.F4, "Edit", EditRequested
          Key.F5, "Copy", CopyRequested
          Key.F6, "RenMov", MoveRequested
          Key.F7, "Mkdir", MakeDirectoryRequested
          Key.F8, "Delete", DeleteRequested
          Key.F10, "Quit", QuitRequested ],
        row
    )

[<Fact>]
let ``the Ctrl row is the sort commands`` () =
    let row = KeyMap.row (Modifiers.held [ Ctrl ])

    Assert.Equal<(Key * string * Message) list>(
        [ Key.F3.WithCtrl, "Name", SortRequested SortKey.ByName
          Key.F4.WithCtrl, "Ext", SortRequested SortKey.ByExtension
          Key.F5.WithCtrl, "Date", SortRequested SortKey.ByDate
          Key.F6.WithCtrl, "Size", SortRequested SortKey.BySize ],
        row
    )

[<Fact>]
let ``the Alt row is the drive choosers`` () =
    let row = KeyMap.row (Modifiers.held [ Alt ])

    Assert.Equal<(Key * string * Message) list>(
        [ Key.F1.WithAlt, "Left", DriveRequested Left
          Key.F2.WithAlt, "Right", DriveRequested Right ],
        row
    )

[<Fact>]
let ``a modifier no command asks for has an empty row`` () =
    let row = KeyMap.row (Modifiers.held [ Shift ])

    Assert.Empty row

[<Fact>]
let ``every command on a row answers to its own key`` () =
    let rows =
        [ Modifiers.none; Modifiers.held [ Ctrl ]; Modifiers.held [ Alt ] ]
        |> List.collect KeyMap.row

    for key, _, message in rows do
        Assert.Equal(Some message, KeyMap.messageFor key)

[<Fact>]
let ``no key stands for two commands`` () =
    let keys = KeyMap.bindings |> List.map _.Key

    Assert.Equal<Key list>(List.distinct keys, keys)

[<Fact>]
let ``the refresh key requests a refresh`` () =
    let message = KeyMap.messageFor KeyMap.refreshKey

    Assert.Equal(Some RefreshRequested, message)

[<Fact>]
let ``every selection key toggles the selection`` () =
    for key in KeyMap.selectionKeys do
        let message = KeyMap.messageFor key

        Assert.Equal(Some SelectionToggled, message)

[<Fact>]
let ``the pattern keys select and deselect`` () =
    let selecting = KeyMap.messageFor KeyMap.selectPatternKey
    let deselecting = KeyMap.messageFor KeyMap.deselectPatternKey

    Assert.Equal(Some(PatternRequested Select), selecting)
    Assert.Equal(Some(PatternRequested Deselect), deselecting)

[<Fact>]
let ``a drive key targets its own side`` () =
    let left = KeyMap.messageFor KeyMap.leftDriveKey
    let right = KeyMap.messageFor KeyMap.rightDriveKey

    Assert.Equal(Some(DriveRequested Left), left)
    Assert.Equal(Some(DriveRequested Right), right)

[<Fact>]
let ``an unmapped key produces no message`` () =
    let message = KeyMap.messageFor (Key 'z')

    Assert.Equal(None, message)
