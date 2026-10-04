module Marbas.Tests.Ui.Messages.ClipboardErrorMessageTests

open Xunit
open Marbas.Domain
open Marbas.Ui.Messages

[<Fact>]
let ``a clipboard the system does not offer says it is not there yet`` () =
    let message = ClipboardErrorMessage.describe ClipboardUnavailable

    Assert.Equal("Marbas cannot reach the clipboard on this system yet.", message)

[<Fact>]
let ``a clipboard that failed says why`` () =
    let message = ClipboardErrorMessage.describe (ClipboardFailed "Access is denied.")

    Assert.Equal("The clipboard could not be used: Access is denied.", message)
