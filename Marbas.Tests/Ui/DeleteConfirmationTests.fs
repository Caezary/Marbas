module Marbas.Tests.Ui.DeleteConfirmationTests

open Xunit
open Marbas.Ui

[<Fact>]
let ``a single target is named in the question`` () =
    let message = DeleteConfirmation.describe [ "notes.txt" ] false

    Assert.Contains("notes.txt", message)
    Assert.DoesNotContain(DeleteConfirmation.NonEmptyWarning, message)

[<Fact>]
let ``several targets are counted instead of listed`` () =
    let message = DeleteConfirmation.describe [ "notes.txt"; "docs"; "readme.txt" ] false

    Assert.Contains("3", message)
    Assert.DoesNotContain("notes.txt", message)

[<Fact>]
let ``a non-empty directory adds the extra warning`` () =
    let message = DeleteConfirmation.describe [ "docs" ] true

    Assert.Contains("docs", message)
    Assert.Contains(DeleteConfirmation.NonEmptyWarning, message)
