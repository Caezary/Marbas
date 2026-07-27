module Marbas.Tests.OperationErrorMessageTests

open Xunit
open Marbas.FileSystem.Operations
open Marbas.Ui

[<Fact>]
let ``a missing source is explained with its path`` () =
    let message = OperationErrorMessage.describe (SourceNotFound @"C:\gone")

    Assert.Contains(@"C:\gone", message)
    Assert.Contains("no longer exists", message)

[<Fact>]
let ``an existing target is explained with its path`` () =
    let message = OperationErrorMessage.describe (TargetExists @"C:\data\new")

    Assert.Contains(@"C:\data\new", message)
    Assert.Contains("already exists", message)

[<Fact>]
let ``access denied is explained with its path`` () =
    let message = OperationErrorMessage.describe (AccessDenied @"C:\locked")

    Assert.Contains(@"C:\locked", message)
    Assert.Contains("Access denied", message)

[<Fact>]
let ``an unexpected failure includes the underlying reason`` () =
    let message = OperationErrorMessage.describe (OperationFailed(@"C:\bad", "disk full"))

    Assert.Contains(@"C:\bad", message)
    Assert.Contains("disk full", message)
