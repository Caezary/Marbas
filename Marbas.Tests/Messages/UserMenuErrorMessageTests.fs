module Marbas.Tests.Messages.UserMenuErrorMessageTests

open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Profile
open Marbas.Messages
open Marbas.Tests

let private at = Root.at

let private menuPath = FileSystemPath.create (at "config/user-menu.conf")

[<Fact>]
let ``a file that cannot be read is named with the reason`` () =
    let message =
        UserMenuErrorMessage.describe (UserMenuFailed(menuPath, "the disk is gone"))

    Assert.Contains(at "config/user-menu.conf", message)
    Assert.Contains("the disk is gone", message)

[<Fact>]
let ``a file that may not be touched is named`` () =
    let message = UserMenuErrorMessage.describe (UserMenuAccessDenied menuPath)

    Assert.Contains(at "config/user-menu.conf", message)

[<Fact>]
let ``a hotkey already taken is named`` () =
    let message =
        UserMenuErrorMessage.describe (MenuRefused(HotkeyTaken(Expect.some (MenuHotkey.tryCreate "e"))))

    Assert.Contains("'E'", message)

[<Fact>]
let ``an entry removed in the meantime is named by its hotkey`` () =
    let message =
        UserMenuErrorMessage.describe (MenuRefused(CommandGone(Expect.some (MenuHotkey.tryCreate "q"))))

    Assert.Contains("'Q'", message)
