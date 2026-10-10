module Marbas.Tests.Adapters.EditorProcessTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Adapters
open Marbas.Tests

let private at = Root.at

let private notes = FileSystemPath.create (at "start/notes.txt")

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs

let private missingEditor = ProgramPath.tryCreate "marbas-no-such-editor"

let private handingOver () =
    let handedOver = ResizeArray<unit>()

    let handOver (work: unit -> unit) =
        handedOver.Add()
        work ()

    handedOver, handOver

[<Fact>]
let ``on Windows the editor is started without waiting for it`` () =
    let fs = mockFs ()

    let editor = EditorProcess.describe fs None Windows WindowPrograms.none None notes

    Assert.Equal(EditorProcess.StartsDetached, editor.Wait)

[<Fact>]
let ``on Windows the editor goes through the shell, so a console editor gets a window of its own`` () =
    let fs = mockFs ()

    let editor = EditorProcess.describe fs None Windows WindowPrograms.none None notes

    Assert.True editor.Start.UseShellExecute

[<Fact>]
let ``on Unix the editor takes the terminal, because a console editor has no other to run in`` () =
    let fs = mockFs ()

    let editor = EditorProcess.describe fs None Unix WindowPrograms.none None notes

    Assert.Equal(EditorProcess.TakesTheTerminal, editor.Wait)

[<Fact>]
let ``on Unix the editor is started inside the terminal handed over to it`` () =
    let fs = mockFs ()
    let handedOver, handOver = handingOver ()

    let started =
        EditorProcess.run fs handOver None Unix WindowPrograms.none missingEditor notes

    Assert.Equal(1, handedOver.Count)
    Assert.True(Result.isError started)

[<WindowsFact("The shell verb exists only on Windows")>]
let ``on Windows the editor is started without handing the terminal over`` () =
    let fs = mockFs ()
    let handedOver, handOver = handingOver ()

    let started =
        EditorProcess.run fs handOver None Windows WindowPrograms.none missingEditor notes

    Assert.Empty handedOver
    Assert.True(Result.isError started)

[<Fact>]
let ``on Unix the editor is started directly in the terminal the panels are drawn in`` () =
    let fs = mockFs ()

    let editor = EditorProcess.describe fs None Unix WindowPrograms.none None notes

    Assert.False editor.Start.UseShellExecute

[<Fact>]
let ``the editor is handed the file and starts in its directory`` () =
    let fs = mockFs ()

    let editor = EditorProcess.describe fs None Windows WindowPrograms.none None notes

    Assert.Equal<string list>([ at "start/notes.txt" ], List.ofSeq editor.Start.ArgumentList)
    Assert.Equal(at "start", editor.Start.WorkingDirectory)

[<Fact>]
let ``the editor chosen for the file comes before the one the environment names`` () =
    let fs = mockFs ()

    let editor =
        EditorProcess.describe fs (Some "nano") Unix WindowPrograms.none (ProgramPath.tryCreate "micro") notes

    Assert.Equal("micro", editor.Start.FileName)

[<Fact>]
let ``with nothing chosen the environment's editor is started`` () =
    let fs = mockFs ()

    let editor =
        EditorProcess.describe fs (Some "nano") Unix WindowPrograms.none None notes

    Assert.Equal("nano", editor.Start.FileName)

[<Fact>]
let ``on Windows a window editor is started from its file with its streams drained`` () =
    let fs = mockFs ()
    let windowPrograms = WindowPrograms.knowing "code.exe" (at "apps/Code.exe")

    let editor =
        EditorProcess.describe fs (Some "code.exe") Windows windowPrograms None notes

    Assert.Equal(at "apps/Code.exe", editor.Start.FileName)
    Assert.False editor.Start.UseShellExecute
    Assert.True editor.Start.RedirectStandardOutput
    Assert.Equal(EditorProcess.StartsDetached, editor.Wait)

[<Fact>]
let ``on Unix the editor takes the terminal even when it is a window program`` () =
    let fs = mockFs ()
    let windowPrograms = WindowPrograms.knowing "code.exe" (at "apps/Code.exe")

    let editor =
        EditorProcess.describe fs (Some "code.exe") Unix windowPrograms None notes

    Assert.Equal("code.exe", editor.Start.FileName)
    Assert.False editor.Start.RedirectStandardOutput
    Assert.Equal(EditorProcess.TakesTheTerminal, editor.Wait)
