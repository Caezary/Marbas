module Marbas.Tests.Integration.App.ClipboardKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Marbas.Domain
open Marbas.Ui.Keys
open Marbas.Ui.Messages
open Marbas.Ui.Panels
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

[<Fact>]
let ``Alt+Shift+Ins puts the full path under the cursor on the clipboard`` () =
    let handed = ResizeArray<string list>()

    use window =
        focusedWindow
            { Fakes.dependencies (mockFs ()) (at "start") with
                Clipboard.SetText = taking handed }

    pressKey window Key.CursorDown
    pressKey window KeyMap.copyPathsKey

    Assert.Equal<string list list>([ [ at "start/docs" ] ], List.ofSeq handed)

[<Fact>]
let ``Alt+P puts the full path under the cursor on the clipboard`` () =
    let handed = ResizeArray<string list>()

    use window =
        focusedWindow
            { Fakes.dependencies (mockFs ()) (at "start") with
                Clipboard.SetText = taking handed }

    pressKey window Key.CursorDown
    pressKey window KeyMap.spareCopyPathsKey

    Assert.Equal<string list list>([ [ at "start/docs" ] ], List.ofSeq handed)

let private linkedFs () =
    let fs = mockFs ()
    fs.AddDirectory(at "work")
    fs.Directory.CreateSymbolicLink(at "start/place", at "work") |> ignore
    fs

[<Fact>]
let ``Ctrl+Alt+Ins puts the place the link under the cursor leads to on the clipboard`` () =
    let handed = ResizeArray<string list>()

    use window =
        focusedWindow
            { Fakes.dependencies (linkedFs ()) (at "start") with
                Clipboard.SetText = taking handed }

    pressKey window Key.CursorDown
    pressKey window Key.CursorDown
    pressKey window KeyMap.copyLinkTargetsKey

    Assert.Equal<string list list>([ [ at "work" ] ], List.ofSeq handed)

[<Fact>]
let ``Alt+L puts the place the link under the cursor leads to on the clipboard`` () =
    let handed = ResizeArray<string list>()

    use window =
        focusedWindow
            { Fakes.dependencies (linkedFs ()) (at "start") with
                Clipboard.SetText = taking handed }

    pressKey window Key.CursorDown
    pressKey window Key.CursorDown
    pressKey window KeyMap.spareCopyLinkTargetsKey

    Assert.Equal<string list list>([ [ at "work" ] ], List.ofSeq handed)

let private holding fs (text: string) =
    { Fakes.dependencies fs (at "start") with
        Clipboard.GetText = fun () -> Ok(Some text) }

[<Fact>]
let ``Ctrl+Shift+Ins sends the active panel into the directory on the clipboard`` () =
    use window = focusedWindow (holding (mockFs ()) (at "start/docs"))

    pressKey window KeyMap.clipboardPathKey

    Assert.Equal(at "start/docs", shownPath FilePanel.LeftId window)

[<Fact>]
let ``Alt+J sends the active panel beside the file on the clipboard`` () =
    use window = focusedWindow (holding (mockFs ()) ("\"" + at "start/notes.txt" + "\""))

    pressKey window KeyMap.spareClipboardPathKey

    Assert.Equal(at "start", shownPath FilePanel.LeftId window)
    Assert.Equal(System.Nullable 2, (leftList window).SelectedItem)

[<Theory>]
[<InlineData("start/gone")>]
[<InlineData("start/docs\nstart")>]
let ``a clipboard that names no single existing place leaves the panel and says nothing`` (text: string) =
    let reported = ResizeArray<string>()

    use window =
        focusedWindow
            { holding (mockFs ()) (at text) with
                Prompts.ReportError = fun report -> reported.Add report.Title }

    pressKey window KeyMap.spareClipboardPathKey

    Assert.Equal(at "start", shownPath FilePanel.LeftId window)
    Assert.Empty reported
