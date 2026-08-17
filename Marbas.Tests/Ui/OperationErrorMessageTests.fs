module Marbas.Tests.Ui.OperationErrorMessageTests

open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Ui

let private fp = FileSystemPath.create

[<Fact>]
let ``a missing source is explained with its path`` () =
    let message = OperationErrorMessage.describe (SourceNotFound(fp @"C:\gone"))

    Assert.Contains(@"C:\gone", message)
    Assert.Contains("no longer exists", message)

[<Fact>]
let ``an existing target is explained with its path`` () =
    let message = OperationErrorMessage.describe (TargetExists(fp @"C:\data\new"))

    Assert.Contains(@"C:\data\new", message)
    Assert.Contains("already exists", message)

[<Fact>]
let ``access denied is explained with its path`` () =
    let message = OperationErrorMessage.describe (AccessDenied(fp @"C:\locked"))

    Assert.Contains(@"C:\locked", message)
    Assert.Contains("Access denied", message)

[<Fact>]
let ``an entry the batch stopped inside says so`` () =
    let message = OperationErrorMessage.describe (Cancelled(fp @"C:\data\tree"))

    Assert.Contains(@"C:\data\tree", message)
    Assert.Contains("Stopped before", message)

[<Fact>]
let ``an unexpected failure includes the underlying reason`` () =
    let message = OperationErrorMessage.describe (OperationFailed(fp @"C:\bad", "disk full"))

    Assert.Contains(@"C:\bad", message)
    Assert.Contains("disk full", message)

[<Fact>]
let ``every failed item is named with its own reason`` () =
    let items =
        [ { Name = EntryName.create "gone.txt"
            Outcome = Error(SourceNotFound(fp @"C:\data\gone.txt")) }
          { Name = EntryName.create "locked"
            Outcome = Error(AccessDenied(fp @"C:\data\locked")) } ]

    let message = OperationErrorMessage.describeFailures items

    Assert.Contains("gone.txt", message)
    Assert.Contains("no longer exists", message)
    Assert.Contains("locked", message)
    Assert.Contains("Access denied", message)

[<Fact>]
let ``items that succeeded are left out`` () =
    let items =
        [ { Name = EntryName.create "a.txt"; Outcome = Ok() }
          { Name = EntryName.create "gone.txt"
            Outcome = Error(SourceNotFound(fp @"C:\data\gone.txt")) } ]

    let message = OperationErrorMessage.describeFailures items

    Assert.DoesNotContain("a.txt", message)
    Assert.Contains("gone.txt", message)
