module Marbas.Tests.Integration.DeleteKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Marbas.FileSystem
open Marbas.Ui
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

let private silent = fun (_: string) (_: string) -> ()

let private agreeing = fun (_: string) (_: string) -> true

type private Bin(fs: MockFileSystem) =
    let taken = ResizeArray<string>()

    member _.Taken = List.ofSeq taken

    member _.Send(path: FileSystemPath) =
        taken.Add(FileSystemPath.value path)
        Operations.delete fs path

let private windowWith (fs: MockFileSystem) (bin: Bin) confirm reportError =
    focusedWindow
        { Fakes.dependencies fs (at "start") with
            Confirm = confirm
            ReportError = reportError
            SendToTrash = bin.Send }

let private onNotes (fs: MockFileSystem) bin confirm =
    let window = windowWith fs bin confirm silent
    (leftList window).SelectedItem <- 3
    window

[<Fact>]
let ``F8 asks for confirmation and sends the entry under the cursor to the trash`` () =
    let fs = mockFs ()
    let bin = Bin fs
    let asked = ResizeArray<string * string>()
    use window = onNotes fs bin (fun title message -> asked.Add(title, message); true)

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
    use window = onNotes fs bin (fun title message -> asked.Add(title, message); true)

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
let ``a refused confirmation deletes nothing`` () =
    let fs = mockFs ()
    let bin = Bin fs
    use window = onNotes fs bin (fun _ _ -> false)

    pressKey window Key.F8

    Assert.Empty bin.Taken
    Assert.True(fs.File.Exists(at "start/notes.txt"))
    Assert.Equal(5, (leftList window).Source.Count)

[<Fact>]
let ``F8 on the parent entry asks nothing`` () =
    let fs = mockFs ()
    let asked = ResizeArray<string>()
    use window = windowWith fs (Bin fs) (fun title _ -> asked.Add title; true) silent

    pressKey window Key.F8

    Assert.Empty asked

[<Fact>]
let ``a failed delete is reported`` () =
    let fs = mockFs ()
    let reported = ResizeArray<string>()
    let window = windowWith fs (Bin fs) agreeing (fun _ -> reported.Add)
    use window = window
    (leftList window).SelectedItem <- 3
    fs.File.Delete(at "start/notes.txt")

    pressKey window Key.F8

    Assert.Contains("notes.txt", Assert.Single reported)
