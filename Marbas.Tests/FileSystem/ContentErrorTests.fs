module Marbas.Tests.FileSystem.ContentErrorTests

open System
open System.IO
open Xunit
open Marbas.FileSystem
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

[<Fact>]
let ``access denied maps to an error value`` () =
    let denied = UnauthorizedAccessException()

    let contentError = ContentError.mapException (fp (at "locked.txt")) denied

    Assert.Equal(AccessDenied(fp (at "locked.txt")), contentError)

[<Fact>]
let ``unexpected failures map to an error value`` () =
    let failure = IOException "boom"

    let contentError = ContentError.mapException (fp (at "bad.txt")) failure

    Assert.Equal(ReadFailed(fp (at "bad.txt"), "boom"), contentError)
