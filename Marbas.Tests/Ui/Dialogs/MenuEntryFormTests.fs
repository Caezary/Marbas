module Marbas.Tests.Ui.Dialogs.MenuEntryFormTests

open Xunit
open Marbas.Domain
open Marbas.Programs
open Marbas.Profile
open Marbas.Ui.Dialogs
open Marbas.Ui.Dialogs.MenuEntryForm
open Marbas.Tests

let private compare =
    { Hotkey = Expect.some (MenuHotkey.tryCreate "C")
      Label = Expect.some (MenuLabel.tryCreate "Compare")
      Line = Expect.some (CommandLine.tryCreate "code --diff %Lp %Rp")
      Run = Directly
      Elevation = AsUser }

let private filled hotkey label line =
    { LabelText = label
      HotkeyText = hotkey
      LineText = line
      ChosenRun = InShellKeptOpen
      ChosenElevation = AsAdministrator }

[<Fact>]
let ``fields filled in are an entry, the way of running and the rights as chosen`` () =
    let read = MenuEntryForm.read [ compare ] (filled "e" " Edit " "vim %p")

    Assert.Equal(
        Ok
            { Hotkey = Expect.some (MenuHotkey.tryCreate "E")
              Label = Expect.some (MenuLabel.tryCreate "Edit")
              Line = Expect.some (CommandLine.tryCreate "vim %p")
              Run = InShellKeptOpen
              Elevation = AsAdministrator },
        read
    )

[<Fact>]
let ``a hotkey another entry runs on is refused, naming that entry`` () =
    let read = MenuEntryForm.read [ compare ] (filled "c" "Copy" "copy %s")

    Assert.Equal(Error(HotkeyHeld compare), read)

[<Theory>]
[<InlineData("", "Edit", "vim %p")>]
[<InlineData("EE", "Edit", "vim %p")>]
[<InlineData("%", "Edit", "vim %p")>]
let ``a hotkey that is not one letter or digit is refused`` (hotkey: string, label: string, line: string) =
    let read = MenuEntryForm.read [] (filled hotkey label line)

    Assert.Equal(Error NoHotkey, read)

[<Fact>]
let ``an entry without a label is refused`` () =
    let read = MenuEntryForm.read [] (filled "E" "  " "vim %p")

    Assert.Equal(Error NoLabel, read)

[<Fact>]
let ``an entry without a command line is refused`` () =
    let read = MenuEntryForm.read [] (filled "E" "Edit" "")

    Assert.Equal(Error NoCommandLine, read)

[<Fact>]
let ``the refusal of a held hotkey names the hotkey and the entry holding it`` () =
    let said = MenuEntryForm.describe (HotkeyHeld compare)

    Assert.Contains("'C'", said)
    Assert.Contains("Compare", said)

[<Fact>]
let ``the fields of an entry are what it is made of`` () =
    let fields = MenuEntryForm.fieldsOf compare

    Assert.Equal(
        { LabelText = "Compare"
          HotkeyText = "C"
          LineText = "code --diff %Lp %Rp"
          ChosenRun = Directly
          ChosenElevation = AsUser },
        fields
    )
