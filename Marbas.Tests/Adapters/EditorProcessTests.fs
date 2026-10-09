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

    let editor = EditorProcess.describe fs None Windows None notes

    Assert.Equal(EditorProcess.StartsDetached, editor.Wait)

[<Fact>]
let ``on Windows the editor goes through the shell, so a console editor gets a window of its own`` () =
    let fs = mockFs ()

    let editor = EditorProcess.describe fs None Windows None notes

    Assert.True editor.Start.UseShellExecute

[<Fact>]
let ``on Unix the editor takes the terminal, because a console editor has no other to run in`` () =
    let fs = mockFs ()

    let editor = EditorProcess.describe fs None Unix None notes

    Assert.Equal(EditorProcess.TakesTheTerminal, editor.Wait)

[<Fact>]
let ``on Unix the editor is started inside the terminal handed over to it`` () =
    let fs = mockFs ()
    let handedOver, handOver = handingOver ()

    let started = EditorProcess.run fs handOver None Unix missingEditor notes

    Assert.Equal(1, handedOver.Count)
    Assert.True(Result.isError started)

[<WindowsFact("The shell verb exists only on Windows")>]
let ``on Windows the editor is started without handing the terminal over`` () =
    let fs = mockFs ()
    let handedOver, handOver = handingOver ()

    let started = EditorProcess.run fs handOver None Windows missingEditor notes

    Assert.Empty handedOver
    Assert.True(Result.isError started)

[<Fact>]
let ``on Unix the editor is started directly in the terminal the panels are drawn in`` () =
    let fs = mockFs ()

    let editor = EditorProcess.describe fs None Unix None notes

    Assert.False editor.Start.UseShellExecute

[<Fact>]
let ``the editor is handed the file and starts in its directory`` () =
    let fs = mockFs ()

    let editor = EditorProcess.describe fs None Windows None notes

    Assert.Equal<string list>([ at "start/notes.txt" ], List.ofSeq editor.Start.ArgumentList)
    Assert.Equal(at "start", editor.Start.WorkingDirectory)

[<Fact>]
let ``the editor chosen for the file comes before the one the environment names`` () =
    let fs = mockFs ()

    let editor =
        EditorProcess.describe fs (Some "nano") Unix (ProgramPath.tryCreate "micro") notes

    Assert.Equal("micro", editor.Start.FileName)

[<Fact>]
let ``with nothing chosen the environment's editor is started`` () =
    let fs = mockFs ()

    let editor = EditorProcess.describe fs (Some "nano") Unix None notes

    Assert.Equal("nano", editor.Start.FileName)
