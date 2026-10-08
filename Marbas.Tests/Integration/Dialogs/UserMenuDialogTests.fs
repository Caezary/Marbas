module Marbas.Tests.Integration.Dialogs.UserMenuDialogTests

open System
open System.Drawing
open Xunit
open Terminal.Gui.Input
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.Keys
open Marbas.Ui.Theme
open Marbas.Ui.Dialogs
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private entry hotkey label line =
    { Hotkey = Expect.some (MenuHotkey.tryCreate hotkey)
      Label = Expect.some (MenuLabel.tryCreate label)
      Line = Expect.some (CommandLine.tryCreate line)
      Run = Directly
      Elevation = AsUser }

let private edit = entry "E" "Edit" "vim %p"

let private compare = entry "C" "Compare" "code --diff %Lp %Rp"

let private pack = entry "Z" "Pack" "7z a %d.7z %s"

let private menuOf items =
    List.foldBack (fun item menu -> Expect.ok (UserMenu.apply (AddItem(item, None)) menu)) items UserMenu.empty

let private sample =
    menuOf [ CommandItem edit; CommandItem compare; SeparatorItem; CommandItem pack ]

let private untouched: UserMenuDialog.MenuEditing =
    { AskEntry = fun _ -> None
      AskRemoval = fun _ -> None
      Change = fun _ -> None }

let private laidOutEditing editing menu =
    DialogStyle.apply ()
    let dialog = UserMenuDialog.create editing menu
    dialog.BeginInit()
    dialog.EndInit()
    dialog.Layout(Size(100, 30)) |> ignore
    (UserMenuDialog.entries dialog).SetFocus() |> ignore
    dialog

let private laidOut menu =
    DialogStyle.apply ()
    let dialog = UserMenuDialog.create untouched menu
    dialog.BeginInit()
    dialog.EndInit()
    dialog.Layout(Size(100, 30)) |> ignore
    (UserMenuDialog.entries dialog).SetFocus() |> ignore
    dialog

let private shown (dialog: UserMenuDialog.UserMenuView) =
    let list = UserMenuDialog.entries dialog
    let source = list.Source.ToList()
    [ for index in 0 .. list.Source.Count - 1 -> source[index].ToString() ]

let private standOn index (dialog: UserMenuDialog.UserMenuView) =
    (UserMenuDialog.entries dialog).SelectedItem <- Nullable index

let private cursor (dialog: UserMenuDialog.UserMenuView) =
    (UserMenuDialog.entries dialog).SelectedItem |> Option.ofNullable

let private pressOn (dialog: UserMenuDialog.UserMenuView) key =
    press (UserMenuDialog.entries dialog) key

[<Fact>]
let ``each entry is listed as its hotkey and its label, a separator as a line`` () =
    use dialog = laidOut sample

    let rows = shown dialog

    Assert.Equal<string list>([ "E  Edit"; "C  Compare" ], List.take 2 rows)
    Assert.Matches("^─+$", rows[2])
    Assert.Equal("Z  Pack", rows[3])

[<Fact>]
let ``the menu opens on its first entry`` () =
    use dialog = laidOut sample

    let opened = cursor dialog

    Assert.Equal(Some 0, opened)

[<Fact>]
let ``a menu that starts with a separator opens on the entry below it`` () =
    use dialog = laidOut (menuOf [ SeparatorItem; CommandItem edit ])

    let opened = cursor dialog

    Assert.Equal(Some 1, opened)

[<Theory>]
[<InlineData('c')>]
[<InlineData('C')>]
let ``a letter runs its entry at once and closes the menu`` (letter: char) =
    use dialog = laidOut sample

    pressOn dialog (Key letter)

    Assert.Equal(Some compare, UserMenuDialog.chosen dialog)

[<Fact>]
let ``Enter runs the entry under the cursor, the same one its letter runs`` () =
    use dialog = laidOut sample
    standOn 1 dialog

    pressOn dialog KeyMap.userMenuRunKey

    Assert.Equal(Some compare, UserMenuDialog.chosen dialog)

[<Fact>]
let ``a letter no entry runs on chooses nothing`` () =
    use dialog = laidOut sample

    pressOn dialog (Key 'q')

    Assert.Equal(None, UserMenuDialog.chosen dialog)

[<Fact>]
let ``down steps over a separator`` () =
    use dialog = laidOut sample
    standOn 1 dialog

    pressOn dialog Key.CursorDown

    Assert.Equal(Some 3, cursor dialog)

