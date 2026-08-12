module Marbas.Tests.Ui.DeleteConfirmationTests

open Xunit
open Marbas.FileSystem
open Marbas.Ui

let private en = EntryName.create

[<Fact>]
let ``a single target is named in the question`` () =
    let message = DeleteConfirmation.describe [ en "notes.txt" ] false

    Assert.Contains("notes.txt", message)
    Assert.DoesNotContain(DeleteConfirmation.NonEmptyWarning, message)

[<Fact>]
let ``several targets are counted instead of listed`` () =
    let message =
        DeleteConfirmation.describe [ en "notes.txt"; en "docs"; en "readme.txt" ] false

    Assert.Contains("3", message)
    Assert.DoesNotContain("notes.txt", message)

[<Fact>]
let ``a non-empty directory adds the extra warning`` () =
    let message = DeleteConfirmation.describe [ en "docs" ] true

    Assert.Contains("docs", message)
    Assert.Contains(DeleteConfirmation.NonEmptyWarning, message)
