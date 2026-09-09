module Marbas.Tests.Ui.Messages.ArchiveErrorMessageTests

open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.Messages
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private archive = at "backup/data.zip"

[<Fact>]
let ``a kind that cannot be read says so with its path`` () =
    let message = ArchiveErrorMessage.describe (KindCannotBeRead(fp archive))

    Assert.Contains(archive, message)
    Assert.Contains("not an archive", message)

[<Fact>]
let ``an archive that is gone is explained as gone`` () =
    let message = ArchiveErrorMessage.describe (ArchiveNotFound(fp archive))

    Assert.Contains(archive, message)
    Assert.Contains("no longer exists", message)

[<Fact>]
let ``a protected archive says a password is needed`` () =
    let message = ArchiveErrorMessage.describe (PasswordNeeded(fp archive))

    Assert.Contains(archive, message)
    Assert.Contains("password", message)

[<Fact>]
let ``a damaged archive carries the reason it gave`` () =
    let message = ArchiveErrorMessage.describe (ArchiveDamaged(fp archive, "unknown header"))

    Assert.Contains(archive, message)
    Assert.Contains("unknown header", message)

[<Fact>]
let ``the password prompt names the archive it is for`` () =
    let label = PasswordQuestion.describe (EntryName.create "data.zip")

    Assert.Contains("data.zip", label)
    Assert.Contains("Password", label)

[<Fact>]
let ``a kind that cannot be changed says so with its path`` () =
    let message = ArchiveErrorMessage.describe (KindCannotBeChanged(fp archive))

    Assert.Contains(archive, message)
    Assert.Contains("can change", message)

[<Fact>]
let ``an archive behind a password says why it cannot be changed`` () =
    let message = ArchiveErrorMessage.describe (ProtectedArchiveCannotBeChanged(fp archive))

    Assert.Contains(archive, message)
    Assert.Contains("password", message)

[<Fact>]
let ``an archive that refuses access says so with its path`` () =
    let message = ArchiveErrorMessage.describe (ArchiveAccessDenied(fp archive))

    Assert.Contains(archive, message)
    Assert.Contains("Access denied", message)
