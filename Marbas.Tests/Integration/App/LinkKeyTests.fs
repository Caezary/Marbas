module Marbas.Tests.Integration.App.LinkKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.Keys
open Marbas.Ui.Messages
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/docs/inner.txt", MockFileData "x")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddDirectory(at "backup")
    fs

let private unreachable: NativeLinks =
    { MakeHard = fun _ _ -> failwith "no hard link expected"
      MakeJunction = fun _ _ -> failwith "no junction expected" }

let private windowWith (fs: MockFileSystem) askLink reportError =
    let window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                ChooseOption = (fun _ _ -> Some(at "backup"))
                AskLink = askLink
                RefuseLink = Links.refusal Windows fs
                MakeLink = Links.create unreachable Windows fs
                ReportError = reportError }

    press (leftList window) KeyMap.rightRootKey
    window

let private answering target kind =
    fun (_: string) (_: string -> LinkKind -> LinkRefusal option) -> Some { Target = target; Kind = kind }

let private silent (_: string) (_: string) = ()

[<Fact>]
let ``Alt+F6 makes a link in the other panel and shows it there as a link under the cursor`` () =
    let fs = mockFs ()
    use window = windowWith fs (answering (at "backup/twin.txt") Symbolic) silent
    (leftList window).SelectedItem <- 2

    pressKey window KeyMap.linkKey

    Assert.Equal(at "start/notes.txt", fs.FileInfo.New(at "backup/twin.txt").LinkTarget)
    Assert.Equal(System.Nullable 1, (rightList window).SelectedItem)
    Assert.Equal(2, (rightList window).Source.Count)

[<Fact>]
let ``Alt+F6 offers the other panel's directory and the name of the entry under the cursor`` () =
    let fs = mockFs ()
    let mutable offered = ""

    let asking suggestion _ =
        offered <- suggestion
        None

    use window = windowWith fs asking silent
    (leftList window).SelectedItem <- 2

    pressKey window KeyMap.linkKey

    Assert.Equal(at "backup/notes.txt", offered)

[<Fact>]
let ``the popup is told which kinds the place typed cannot carry`` () =
    let fs = mockFs ()
    let mutable refusalOf = fun (_: string) (_: LinkKind) -> None

    let asking _ refusal =
        refusalOf <- refusal
        None

    use window = windowWith fs asking silent
    (leftList window).SelectedItem <- 1

    pressKey window KeyMap.linkKey

    Assert.Equal(Some HardLinkToDirectory, refusalOf (at "backup/docs") Hard)
    Assert.Equal(None, refusalOf (at "backup/docs") Symbolic)

[<Fact>]
let ``a kind the entry cannot carry is refused with a sentence`` () =
    let fs = mockFs ()
    let reported = ResizeArray<string>()
    use window = windowWith fs (answering (at "backup/docs") Hard) (fun _ -> reported.Add)
    (leftList window).SelectedItem <- 1

    pressKey window KeyMap.linkKey

    Assert.Contains(LinkRefusalMessage.describe HardLinkToDirectory, Assert.Single reported)
    Assert.False(fs.Directory.Exists(at "backup/docs"))

[<Fact>]
let ``a cancelled link popup leaves the disk alone`` () =
    let fs = mockFs ()
    use window = windowWith fs (fun _ _ -> None) silent
    (leftList window).SelectedItem <- 2

    pressKey window KeyMap.linkKey

    Assert.Empty(fs.Directory.GetFileSystemEntries(at "backup"))
