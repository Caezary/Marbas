module Marbas.Tests.Ui.ListingErrorMessageTests

open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.Ui

let private fp = FileSystemPath.create

[<Fact>]
let ``missing directory is explained with its path`` () =
    let message = ListingErrorMessage.describe (DirectoryNotFound(fp @"C:\gone"))

    Assert.Contains(@"C:\gone", message)
    Assert.Contains("no longer exists", message)

[<Fact>]
let ``access denied is explained with its path`` () =
    let message = ListingErrorMessage.describe (AccessDenied(fp @"C:\locked"))

    Assert.Contains(@"C:\locked", message)
    Assert.Contains("Access denied", message)

[<Fact>]
let ``unexpected failure includes the underlying reason`` () =
    let message = ListingErrorMessage.describe (ListingFailed(fp @"C:\bad", "drive not ready"))

    Assert.Contains(@"C:\bad", message)
    Assert.Contains("drive not ready", message)
