module Marbas.Tests.Ui.Keys.KeyMapTests

open Xunit
open Terminal.Gui.Input
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Ui.Keys

let private commandsOn modifiers = KeyMap.bar modifiers |> List.map _.Label

[<Fact>]
let ``the bar names every function key, whatever is held`` () =
    let names = KeyMap.bar (Modifiers.held [ Shift ]) |> List.map _.Name

    Assert.Equal<string list>(
        [ "F1"; "F2"; "F3"; "F4"; "F5"; "F6"; "F7"; "F8"; "F9"; "F10"; "F11"; "F12" ],
        names
    )

[<Fact>]
let ``the bar of no modifier carries the classic commands`` () =
    let commands = commandsOn Modifiers.none

    Assert.Equal<string list>(
        [ ""; ""; "View"; "Edit"; "Copy"; "RenMov"; "Mkdir"; "Delete"; ""; "Quit"; ""; "Setup" ],
        commands
    )

[<Fact>]
let ``holding Ctrl puts the sort commands on the same slots`` () =
    let commands = commandsOn (Modifiers.held [ Ctrl ])

    Assert.Equal<string list>([ ""; ""; "Name"; "Ext"; "Date"; "Size"; ""; ""; ""; ""; ""; "" ], commands)

[<Fact>]
let ``holding Alt puts the drive choosers on the first two slots`` () =
    let commands = commandsOn (Modifiers.held [ Alt ])

    Assert.Equal<string list>([ "Left"; "Right"; ""; ""; ""; ""; ""; ""; ""; ""; ""; "" ], commands)

[<Fact>]
let ``holding Shift puts the permanent delete on the delete slot`` () =
    let commands = commandsOn (Modifiers.held [ Shift ])

    Assert.Equal<string list>([ "Pack"; "Unpack"; ""; "NewFile"; ""; ""; ""; "Erase"; ""; ""; ""; "" ], commands)

[<Fact>]
let ``a slot carries the key the modifiers held make of its function key`` () =
    let keys = KeyMap.bar (Modifiers.held [ Ctrl ]) |> List.map _.Key

    Assert.Equal<Key list>(KeyMap.functionKeys |> List.map _.WithCtrl, keys)

[<Fact>]
let ``a slot carries what its own key stands for`` () =
    for modifiers in [ Modifiers.none; Modifiers.held [ Ctrl ]; Modifiers.held [ Alt ]; Modifiers.held [ Shift ] ] do
        for slot in KeyMap.bar modifiers do
            Assert.Equal(KeyMap.messageFor slot.Key, slot.Message)

[<Fact>]
let ``a slot with no command carries no message`` () =
    let unlabelled = KeyMap.bar Modifiers.none |> List.filter (fun slot -> slot.Label = "")

    Assert.NotEmpty unlabelled
    Assert.All(unlabelled, fun slot -> Assert.Equal(None, slot.Message))

[<Fact>]
let ``no key stands for two commands`` () =
    let keys = KeyMap.bindings |> List.map _.Key

    Assert.Equal<Key list>(List.distinct keys, keys)

[<Fact>]
let ``the refresh key requests a refresh`` () =
    let message = KeyMap.messageFor KeyMap.refreshKey

    Assert.Equal(Some RefreshRequested, message)

[<Fact>]
let ``Alt+W asks for the walk Enter no longer always makes`` () =
    Assert.Equal(Key.W.WithAlt, KeyMap.walkIntoKey)
    Assert.Equal(Some WalkIntoRequested, KeyMap.messageFor KeyMap.walkIntoKey)

[<Fact>]
let ``Ctrl+PgDn asks for the same walk, where a terminal delivers it`` () =
    Assert.Equal(Key.PageDown.WithCtrl, KeyMap.classicWalkIntoKey)
    Assert.Equal(Some WalkIntoRequested, KeyMap.messageFor KeyMap.classicWalkIntoKey)

[<Fact>]
let ``Alt+R asks which program should open the entry`` () =
    Assert.Equal(Key.R.WithAlt, KeyMap.openWithKey)
    Assert.Equal(Some OpenWithRequested, KeyMap.messageFor KeyMap.openWithKey)

[<Fact>]
let ``Shift+Enter asks the same, where a terminal delivers it`` () =
    Assert.Equal(Key.Enter.WithShift, KeyMap.classicOpenWithKey)
    Assert.Equal(Some OpenWithRequested, KeyMap.messageFor KeyMap.classicOpenWithKey)

[<Fact>]
let ``Alt+Shift+R runs the entry with administrator rights`` () =
    Assert.Equal(Key.R.WithAlt.WithShift, KeyMap.elevatedRunKey)
    Assert.Equal(Some ElevatedRunRequested, KeyMap.messageFor KeyMap.elevatedRunKey)

[<Fact>]
let ``Ctrl+Alt+Enter raises the same rights, where a terminal delivers it`` () =
    Assert.Equal(Key.Enter.WithCtrl.WithAlt, KeyMap.classicElevatedRunKey)
    Assert.Equal(Some ElevatedRunRequested, KeyMap.messageFor KeyMap.classicElevatedRunKey)

[<Fact>]
let ``the elevated run is the open-with key with one more modifier`` () =
    Assert.Equal(KeyMap.openWithKey.WithShift, KeyMap.elevatedRunKey)

[<Fact>]
let ``every selection key toggles the selection and moves on down`` () =
    for key in KeyMap.selectionKeys do
        let message = KeyMap.messageFor key

        Assert.Equal(Some(SelectionToggled Downward), message)

