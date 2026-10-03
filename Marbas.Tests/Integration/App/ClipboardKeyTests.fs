module Marbas.Tests.Integration.App.ClipboardKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Marbas.Domain
open Marbas.Ui.Keys
open Marbas.Ui.Messages
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs

let private taking (handed: ResizeArray<string list>) =
    fun text ->
        handed.Add(ClipboardText.value text)
        Ok()

[<Fact>]
let ``Ctrl+Ins puts the name under the cursor on the clipboard`` () =
    let handed = ResizeArray<string list>()

    use window =
        focusedWindow
            { Fakes.dependencies (mockFs ()) (at "start") with
                Clipboard.SetText = taking handed }

    pressKey window Key.CursorDown
    pressKey window KeyMap.copyNamesKey

    Assert.Equal<string list list>([ [ "docs" ] ], List.ofSeq handed)

[<Fact>]
let ``Alt+C puts the name under the cursor on the clipboard`` () =
    let handed = ResizeArray<string list>()

    use window =
        focusedWindow
            { Fakes.dependencies (mockFs ()) (at "start") with
                Clipboard.SetText = taking handed }

    pressKey window Key.CursorDown
    pressKey window KeyMap.spareCopyNamesKey

    Assert.Equal<string list list>([ [ "docs" ] ], List.ofSeq handed)

[<Fact>]
let ``a clipboard the system does not offer is reported once, however often the key is pressed`` () =
    let reported = ResizeArray<string>()

    use window =
        focusedWindow
            { Fakes.dependencies (mockFs ()) (at "start") with
                Clipboard.SetText = fun _ -> Error ClipboardUnavailable
                Prompts.ReportError = fun report -> reported.Add report.Title }

    pressKey window Key.CursorDown
    pressKey window KeyMap.spareCopyNamesKey
    pressKey window KeyMap.spareCopyNamesKey

    Assert.Equal<string list>([ ClipboardErrorMessage.Title ], List.ofSeq reported)
