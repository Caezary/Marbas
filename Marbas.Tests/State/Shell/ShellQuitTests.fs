module Marbas.Tests.State.Shell.ShellQuitTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.State
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.Shell.ShellFixture

let private at = Root.at

let private fp = FileSystemPath.create

[<Fact>]
let ``quit runs the quit port`` () =
    let mutable quit = false

    let deps =
        { noDeps () with
            OnQuit = fun () -> quit <- true }

    Shell.interpret deps ignore Quit

    Assert.True quit

[<Fact>]
let ``quit takes the workspace of the session with it`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "temp/Marbas/session/left.txt", MockFileData "left")

    let deps =
        { noDeps () with
            FileSystem = fs
            WorkspacePath = fp (at "temp/Marbas/session") }

    Shell.interpret deps ignore Quit

    Assert.False(fs.Directory.Exists(at "temp/Marbas/session"))
