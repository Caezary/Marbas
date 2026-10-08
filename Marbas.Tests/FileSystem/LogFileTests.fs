module Marbas.Tests.FileSystem.LogFileTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Tests

let private at = Root.at

let private logPath = FileSystemPath.create (at "config/Marbas/marbas.log")

let private kibibyteLine = String('x', 1023) + "\n"

let private megabyteOfLines = String.replicate 1024 kibibyteLine

let private withLog (text: string) =
    let fs = MockFileSystem()
    fs.AddFile(at "config/Marbas/marbas.log", MockFileData text)
    fs

[<Fact>]
let ``the log sits beside the settings, in a file of its own`` () =
    let fs = MockFileSystem()

    let path =
        LogFile.beside fs (FileSystemPath.create (at "config/Marbas/settings.conf"))

    Assert.Equal(at "config/Marbas/marbas.log", FileSystemPath.value path)

[<Fact>]
let ``a log longer than a megabyte keeps only its last megabyte`` () =
    let fs = withLog ("first line\n" + megabyteOfLines)

    let cut = LogFile.cut fs logPath

    Assert.Equal(Ok(), cut)
    Assert.Equal(megabyteOfLines, fs.File.ReadAllText(at "config/Marbas/marbas.log"))

[<Fact>]
let ``a line split by the megabyte boundary is dropped whole`` () =
    let fs = withLog (megabyteOfLines + "last\n")

    LogFile.cut fs logPath |> ignore

    let kept = fs.File.ReadAllText(at "config/Marbas/marbas.log")
    Assert.Equal(String.replicate 1023 kibibyteLine + "last\n", kept)

[<Fact>]
let ``a log within a megabyte is left as it is`` () =
    let fs = withLog "only line\n"

    let cut = LogFile.cut fs logPath

    Assert.Equal(Ok(), cut)
    Assert.Equal("only line\n", fs.File.ReadAllText(at "config/Marbas/marbas.log"))

[<Fact>]
let ``cutting a log that was never written makes none`` () =
    let fs = MockFileSystem()

    let cut = LogFile.cut fs logPath

    Assert.Equal(Ok(), cut)
    Assert.False(fs.File.Exists(at "config/Marbas/marbas.log"))

[<Fact>]
let ``lines are appended after what the log already holds`` () =
    let fs = withLog ("earlier" + Environment.NewLine)

    let appended = LogFile.append fs logPath [ "one"; "two" ]

    Assert.Equal(Ok(), appended)
    Assert.Equal<string>([| "earlier"; "one"; "two" |], fs.File.ReadAllLines(at "config/Marbas/marbas.log"))

[<Fact>]
let ``the first lines make the log and its directory`` () =
    let fs = MockFileSystem()

    LogFile.append fs logPath [ "one" ] |> ignore

    Assert.Equal<string>([| "one" |], fs.File.ReadAllLines(at "config/Marbas/marbas.log"))

[<Fact>]
let ``a log that cannot be written is an error naming it`` () =
    let fs = withLog ""
    fs.File.SetAttributes(at "config/Marbas/marbas.log", IO.FileAttributes.ReadOnly)

    let appended = LogFile.append fs logPath [ "one" ]

    Assert.Equal(Error(LogAccessDenied logPath), appended)
