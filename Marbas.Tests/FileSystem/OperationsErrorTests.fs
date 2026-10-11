module Marbas.Tests.FileSystem.OperationsErrorTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

[<Fact>]
let ``access denied maps to an error value`` () =
    let denied = UnauthorizedAccessException()

    let operationError = mapException (fp (at "locked")) denied

    Assert.Equal(AccessDenied(fp (at "locked")), operationError)

[<Fact>]
let ``unexpected failures map to an error value`` () =
    let failure = IO.IOException "boom"

    let operationError = mapException (fp (at "bad")) failure

    Assert.Equal(OperationFailed(fp (at "bad"), "boom"), operationError)

[<Fact>]
let ``a name Windows has no application for maps to an error value`` () =
    let refusal = ComponentModel.Win32Exception 1155

    let operationError = mapException (fp (at "notes.odd")) refusal

    Assert.Equal(NoProgramAssociated(fp (at "notes.odd")), operationError)

[<Fact>]
let ``an elevation prompt the user turned down maps to an error value`` () =
    let refusal = ComponentModel.Win32Exception 1223

    let operationError = mapException (fp (at "tool.exe")) refusal

    Assert.Equal(StartDeclined(fp (at "tool.exe")), operationError)

[<Fact>]
let ``a symbolic link refused for want of a privilege maps to an error value`` () =
    let refusal =
        IO.IOException("A required privilege is not held by the client.", Fakes.PrivilegeNotHeld)

    let operationError = mapException (fp (at "data/twin")) refusal

    Assert.Equal(SymbolicLinkNotPermitted(fp (at "data/twin")), operationError)

[<Fact>]
let ``copying a directory link the system will not let this user make says so`` () =
    let fs = Fakes.SymbolicLinksRefused(at "backup")
    fs.AddFile(at "data/docs/inner.txt", MockFileData "inner")
    fs.Directory.CreateSymbolicLink(at "data/twin", at "data/docs") |> ignore
    fs.AddDirectory(at "backup")

    let failure =
        copy fs unattended (fp (at "backup")) (fp (at "data/twin")) |> Expect.error

    Assert.Equal(SymbolicLinkNotPermitted(fp (at "data/twin")), failure)

[<Fact>]
let ``a Windows failure of any other code keeps the words the system gave`` () =
    let refusal = ComponentModel.Win32Exception 2

    let operationError = mapException (fp (at "gone.exe")) refusal

    Assert.Equal(OperationFailed(fp (at "gone.exe"), refusal.Message), operationError)
