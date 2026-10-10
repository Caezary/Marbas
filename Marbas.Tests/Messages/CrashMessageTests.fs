module Marbas.Tests.Messages.CrashMessageTests

open Xunit
open Marbas.FileSystem
open Marbas.Messages
open Marbas.Tests

let private at = Root.at

let private logFile = at "config/Marbas/marbas.log"

let private logPath = FileSystemPath.create logFile

[<Fact>]
let ``a written crash points at the log in one line`` () =
    let message = CrashMessage.written logPath

    Assert.Equal($"Marbas stopped on an unexpected error. The details are in '{logFile}'.", message)

[<Fact>]
let ``a crash the log refused names the log and the reason`` () =
    let message = CrashMessage.unwritten (LogWriteFailed(logPath, "the disk is full"))

    Assert.Contains(logFile, message)
    Assert.Contains("the disk is full", message)

[<Fact>]
let ``a crash the log may not take names the log`` () =
    let message = CrashMessage.unwritten (LogAccessDenied logPath)

    Assert.Contains(logFile, message)
    Assert.Contains("access denied", message)
