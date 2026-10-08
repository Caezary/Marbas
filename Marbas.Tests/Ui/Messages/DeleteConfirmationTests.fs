module Marbas.Tests.Ui.Messages.DeleteConfirmationTests

open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Ui.Messages

let private en = EntryName.create

let private request names scope =
    { Targets = names
      Scope = scope
      Mode = ToTrash }

let private permanent names scope =
    { request names scope with
        Mode = Permanently }

[<Fact>]
let ``a single target is named in the question`` () =
    let message =
        DeleteConfirmation.describe (request [ en "notes.txt" ] NamedEntriesOnly)

    Assert.Contains("notes.txt", message)
    Assert.DoesNotContain(DeleteConfirmation.TrashWarning, message)

[<Fact>]
let ``several targets are counted instead of listed`` () =
    let names = [ en "notes.txt"; en "docs"; en "readme.txt" ]

    let message = DeleteConfirmation.describe (request names NamedEntriesOnly)

    Assert.Contains("3", message)
    Assert.DoesNotContain("notes.txt", message)

[<Fact>]
let ``a wider scope adds the extra warning`` () =
    let message =
        DeleteConfirmation.describe (permanent [ en "docs" ] WithDirectoryContents)

    Assert.Contains("docs", message)
    Assert.Contains(DeleteConfirmation.NonEmptyWarning, message)

[<Fact>]
let ``the trash question says where the entry is going`` () =
    let message =
        DeleteConfirmation.describe (request [ en "notes.txt" ] WithDirectoryContents)

    Assert.Contains("trash", message)
    Assert.Contains(DeleteConfirmation.TrashWarning, message)

[<Fact>]
let ``a permanent delete asks a question of its own, under a title of its own`` () =
    let asked = permanent [ en "notes.txt" ] NamedEntriesOnly

    let message = DeleteConfirmation.describe asked

    Assert.DoesNotContain("trash", message)
    Assert.Equal(DeleteConfirmation.PermanentTitle, DeleteConfirmation.title asked)

let private wiping names scope =
    { request names scope with
        Mode = ByWiping }

[<Fact>]
let ``a wipe asks under a title of its own`` () =
    let title = DeleteConfirmation.title (wiping [ en "notes.txt" ] NamedEntriesOnly)

    Assert.Equal(DeleteConfirmation.WipeTitle, title)

[<Fact>]
let ``a wipe names its target and says what wiping does and cannot promise`` () =
    let message =
        DeleteConfirmation.describe (wiping [ en "notes.txt" ] NamedEntriesOnly)

    Assert.Contains("notes.txt", message)
    Assert.Contains(DeleteConfirmation.WipeExplanation, message)
    Assert.Contains(DeleteConfirmation.WipeLimits, message)
    Assert.DoesNotContain("trash", message)

[<Fact>]
let ``a wipe reaching into a directory says the contents are wiped too`` () =
    let message =
        DeleteConfirmation.describe (wiping [ en "docs"; en "notes.txt" ] WithDirectoryContents)

    Assert.Contains("2", message)
    Assert.Contains(DeleteConfirmation.WipeContentsWarning, message)
    Assert.Contains(DeleteConfirmation.WipeLimits, message)

[<Fact>]
let ``a delete to the trash keeps the plain title`` () =
    let title = DeleteConfirmation.title (request [ en "notes.txt" ] NamedEntriesOnly)

    Assert.Equal(DeleteConfirmation.Title, title)