[<Fact>]
let ``up steps over a separator`` () =
    use dialog = laidOut sample
    standOn 3 dialog

    pressOn dialog Key.CursorUp

    Assert.Equal(Some 1, cursor dialog)

[<Fact>]
let ``down on the last entry stays on it`` () =
    use dialog = laidOut sample
    standOn 3 dialog

    pressOn dialog Key.CursorDown

    Assert.Equal(Some 3, cursor dialog)

[<Fact>]
let ``Enter in an empty menu chooses nothing`` () =
    use dialog = laidOut UserMenu.empty

    pressOn dialog KeyMap.userMenuRunKey

    Assert.Equal(None, UserMenuDialog.chosen dialog)

let private key raw = Expect.some (MenuHotkey.tryCreate raw)

let private extra = entry "X" "Extra" "notepad %p"

type private Recorded =
    { Changes: ResizeArray<MenuChange>
      Entries: ResizeArray<MenuEntryForm.EntryPrompt>
      Removals: ResizeArray<MenuRemoval.RemovalPrompt> }

let private recorded () =
    { Changes = ResizeArray()
      Entries = ResizeArray()
      Removals = ResizeArray() }

let private editing (record: Recorded) entryAnswer removalAnswer start : UserMenuDialog.MenuEditing =
    let mutable menu = start

    { AskEntry =
        fun prompt ->
            record.Entries.Add prompt
            entryAnswer
      AskRemoval =
        fun prompt ->
            record.Removals.Add prompt
            removalAnswer
      Change =
        fun change ->
            record.Changes.Add change
            menu <- Expect.ok (UserMenu.apply change menu)
            Some menu }

let private editedWith entryAnswer removalAnswer menu =
    let record = recorded ()
    let dialog = laidOutEditing (editing record entryAnswer removalAnswer menu) menu
    record, dialog

[<Fact>]
let ``Ins adds the entry typed in the dialog after the one under the cursor, and stands on it`` () =
    let record, dialog =
        editedWith (Some(MenuEntryForm.CommandEntered extra)) None sample

    use dialog = dialog
    standOn 1 dialog

    pressOn dialog KeyMap.userMenuAddKey

    Assert.Equal<MenuChange list>([ AddItem(CommandItem extra, Some(CommandAt(key "C"))) ], List.ofSeq record.Changes)
    Assert.Equal("X  Extra", (shown dialog)[2])
    Assert.Equal(Some 2, cursor dialog)

[<Fact>]
let ``Ins asks for a new entry, naming every entry already in the menu`` () =
    let record, dialog = editedWith None None sample
    use dialog = dialog

    pressOn dialog KeyMap.userMenuAddKey

    let prompt = Assert.Single record.Entries
    Assert.Equal(MenuEntryForm.Adding, prompt.Editing)
    Assert.Equal<MenuCommand list>([ edit; compare; pack ], prompt.Others)

[<Fact>]
let ``Ins in an empty menu adds the first entry`` () =
    let record, dialog =
        editedWith (Some(MenuEntryForm.CommandEntered extra)) None UserMenu.empty

    use dialog = dialog

    pressOn dialog KeyMap.userMenuAddKey

    Assert.Equal<MenuChange list>([ AddItem(CommandItem extra, None) ], List.ofSeq record.Changes)
    Assert.Equal<string list>([ "X  Extra" ], shown dialog)

[<Fact>]
let ``a line chosen below goes under the entry, the cursor staying on it`` () =
    let record, dialog =
        editedWith (Some(MenuEntryForm.LineEntered MenuEntryForm.BelowCursor)) None sample

    use dialog = dialog

    pressOn dialog KeyMap.userMenuAddKey

    Assert.Equal<MenuChange list>([ AddItem(SeparatorItem, Some(CommandAt(key "E"))) ], List.ofSeq record.Changes)
    Assert.Matches("^─+$", (shown dialog)[1])
    Assert.Equal(Some 0, cursor dialog)

[<Fact>]
let ``a line chosen above goes over the entry, the cursor staying on it`` () =
    let record, dialog =
        editedWith (Some(MenuEntryForm.LineEntered MenuEntryForm.AboveCursor)) None sample

    use dialog = dialog
    standOn 1 dialog

    pressOn dialog KeyMap.userMenuAddKey

    Assert.Equal<MenuChange list>([ AddItem(SeparatorItem, Some(CommandAt(key "E"))) ], List.ofSeq record.Changes)
    Assert.Equal(Some 2, cursor dialog)