[<Fact>]
let ``shift and an arrow toggle the selection in the direction of the arrow`` () =
    let down = KeyMap.messageFor KeyMap.selectionDownKey
    let up = KeyMap.messageFor KeyMap.selectionUpKey

    Assert.Equal(Some(SelectionToggled Downward), down)
    Assert.Equal(Some(SelectionToggled Upward), up)

[<Fact>]
let ``the selection arrows are the plain arrows with shift held`` () =
    Assert.Equal(Key.CursorDown.WithShift, KeyMap.selectionDownKey)
    Assert.Equal(Key.CursorUp.WithShift, KeyMap.selectionUpKey)

[<Fact>]
let ``both page keys of a direction select a page that way`` () =
    let down = [ Key.CursorRight.WithShift; Key.PageDown.WithShift ]
    let up = [ Key.CursorLeft.WithShift; Key.PageUp.WithShift ]

    for key in down do
        Assert.Equal(Some Downward, KeyMap.selectionPageDirection key)

    for key in up do
        Assert.Equal(Some Upward, KeyMap.selectionPageDirection key)

[<Fact>]
let ``a page key without shift selects nothing`` () =
    for key in [ Key.CursorRight; Key.CursorLeft; Key.PageDown; Key.PageUp ] do
        Assert.Equal(None, KeyMap.selectionPageDirection key)

[<Fact>]
let ``the pattern keys select and deselect`` () =
    let selecting = KeyMap.messageFor KeyMap.selectPatternKey
    let deselecting = KeyMap.messageFor KeyMap.deselectPatternKey

    Assert.Equal(Some(PatternRequested Select), selecting)
    Assert.Equal(Some(PatternRequested Deselect), deselecting)

[<Fact>]
let ``a drive key targets its own side`` () =
    let left = KeyMap.messageFor KeyMap.leftRootKey
    let right = KeyMap.messageFor KeyMap.rightRootKey

    Assert.Equal(Some(RootRequested Left), left)
    Assert.Equal(Some(RootRequested Right), right)

[<Fact>]
let ``every hidden key toggles the hidden entries`` () =
    for key in KeyMap.hiddenKeys do
        let message = KeyMap.messageFor key

        Assert.Equal(Some HiddenToggled, message)

[<Fact>]
let ``both hidden keys are off the bar, because neither is a function key`` () =
    let labelled =
        KeyMap.bindings
        |> List.filter (fun binding -> binding.Message = HiddenToggled && binding.Label.IsSome)

    Assert.Empty labelled

[<Fact>]
let ``only a function key is labelled, because the bar holds nothing else`` () =
    let labelled =
        KeyMap.bindings
        |> List.filter (fun binding -> binding.Label.IsSome)
        |> List.map _.Key

    let isFunctionKey (key: Key) =
        KeyMap.functionKeys
        |> List.exists (fun functionKey -> Modifiers.apply (Modifiers.ofKey key) functionKey = key)

    Assert.NotEmpty labelled
    Assert.All(labelled, fun key -> Assert.True(isFunctionKey key, string key))

[<Fact>]
let ``the delete keys tell the trash from the permanent delete`` () =
    Assert.Equal(Some(DeleteRequested ToTrash), KeyMap.messageFor Key.F8)
    Assert.Equal(Some(DeleteRequested ToTrash), KeyMap.messageFor KeyMap.trashKey)
    Assert.Equal(Some(DeleteRequested Permanently), KeyMap.messageFor KeyMap.permanentDeleteKey)
    Assert.Equal(Some(DeleteRequested Permanently), KeyMap.messageFor Key.F8.WithShift)

[<Fact>]
let ``the permanent delete keys are the delete keys with shift held`` () =
    Assert.Equal(Key.Delete, KeyMap.trashKey)
    Assert.Equal(KeyMap.trashKey.WithShift, KeyMap.permanentDeleteKey)

[<Fact>]
let ``the quit key requests a quit, so quitting always passes through the state`` () =
    let message = KeyMap.messageFor KeyMap.quitKey

    Assert.Equal(Some QuitRequested, message)

[<Fact>]
let ``an unmapped key produces no message`` () =
    let message = KeyMap.messageFor (Key 'z')

    Assert.Equal(None, message)

[<Fact>]
let ``the pack key is F1 with shift and asks for a pack`` () =
    Assert.Equal(Key.F1.WithShift, KeyMap.packKey)
    Assert.Equal(Some PackRequested, KeyMap.messageFor KeyMap.packKey)

[<Fact>]
let ``packing takes the first slot of the shift row`` () =
    let slot = KeyMap.bar (Set.singleton Shift) |> List.head

    Assert.Equal("F1", slot.Name)
    Assert.Equal("Pack", slot.Label)
    Assert.Equal(Some PackRequested, slot.Message)

[<Fact>]
let ``the unpack key is F2 with shift and asks for an unpack`` () =
    Assert.Equal(Key.F2.WithShift, KeyMap.unpackKey)
    Assert.Equal(Some UnpackRequested, KeyMap.messageFor KeyMap.unpackKey)

[<Fact>]
let ``unpacking takes the second slot of the shift row`` () =
    let slot = KeyMap.bar (Set.singleton Shift) |> List.item 1

    Assert.Equal("F2", slot.Name)
    Assert.Equal("Unpack", slot.Label)
    Assert.Equal(Some UnpackRequested, slot.Message)
