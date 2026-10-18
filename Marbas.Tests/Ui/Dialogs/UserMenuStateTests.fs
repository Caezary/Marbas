module Marbas.Tests.Ui.Dialogs.UserMenuStateTests

open Xunit
open Marbas.Domain
open Marbas.Programs
open Marbas.Profile
open Marbas.Ui.Dialogs
open Marbas.Tests

let private key raw = Expect.some (MenuHotkey.tryCreate raw)

let private entry hotkey label =
    { Hotkey = key hotkey
      Label = Expect.some (MenuLabel.tryCreate label)
      Line = Expect.some (CommandLine.tryCreate "run %p")
      Run = Directly
      Elevation = AsUser }

let private edit = entry "E" "Edit"

let private compare = entry "C" "Compare"

let private pack = entry "Z" "Pack"

let private menuOf items =
    List.foldBack (fun item menu -> Expect.ok (UserMenu.apply (AddItem(item, None)) menu)) items UserMenu.empty

let private stateOf items = UserMenuState.start (menuOf items)

let private sample =
    stateOf [ CommandItem edit; CommandItem compare; SeparatorItem; CommandItem pack ]

[<Fact>]
let ``the popup starts holding the menu it was given, with nothing chosen`` () =
    let menu = menuOf [ CommandItem edit ]

    let started = UserMenuState.start menu

    Assert.Equal(menu, started.Menu)
    Assert.Equal(None, started.Chosen)

[<Fact>]
let ``the menu the file answers with takes the place of the one held`` () =
    let shorter = menuOf [ CommandItem edit ]

    let held = UserMenuState.holding shorter sample

    Assert.Equal(shorter, held.Menu)

[<Fact>]
let ``an entry chosen is the one the popup answers with`` () =
    let chosen = UserMenuState.choosing compare sample

    Assert.Equal(Some compare, chosen.Chosen)

[<Fact>]
let ``each row is an entry or a line as wide as it is told`` () =
    let rows = UserMenuState.rows 5 sample

    Assert.Equal<string list>([ "E  Edit"; "C  Compare"; "─────"; "Z  Pack" ], rows)

[<Fact>]
let ``the commands are the entries without the lines`` () =
    let commands = UserMenuState.commands sample

    Assert.Equal<MenuCommand list>([ edit; compare; pack ], commands)

[<Theory>]
[<InlineData(0, "E")>]
[<InlineData(1, "C")>]
[<InlineData(3, "Z")>]
let ``a row holding an entry answers it`` (index: int, hotkey: string) =
    let command = UserMenuState.commandAt index sample

    Assert.Equal(Some(key hotkey), command |> Option.map _.Hotkey)

[<Theory>]
[<InlineData(-1)>]
[<InlineData(2)>]
[<InlineData(4)>]
let ``a line or a row outside the menu holds no entry`` (index: int) =
    let command = UserMenuState.commandAt index sample

    Assert.Equal(None, command)

[<Fact>]
let ``a hotkey answers the entry that holds it`` () =
    let command = UserMenuState.commandOn (key "C") sample

    Assert.Equal(Some compare, command)

[<Fact>]
let ``a hotkey no entry holds answers nothing`` () =
    let command = UserMenuState.commandOn (key "Q") sample

    Assert.Equal(None, command)

[<Fact>]
let ``the row of an entry counts the lines above it`` () =
    let row = UserMenuState.positionOf (key "Z") sample

    Assert.Equal(Some 3, row)

[<Fact>]
let ``a hotkey no entry holds stands on no row`` () =
    let row = UserMenuState.positionOf (key "Q") sample

    Assert.Equal(None, row)

[<Theory>]
[<InlineData(0)>]
[<InlineData(1)>]
[<InlineData(3)>]
let ``a cursor on an entry stays where it is`` (index: int) =
    let cursor = UserMenuState.cursorWithin (Some index) sample

    Assert.Equal(Some index, cursor)

