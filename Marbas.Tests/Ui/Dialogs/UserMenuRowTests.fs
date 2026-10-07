module Marbas.Tests.Ui.Dialogs.UserMenuRowTests

open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.Dialogs
open Marbas.Tests

let private edit =
    { Hotkey = Expect.some (MenuHotkey.tryCreate "e")
      Label = Expect.some (MenuLabel.tryCreate "Edit in vim")
      Line = Expect.some (CommandLine.tryCreate "vim %p")
      Run = Directly
      Elevation = AsUser }

[<Fact>]
let ``an entry is shown as its hotkey and its label`` () =
    let row = UserMenuRow.describe 30 (CommandItem edit)

    Assert.Equal("E  Edit in vim", row)

[<Fact>]
let ``a separator is a line across the whole row`` () =
    let row = UserMenuRow.describe 6 SeparatorItem

    Assert.Equal("──────", row)

[<Fact>]
let ``a separator in a row with no room yet is empty`` () =
    let row = UserMenuRow.describe 0 SeparatorItem

    Assert.Equal("", row)
