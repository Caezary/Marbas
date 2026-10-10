module Marbas.Tests.State.Messages.ContentErrorMessageTests

open Xunit
open Marbas.FileSystem
open Marbas.State.Messages
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

[<Fact>]
let ``a missing file is explained with its path`` () =
    let message = ContentErrorMessage.describe (FileNotFound(fp (at "gone.txt")))

    Assert.Contains(at "gone.txt", message)
    Assert.Contains("no longer exists", message)

[<Fact>]
let ``access denied is explained with its path`` () =
    let message = ContentErrorMessage.describe (AccessDenied(fp (at "locked.txt")))

    Assert.Contains(at "locked.txt", message)
    Assert.Contains("Access denied", message)

[<Fact>]
let ``unexpected failure includes the underlying reason`` () =
    let message =
        ContentErrorMessage.describe (ReadFailed(fp (at "bad.txt"), "drive not ready"))

    Assert.Contains(at "bad.txt", message)
    Assert.Contains("drive not ready", message)