[<Fact>]
let ``F4 changes the entry under the cursor through the same dialog`` () =
    let changed = entry "K" "Compare all" "code --diff %Ls %Rs"

    let record, dialog =
        editedWith (Some(MenuEntryForm.CommandEntered changed)) None sample

    use dialog = dialog
    standOn 1 dialog

    pressOn dialog KeyMap.userMenuChangeKey

    let prompt = Assert.Single record.Entries
    Assert.Equal(MenuEntryForm.Changing compare, prompt.Editing)
    Assert.Equal<MenuCommand list>([ edit; pack ], prompt.Others)
    Assert.Equal<MenuChange list>([ ChangeCommand(key "C", changed) ], List.ofSeq record.Changes)
    Assert.Equal("K  Compare all", (shown dialog)[1])

[<Fact>]
let ``a dialog closed without an answer changes nothing`` () =
    let record, dialog = editedWith None None sample
    use dialog = dialog

    pressOn dialog KeyMap.userMenuChangeKey

    Assert.Empty record.Changes

[<Fact>]
let ``Del asks what to remove, offering the line beside the entry`` () =
    let record, dialog = editedWith None None sample
    use dialog = dialog
    standOn 1 dialog

    pressOn dialog KeyMap.userMenuRemoveKey

    let prompt = Assert.Single record.Removals
    Assert.Equal(compare, prompt.Command)

    Assert.Equal<MenuRemoval.RemovalChoice list>(
        [ MenuRemoval.RemoveCommand; MenuRemoval.RemoveLine MenuEntryForm.BelowCursor ],
        prompt.Offered
    )

[<Fact>]
let ``Del removes the entry once the answer says so, the cursor moving to the next entry`` () =
    let record, dialog = editedWith None (Some MenuRemoval.RemoveCommand) sample
    use dialog = dialog
    standOn 1 dialog

    pressOn dialog KeyMap.userMenuRemoveKey

    Assert.Equal<MenuChange list>([ RemoveItem(CommandAt(key "C")) ], List.ofSeq record.Changes)
    Assert.Equal(Some 2, cursor dialog)
    Assert.Equal("Z  Pack", (shown dialog)[2])

[<Fact>]
let ``Del removes the line below the entry when that is the answer`` () =
    let record, dialog =
        editedWith None (Some(MenuRemoval.RemoveLine MenuEntryForm.BelowCursor)) sample

    use dialog = dialog
    standOn 1 dialog

    pressOn dialog KeyMap.userMenuRemoveKey

    Assert.Equal<MenuChange list>([ RemoveItem(SeparatorAfter(Some(key "C"), 0)) ], List.ofSeq record.Changes)
    Assert.Equal<string list>([ "E  Edit"; "C  Compare"; "Z  Pack" ], shown dialog)
    Assert.Equal(Some 1, cursor dialog)

[<Fact>]
let ``a removal declined changes nothing`` () =
    let record, dialog = editedWith None None sample
    use dialog = dialog

    pressOn dialog KeyMap.userMenuRemoveKey

    Assert.Empty record.Changes

[<Fact>]
let ``Ctrl+Down moves the entry down, the cursor going with it`` () =
    let record, dialog = editedWith None None sample
    use dialog = dialog

    pressOn dialog KeyMap.userMenuLaterKey

    Assert.Equal<MenuChange list>([ MoveItem(CommandAt(key "E"), Later) ], List.ofSeq record.Changes)
    Assert.Equal<string list>([ "C  Compare"; "E  Edit" ], List.take 2 (shown dialog))
    Assert.Equal(Some 1, cursor dialog)

[<Fact>]
let ``Ctrl+Up past a line takes the entry to the other side of it`` () =
    let record, dialog = editedWith None None sample
    use dialog = dialog
    standOn 3 dialog

    pressOn dialog KeyMap.userMenuEarlierKey

    Assert.Equal<MenuChange list>([ MoveItem(CommandAt(key "Z"), Earlier) ], List.ofSeq record.Changes)
    Assert.Equal("Z  Pack", (shown dialog)[2])
    Assert.Equal(Some 2, cursor dialog)

[<Fact>]
let ``a change the file refused leaves the popup as it was`` () =
    let record = recorded ()

    let refusing =
        { editing record (Some(MenuEntryForm.CommandEntered extra)) None sample with
            Change =
                fun change ->
                    record.Changes.Add change
                    None }

    use dialog = laidOutEditing refusing sample

    pressOn dialog KeyMap.userMenuAddKey

    Assert.Single record.Changes |> ignore
    Assert.Equal(4, (shown dialog).Length)
