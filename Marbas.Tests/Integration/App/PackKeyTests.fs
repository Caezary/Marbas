module Marbas.Tests.Integration.App.PackKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Ui.App
open Marbas.Ui.Keys
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/docs/inner.txt", MockFileData "x")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddDirectory(at "backup")
    fs

let private windowWith (fs: MockFileSystem) askArchive =
    let window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                Prompts.ChooseDrive = Fakes.choosingDrive (at "backup")
                Prompts.AskArchive = askArchive }

    press (leftList window) KeyMap.rightRootKey
    window

let private zipFormat = ArchiveFormat.tryCreate Zip |> Expect.some

let private answering target =
    fun (_: Ports.PackPrompt) ->
        Some
            { Target = target
              Format = zipFormat
              Compression = Normal
              Password = None }

[<Fact>]
let ``Shift+F1 packs the entry under the cursor into the other panel`` () =
    let fs = mockFs ()
    use window = windowWith fs (answering (at "backup/notes.zip"))
    (leftList window).SelectedItem <- 2

    pressKey window KeyMap.packKey

    Assert.True(fs.File.Exists(at "backup/notes.zip"))
    Assert.Equal(2, (rightList window).Source.Count)

[<Fact>]
let ``Shift+F1 offers an archive named after the entry under the cursor`` () =
    let fs = mockFs ()
    let mutable offered = ""

    use window =
        windowWith
            fs
            (fun request ->
                offered <- FileSystemPath.value request.Suggestion
                None)

    (leftList window).SelectedItem <- 2

    pressKey window KeyMap.packKey

    Assert.Equal(at "backup/notes.zip", offered)

[<Fact>]
let ``a cancelled pack dialog leaves the disk alone`` () =
    let fs = mockFs ()
    use window = windowWith fs (fun _ -> None)
    (leftList window).SelectedItem <- 2

    pressKey window KeyMap.packKey

    Assert.Empty(fs.Directory.GetFiles(at "backup"))
