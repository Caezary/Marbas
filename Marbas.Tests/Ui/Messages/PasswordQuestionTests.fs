module Marbas.Tests.Ui.Messages.PasswordQuestionTests

open Xunit
open Marbas.Domain
open Marbas.Ui.Messages

[<Fact>]
let ``the label names the archive the password is for`` () =
    let label = PasswordQuestion.describe (EntryName.create "data.zip")

    Assert.Contains("data.zip", label)
    Assert.Contains("Password for", label)
