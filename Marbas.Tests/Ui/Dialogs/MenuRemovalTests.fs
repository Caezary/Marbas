module Marbas.Tests.Ui.Dialogs.MenuRemovalTests

open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Profile
open Marbas.Ui.Dialogs
open Marbas.Ui.Dialogs.MenuEntryForm
open Marbas.Ui.Dialogs.MenuRemoval
open Marbas.Tests

let private key raw = Expect.some (MenuHotkey.tryCreate raw)

let private entry hotkey label =
    { Hotkey = key hotkey
      Label = Expect.some (MenuLabel.tryCreate label)
      Line = Expect.some (CommandLine.tryCreate "run %p")
      Run = Directly
      Elevation = AsUser }

let private menuOf items =
    List.foldBack (fun item menu -> Expect.ok (UserMenu.apply (AddItem(item, None)) menu)) items UserMenu.empty

let private sample =
    menuOf
        [ CommandItem(entry "E" "Edit")
          SeparatorItem
          CommandItem(entry "C" "Compare")
          SeparatorItem
          CommandItem(entry "Z" "Pack") ]

[<Fact>]
let ``an entry with no line beside it offers only itself`` () =
    let menu =
        menuOf [ CommandItem(entry "E" "Edit"); CommandItem(entry "C" "Compare") ]

    let offered = MenuRemoval.offered 1 menu

    Assert.Equal<RemovalChoice list>([ RemoveCommand ], offered)

[<Fact>]
let ``an entry between two lines offers itself and either line`` () =
    let offered = MenuRemoval.offered 2 sample

    Assert.Equal<RemovalChoice list>([ RemoveCommand; RemoveLine AboveCursor; RemoveLine BelowCursor ], offered)

[<Fact>]
let ``the first entry offers the line below it`` () =
    let offered = MenuRemoval.offered 0 sample

    Assert.Equal<RemovalChoice list>([ RemoveCommand; RemoveLine BelowCursor ], offered)

[<Fact>]
let ``a row that is not an entry offers nothing`` () =
    let offered = MenuRemoval.offered 1 sample

    Assert.Empty offered

[<Fact>]
let ``removing the entry removes it by its hotkey`` () =
    let change = MenuRemoval.changeFor 2 sample RemoveCommand

    Assert.Equal(Some(RemoveItem(CommandAt(key "C"))), change)

[<Fact>]
let ``removing the line above names the separator below the entry before it`` () =
    let change = MenuRemoval.changeFor 2 sample (RemoveLine AboveCursor)

    Assert.Equal(Some(RemoveItem(SeparatorAfter(Some(key "E"), 0))), change)

[<Fact>]
let ``removing the line below names the separator right under the entry`` () =
    let change = MenuRemoval.changeFor 2 sample (RemoveLine BelowCursor)

    Assert.Equal(Some(RemoveItem(SeparatorAfter(Some(key "C"), 0))), change)

[<Fact>]
let ``with only the entry to remove the question asks about the entry alone`` () =
    let question =
        MenuRemoval.question
            { Command = entry "C" "Compare"
              Offered = [ RemoveCommand ] }

    Assert.Equal("Remove 'Compare' from the menu?", question)

[<Fact>]
let ``with a line beside the entry the question offers the line as well`` () =
    let prompt =
        { Command = entry "C" "Compare"
          Offered = [ RemoveCommand; RemoveLine BelowCursor ] }

    let question = MenuRemoval.question prompt

    Assert.Equal("Remove 'Compare' from the menu, or a line next to it?", question)
