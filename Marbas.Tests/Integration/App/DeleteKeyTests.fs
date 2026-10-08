module Marbas.Tests.Integration.App.DeleteKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Marbas.FileSystem
open Marbas.Ui.App
open Marbas.Ui.Keys
open Marbas.Ui.Messages
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/docs/inner.txt", MockFileData "x")
    fs.AddDirectory(at "start/empty")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "start/readme.txt", MockFileData "hi")
    fs

let private silent = ignore<Ports.ErrorReport>

let private agreeing = fun (_: Ports.Confirmation) -> true

type private Bin(fs: MockFileSystem) =
    let taken = ResizeArray<string>()

    member _.Taken = List.ofSeq taken

    member _.Send(path: FileSystemPath) =
        taken.Add(FileSystemPath.value path)
        Operations.delete fs Operations.unattended path

let private windowWith (fs: MockFileSystem) (bin: Bin) confirm reportError =
    focusedWindow
        { Fakes.dependencies fs (at "start") with
            Prompts.Confirm = confirm
            Prompts.ReportError = reportError
            Processes.SendToTrash = bin.Send }

let private onNotes (fs: MockFileSystem) bin confirm =
    let window = windowWith fs bin confirm silent
    (leftList window).SelectedItem <- 3
    window

[<Fact>]
let ``F8 asks for confirmation and sends the entry under the cursor to the trash`` () =
    let fs = mockFs ()
    let bin = Bin fs
    let asked = ResizeArray<string * string>()

    use window =
        onNotes fs bin (fun question ->
            asked.Add(question.Title, question.Question)
            true)

    pressKey window Key.F8

    let title, message = Assert.Single asked
    Assert.Equal(DeleteConfirmation.Title, title)
    Assert.Contains("notes.txt", message)
    Assert.Equal<string list>([ at "start/notes.txt" ], bin.Taken)
    Assert.Equal(4, (leftList window).Source.Count)

[<Fact>]
let ``Del does what F8 does`` () =
    let fs = mockFs ()
    let bin = Bin fs
    use window = onNotes fs bin agreeing

    pressKey window KeyMap.trashKey

    Assert.Equal<string list>([ at "start/notes.txt" ], bin.Taken)

[<Fact>]
let ``Shift and Del delete for good, leaving the trash out of it`` () =
    let fs = mockFs ()
    let bin = Bin fs
    let asked = ResizeArray<string * string>()

    use window =
        onNotes fs bin (fun question ->
            asked.Add(question.Title, question.Question)
            true)

    pressKey window KeyMap.permanentDeleteKey

    let title, _ = Assert.Single asked
    Assert.Equal(DeleteConfirmation.PermanentTitle, title)
    Assert.Empty bin.Taken
    Assert.False(fs.File.Exists(at "start/notes.txt"))
    Assert.Equal(4, (leftList window).Source.Count)

[<Fact>]
let ``Shift and F8 delete for good as well`` () =
    let fs = mockFs ()
    let bin = Bin fs
    use window = onNotes fs bin agreeing

    pressKey window Key.F8.WithShift

    Assert.Empty bin.Taken
    Assert.False(fs.File.Exists(at "start/notes.txt"))

[<Fact>]
let ``Alt and Del wipe the entry behind the progress window after a question of its own`` () =
    let fs = mockFs ()
    let bin = Bin fs
    let asked = ResizeArray<string * string>()
    let batches = ResizeArray<string>()

    use window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                Prompts.Confirm =
                    fun question ->
                        asked.Add(question.Title, question.Question)
                        true
                Processes.SendToTrash = bin.Send
                Runners.Batch =
                    fun title work finish ->
                        batches.Add title
                        work Operations.unattended |> finish }

    (leftList window).SelectedItem <- 3

    pressKey window KeyMap.wipeKey

    let title, message = Assert.Single asked
    Assert.Equal(DeleteConfirmation.WipeTitle, title)
    Assert.Contains(DeleteConfirmation.WipeLimits, message)
    Assert.Equal<string list>([ Shell.WipeTitle ], List.ofSeq batches)
    Assert.Empty bin.Taken
    Assert.False(fs.File.Exists(at "start/notes.txt"))
    Assert.Equal(4, (leftList window).Source.Count)

