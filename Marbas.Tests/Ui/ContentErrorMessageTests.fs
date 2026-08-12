module Marbas.Tests.Ui.ContentErrorMessageTests

open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.FileContent
open Marbas.Ui

let private fp = FileSystemPath.create

[<Fact>]
let ``a missing file is explained with its path`` () =
    let message = ContentErrorMessage.describe (FileNotFound(fp @"C:\gone.txt"))

    Assert.Contains(@"C:\gone.txt", message)
    Assert.Contains("no longer exists", message)

[<Fact>]
let ``access denied is explained with its path`` () =
    let message = ContentErrorMessage.describe (AccessDenied(fp @"C:\locked.txt"))

    Assert.Contains(@"C:\locked.txt", message)
    Assert.Contains("Access denied", message)

[<Fact>]
let ``unexpected failure includes the underlying reason`` () =
    let message = ContentErrorMessage.describe (ReadFailed(fp @"C:\bad.txt", "drive not ready"))

    Assert.Contains(@"C:\bad.txt", message)
    Assert.Contains("drive not ready", message)
