module Marbas.Tests.Messages.CrashMessageTests

open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Messages
open Marbas.Tests

let private at = Root.at

let private logFile = at "config/Marbas/marbas.log"

let private logPath = FileSystemPath.create logFile

let private version = ProductVersion "0.1.0"

[<Fact>]
let ``a written crash names the version and points at the log in one line`` () =
    let message = CrashMessage.written version logPath

    Assert.Equal($"Marbas 0.1.0 stopped on an unexpected error. The details are in '{logFile}'.", message)

[<Fact>]
let ``a crash the log refused names the version, the log and the reason`` () =
    let message =
        CrashMessage.unwritten version (LogWriteFailed(logPath, "the disk is full"))

    Assert.StartsWith("Marbas 0.1.0 stopped", message)
    Assert.Contains(logFile, message)
    Assert.Contains("the disk is full", message)

[<Fact>]
let ``a crash the log may not take names the version and the log`` () =
    let message = CrashMessage.unwritten version (LogAccessDenied logPath)

    Assert.StartsWith("Marbas 0.1.0 stopped", message)
    Assert.Contains(logFile, message)
    Assert.Contains("access denied", message)
