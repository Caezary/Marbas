module Marbas.Tests.Ui.DeleteConfirmationTests

open Xunit
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Ui

let private en = EntryName.create

let private request names scope = { Targets = names; Scope = scope }

[<Fact>]
let ``a single target is named in the question`` () =
    let message =
        DeleteConfirmation.describe (request [ en "notes.txt" ] NamedEntriesOnly)

    Assert.Contains("notes.txt", message)
    Assert.DoesNotContain(DeleteConfirmation.NonEmptyWarning, message)

[<Fact>]
let ``several targets are counted instead of listed`` () =
    let names = [ en "notes.txt"; en "docs"; en "readme.txt" ]

    let message = DeleteConfirmation.describe (request names NamedEntriesOnly)

    Assert.Contains("3", message)
    Assert.DoesNotContain("notes.txt", message)

[<Fact>]
let ``a wider scope adds the extra warning`` () =
    let message =
        DeleteConfirmation.describe (request [ en "docs" ] WithDirectoryContents)

    Assert.Contains("docs", message)
    Assert.Contains(DeleteConfirmation.NonEmptyWarning, message)
