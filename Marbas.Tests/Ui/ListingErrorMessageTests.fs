module Marbas.Tests.Ui.ListingErrorMessageTests

open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.Ui
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
    let message = ListingErrorMessage.describe (ListingFailed(fp (at "bad"), "drive not ready"))

    Assert.Contains(at "bad", message)
    Assert.Contains("drive not ready", message)
