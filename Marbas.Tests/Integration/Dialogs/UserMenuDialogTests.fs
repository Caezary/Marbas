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

let private laidOut menu =
    DialogStyle.apply ()
    let dialog = UserMenuDialog.create menu
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

let private pressOn (dialog: UserMenuDialog.UserMenuView) key = press (UserMenuDialog.entries dialog) key

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
