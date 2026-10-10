module Marbas.Tests.Messages.PasswordQuestionTests

open Xunit
open Marbas.Domain
open Marbas.Messages

[<Fact>]
let ``the label names the archive the password is for`` () =
    let label = PasswordQuestion.describe (EntryName.create "data.zip")

    Assert.Contains("data.zip", label)
    Assert.Contains("Password for", label)
