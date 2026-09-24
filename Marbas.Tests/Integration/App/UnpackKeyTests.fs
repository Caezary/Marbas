module Marbas.Tests.Integration.App.UnpackKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Ui.Keys
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private fp = FileSystemPath.create

let private zipFormat = ArchiveFormat.ofKind Zip |> Expect.some

let private mockFs password =
    let fs = MockFileSystem()
    fs.AddFile(at "src/notes.txt", MockFileData "notes")
    fs.AddFile(at "src/docs/inner.txt", MockFileData "inner")
    fs.AddDirectory(at "start")
    fs.AddDirectory(at "backup")

    Archive.write
        fs
        Operations.unattended
        { Path = fp (at "start/data.zip")
          Format = zipFormat
          Compression = Normal
          Password = password }
        [ fp (at "src/notes.txt"); fp (at "src/docs") ]
    |> ignore

    fs

let private windowWith deps =
    let window = focusedWindow deps

    press (leftList window) KeyMap.rightRootKey
    (leftList window).SelectedItem <- 1
    window

let private opening (fs: MockFileSystem) =
    { Fakes.dependencies fs (at "start") with
        Prompts.ChooseDrive = Fakes.choosingDrive (at "backup") }

[<Fact>]
let ``Shift+F2 empties the archive under the cursor into the other panel`` () =
    let fs = mockFs None
    use window = windowWith (opening fs)

    pressKey window KeyMap.unpackKey

    Assert.Equal("notes", fs.File.ReadAllText(at "backup/notes.txt"))
    Assert.Equal("inner", fs.File.ReadAllText(at "backup/docs/inner.txt"))
    Assert.Equal(3, (rightList window).Source.Count)

[<Fact>]
let ``Shift+F2 on a protected archive asks for the password first`` () =
    let fs = mockFs (ArchivePassword.tryCreate "letmein")

    use window =
        windowWith
            { opening fs with
                Prompts.AskPassword = (fun _ -> ArchivePassword.tryCreate "letmein") }

    pressKey window KeyMap.unpackKey

    Assert.Equal("notes", fs.File.ReadAllText(at "backup/notes.txt"))

[<Fact>]
let ``Shift+F2 on an entry that is no archive leaves the disk alone`` () =
    let fs = mockFs None
    fs.AddFile(at "start/notes.txt", MockFileData "plain")

    use window = windowWith (opening fs)
    (leftList window).SelectedItem <- 2

    pressKey window KeyMap.unpackKey

    Assert.Empty(fs.Directory.GetFiles(at "backup"))
