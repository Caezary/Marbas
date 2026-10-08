module Marbas.Tests.Ui.Messages.ListingErrorMessageTests

open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.Ui.Messages
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

[<Fact>]
let ``missing directory is explained with its path`` () =
    let message = ListingErrorMessage.describe (DirectoryNotFound(fp (at "gone")))

    Assert.Contains(at "gone", message)
    Assert.Contains("no longer exists", message)

[<Fact>]
let ``access denied is explained with its path`` () =
    let message = ListingErrorMessage.describe (AccessDenied(fp (at "locked")))

    Assert.Contains(at "locked", message)
    Assert.Contains("Access denied", message)

[<Fact>]
let ``unexpected failure includes the underlying reason`` () =
    let message =
        ListingErrorMessage.describe (ListingFailed(fp (at "bad"), "drive not ready"))

    Assert.Contains(at "bad", message)
    Assert.Contains("drive not ready", message)

[<Fact>]
let ``an archive that cannot be read is explained by the archive wording`` () =
    let message =
        ListingErrorMessage.describe (ArchiveUnreadable(KindCannotBeRead(fp (at "notes.txt"))))

    Assert.Equal(ArchiveErrorMessage.describe (KindCannotBeRead(fp (at "notes.txt"))), message)

[<Fact>]
let ``a directory failure is titled as a directory and an archive failure as an archive`` () =
    Assert.Equal(ListingErrorMessage.Title, ListingErrorMessage.title (DirectoryNotFound(fp (at "gone"))))

    Assert.Equal(
        ArchiveErrorMessage.Title,
        ListingErrorMessage.title (ArchiveUnreadable(ArchiveNotFound(fp (at "gone.zip"))))
    )