[<Fact>]
let ``F8 sends the entry to the trash behind the progress window, naming it on the way`` () =
    let fs = mockFs ()
    let bin = Bin fs
    let batches = ResizeArray<string>()
    let reported = ResizeArray<FileSystemPath>()

    use window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                Prompts.Confirm = agreeing
                Processes.SendToTrash = bin.Send
                Runners.Batch =
                    fun title work finish ->
                        batches.Add title

                        work
                            { Report = reported.Add
                              IsCancelled = fun () -> false }
                        |> finish }

    (leftList window).SelectedItem <- 3

    pressKey window Key.F8

    Assert.Equal<string list>([ Shell.DeleteTitle ], List.ofSeq batches)
    Assert.Equal<FileSystemPath list>([ FileSystemPath.create (at "start/notes.txt") ], List.ofSeq reported)

[<Fact>]
let ``Shift and F8 name every entry of the tree they remove behind the progress window`` () =
    let fs = mockFs ()
    let batches = ResizeArray<string>()
    let reported = ResizeArray<FileSystemPath>()

    use window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                Prompts.Confirm = agreeing
                Runners.Batch =
                    fun title work finish ->
                        batches.Add title

                        work
                            { Report = reported.Add
                              IsCancelled = fun () -> false }
                        |> finish }

    (leftList window).SelectedItem <- 1

    pressKey window Key.F8.WithShift

    Assert.Equal<string list>([ Shell.DeleteTitle ], List.ofSeq batches)

    Assert.Equal<FileSystemPath list>(
        [ FileSystemPath.create (at "start/docs")
          FileSystemPath.create (at "start/docs/inner.txt") ],
        List.ofSeq reported
    )

[<Fact>]
let ``a trash stopped with Esc leaves the entries it never reached`` () =
    let fs = mockFs ()
    let bin = Bin fs

    let stopped: Operations.BatchControl =
        { Report = ignore
          IsCancelled = fun () -> true }

    use window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                Prompts.Confirm = agreeing
                Prompts.ReportError = silent
                Processes.SendToTrash = bin.Send
                Runners.Batch = fun _ work finish -> work stopped |> finish }

    let left = leftList window
    left.SelectedItem <- 3
    press left KeyMap.insertKey
    press left KeyMap.insertKey

    pressKey window Key.F8

    Assert.Equal<string list>([ at "start/notes.txt" ], bin.Taken)
    Assert.True(fs.File.Exists(at "start/readme.txt"))

[<Fact>]
let ``a wipe stopped with Esc leaves the entries it never reached and says where it stopped`` () =
    let fs = mockFs ()
    let reported = ResizeArray<string>()

    let stopped: Operations.BatchControl =
        { Report = ignore
          IsCancelled = fun () -> true }

    use window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                Prompts.Confirm = agreeing
                Prompts.ReportError = fun report -> reported.Add report.Message
                Runners.Batch = fun _ work finish -> work stopped |> finish }

    (leftList window).SelectedItem <- 3

    pressKey window KeyMap.wipeKey

    Assert.Equal("hi", fs.File.ReadAllText(at "start/notes.txt"))
    Assert.Contains("notes.txt", Assert.Single reported)
    Assert.Equal(5, (leftList window).Source.Count)

[<Fact>]
let ``a refused confirmation deletes nothing`` () =
    let fs = mockFs ()
    let bin = Bin fs
    use window = onNotes fs bin (fun _ -> false)

    pressKey window Key.F8

    Assert.Empty bin.Taken
    Assert.True(fs.File.Exists(at "start/notes.txt"))
    Assert.Equal(5, (leftList window).Source.Count)

[<Fact>]
let ``F8 on the parent entry asks nothing`` () =
    let fs = mockFs ()
    let asked = ResizeArray<string>()

    use window =
        windowWith
            fs
            (Bin fs)
            (fun (question: Ports.Confirmation) ->
                asked.Add question.Title
                true)
            silent

    pressKey window Key.F8

    Assert.Empty asked

[<Fact>]
let ``a failed delete is reported`` () =
    let fs = mockFs ()
    let reported = ResizeArray<string>()

    let window =
        windowWith fs (Bin fs) agreeing (fun report -> reported.Add report.Message)

    use window = window
    (leftList window).SelectedItem <- 3
    fs.File.Delete(at "start/notes.txt")

    pressKey window Key.F8

    Assert.Contains("notes.txt", Assert.Single reported)