[<Fact>]
let ``a cursor left on a line moves to the entry below it`` () =
    let cursor = UserMenuState.cursorWithin (Some 2) sample

    Assert.Equal(Some 3, cursor)

[<Fact>]
let ``a cursor past the end moves to the last entry`` () =
    let shorter = stateOf [ CommandItem edit; CommandItem compare ]

    let cursor = UserMenuState.cursorWithin (Some 2) shorter

    Assert.Equal(Some 1, cursor)

[<Fact>]
let ``a cursor past the end of a menu closed by a line moves to the entry above the line`` () =
    let shorter = stateOf [ CommandItem edit; CommandItem compare; SeparatorItem ]

    let cursor = UserMenuState.cursorWithin (Some 3) shorter

    Assert.Equal(Some 1, cursor)

[<Fact>]
let ``a cursor on no row yet goes to the first entry`` () =
    let cursor = UserMenuState.cursorWithin None sample

    Assert.Equal(Some 0, cursor)

[<Fact>]
let ``a cursor on no row yet steps over a line that opens the menu`` () =
    let opened = stateOf [ SeparatorItem; CommandItem edit ]

    let cursor = UserMenuState.cursorWithin None opened

    Assert.Equal(Some 1, cursor)

[<Fact>]
let ``a menu emptied under the cursor has no row to stand on`` () =
    let emptied = UserMenuState.start UserMenu.empty

    let cursor = UserMenuState.cursorWithin (Some 0) emptied

    Assert.Equal(None, cursor)

[<Fact>]
let ``an empty menu opens with the cursor on no row`` () =
    let emptied = UserMenuState.start UserMenu.empty

    let cursor = UserMenuState.cursorWithin None emptied

    Assert.Equal(None, cursor)

[<Fact>]
let ``a menu of lines alone has no row to stand on`` () =
    let lines = stateOf [ SeparatorItem; SeparatorItem ]

    let cursor = UserMenuState.cursorWithin (Some 1) lines

    Assert.Equal(None, cursor)

[<Fact>]
let ``a step down goes over a line`` () =
    let row = UserMenuState.stepped Downward (Some 1) sample

    Assert.Equal(Some 3, row)

[<Fact>]
let ``a step up goes over a line`` () =
    let row = UserMenuState.stepped Upward (Some 3) sample

    Assert.Equal(Some 1, row)

[<Fact>]
let ``a step down from the last entry goes nowhere`` () =
    let row = UserMenuState.stepped Downward (Some 3) sample

    Assert.Equal(None, row)

[<Fact>]
let ``a step up from the first entry goes nowhere`` () =
    let row = UserMenuState.stepped Upward (Some 0) sample

    Assert.Equal(None, row)

[<Theory>]
[<InlineData(4, 1)>]
[<InlineData(4, 3)>]
[<InlineData(20, 2)>]
let ``rows that all fit are shown from the first one`` (height: int, top: int) =
    let shown = UserMenuState.topRowWithin height top sample

    Assert.Equal(0, shown)

[<Theory>]
[<InlineData(2, 0)>]
[<InlineData(2, 1)>]
[<InlineData(2, 2)>]
[<InlineData(1, 3)>]
let ``a list scrolled no further than its last rows keeps its place`` (height: int, top: int) =
    let shown = UserMenuState.topRowWithin height top sample

    Assert.Equal(top, shown)

[<Theory>]
[<InlineData(2, 3, 2)>]
[<InlineData(3, 2, 1)>]
[<InlineData(1, 7, 3)>]
let ``a list scrolled past its last rows comes back until the bottom row is the last one``
    (height: int, top: int, expected: int)
    =
    let shown = UserMenuState.topRowWithin height top sample

    Assert.Equal(expected, shown)

[<Fact>]
let ``an empty menu is shown from the first row`` () =
    let emptied = UserMenuState.start UserMenu.empty

    let shown = UserMenuState.topRowWithin 1 1 emptied

    Assert.Equal(0, shown)
