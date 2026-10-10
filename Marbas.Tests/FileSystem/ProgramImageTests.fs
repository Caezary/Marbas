module Marbas.Tests.FileSystem.ProgramImageTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Tests
open Marbas.Tests.ProgramImageBytes

let private at = Root.at

let private fp = FileSystemPath.create

let private holding (bytes: byte array) =
    let fs = MockFileSystem()
    fs.AddFile(at "tools/program.exe", MockFileData bytes)
    fs

let private executable = fp (at "tools/program.exe")

[<Fact>]
let ``a window program's header says so`` () =
    let fs = holding windowImage

    let subsystem = ProgramImage.subsystem fs executable

    Assert.Equal(WindowSubsystem, subsystem)

[<Fact>]
let ``a console program's header says so`` () =
    let fs = holding consoleImage

    let subsystem = ProgramImage.subsystem fs executable

    Assert.Equal(ConsoleSubsystem, subsystem)

[<Fact>]
let ``a header naming any other subsystem is told apart from both`` () =
    let fs = holding (imageWith HeaderAt 1us)

    let subsystem = ProgramImage.subsystem fs executable

    Assert.Equal(OtherSubsystem, subsystem)

[<Fact>]
let ``a file too short to hold a header is not an image`` () =
    let fs = holding "MZ"B

    let subsystem = ProgramImage.subsystem fs executable

    Assert.Equal(NotAnImage, subsystem)

[<Fact>]
let ``a file that is not an executable is not an image`` () =
    let fs = holding "@echo off\r\ncode %*\r\n"B

    let subsystem = ProgramImage.subsystem fs executable

    Assert.Equal(NotAnImage, subsystem)

[<Fact>]
let ``a header pointing past the end of the file is not an image`` () =
    let fs = holding (imageWith 0x1000 2us)

    let subsystem = ProgramImage.subsystem fs executable

    Assert.Equal(NotAnImage, subsystem)

[<Fact>]
let ``a header without the PE signature where it points is not an image`` () =
    let fs = holding (imageWith 0x40 2us)

    let subsystem = ProgramImage.subsystem fs executable

    Assert.Equal(NotAnImage, subsystem)

[<Fact>]
let ``a file that is not there cannot be read`` () =
    let fs = MockFileSystem()

    let subsystem = ProgramImage.subsystem fs executable

    Assert.Equal(UnreadableImage, subsystem)
