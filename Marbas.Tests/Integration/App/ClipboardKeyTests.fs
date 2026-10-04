module Marbas.Tests.Integration.App.ClipboardKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Marbas.Domain
open Marbas.FileSystem
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

[<Fact>]
let ``Alt+J opens the archive the path on the clipboard runs into, at the entry it names`` () =
    let fs = Marbas.Tests.State.ArchiveFixture.packed ()

    use window = focusedWindow (holding fs (at "start/backup.zip/docs/inner.txt"))

    pressKey window KeyMap.spareClipboardPathKey

    Assert.Equal(at "start/backup.zip/docs", shownPath FilePanel.LeftId window)
    Assert.Equal(System.Nullable 2, (leftList window).SelectedItem)

let private takingFiles (handed: ResizeArray<ClipboardIntent * string list>) =
    fun files ->
        handed.Add(ClipboardFiles.intent files, ClipboardFiles.paths files |> List.map FileSystemPath.value)
        Ok()

let private handingFiles fs handed =
    { Fakes.dependencies fs (at "start") with
        Clipboard.SetFiles = takingFiles handed }

[<Fact>]
let ``Alt+Shift+C puts the directory under the cursor on the clipboard to be copied`` () =
    let handed = ResizeArray<ClipboardIntent * string list>()

    use window = focusedWindow (handingFiles (mockFs ()) handed)

    pressKey window Key.CursorDown
    pressKey window KeyMap.copyFilesKey

    Assert.Equal<(ClipboardIntent * string list) list>([ ForCopying, [ at "start/docs" ] ], List.ofSeq handed)

[<Fact>]
let ``Ctrl+Shift+C puts the file under the cursor on the clipboard to be copied`` () =
    let handed = ResizeArray<ClipboardIntent * string list>()

    use window = focusedWindow (handingFiles (mockFs ()) handed)

    pressKey window Key.End
    pressKey window KeyMap.spareCopyFilesKey

    Assert.Equal<(ClipboardIntent * string list) list>([ ForCopying, [ at "start/notes.txt" ] ], List.ofSeq handed)

[<Fact>]
let ``Alt+Shift+X puts the file under the cursor on the clipboard to be moved`` () =
    let handed = ResizeArray<ClipboardIntent * string list>()

    use window = focusedWindow (handingFiles (mockFs ()) handed)

    pressKey window Key.End
    pressKey window KeyMap.cutFilesKey

    Assert.Equal<(ClipboardIntent * string list) list>([ ForMoving, [ at "start/notes.txt" ] ], List.ofSeq handed)

[<Fact>]
let ``Ctrl+Shift+X puts the marked entries on the clipboard to be moved`` () =
    let handed = ResizeArray<ClipboardIntent * string list>()

    use window = focusedWindow (handingFiles (mockFs ()) handed)

    pressKey window Key.CursorDown
    pressKey window KeyMap.insertKey
    pressKey window KeyMap.insertKey
    pressKey window KeyMap.spareCutFilesKey

    Assert.Equal<(ClipboardIntent * string list) list>(
        [ ForMoving, [ at "start/docs"; at "start/notes.txt" ] ],
        List.ofSeq handed
    )

[<Fact>]
let ``Alt+Shift+C inside an archive puts the entry on the clipboard once it is extracted`` () =
    let fs = Marbas.Tests.State.ArchiveFixture.packed ()
    let handed = ResizeArray<ClipboardIntent * string list>()

    use window = focusedWindow (handingFiles fs handed)

    pressKey window Key.CursorDown
    pressKey window Key.Enter
    pressKey window Key.End
    pressKey window KeyMap.copyFilesKey

    Assert.Equal<(ClipboardIntent * string list) list>(
        [ ForCopying, [ at "temp/Marbas/session/1/notes.txt" ] ],
        List.ofSeq handed
    )

    Assert.Equal("notes", fs.File.ReadAllText(at "temp/Marbas/session/1/notes.txt"))

[<Fact>]
let ``Alt+Shift+X inside an archive is refused and nothing reaches the clipboard`` () =
    let fs = Marbas.Tests.State.ArchiveFixture.packed ()
    let handed = ResizeArray<ClipboardIntent * string list>()
    let reported = ResizeArray<string>()

    use window =
        focusedWindow
            { handingFiles fs handed with
                Prompts.ReportError = fun report -> reported.Add report.Message }

    pressKey window Key.CursorDown
    pressKey window Key.Enter
    pressKey window Key.End
    pressKey window KeyMap.cutFilesKey

    Assert.Empty handed
    Assert.Contains(at "start/backup.zip/notes.txt", Assert.Single reported)

let private filesOnClipboard intent paths =
    ClipboardFiles.tryCreate intent (paths |> List.map (at >> FileSystemPath.create)) |> Expect.some

let private pasting fs files (asked: ResizeArray<string>) (emptied: ResizeArray<unit>) =
    { Fakes.dependencies fs (at "start") with
        Clipboard.GetFiles = fun () -> Ok(Some files)
        Clipboard.Empty =
            fun () ->
                emptied.Add()
                Ok()
        Prompts.AskOverwrite =
            fun prompt ->
                asked.Add(FileSystemPath.value prompt.Target)
                Operations.Yes }

let private bothOnClipboard =
    filesOnClipboard ForCopying [ "work/notes.txt"; "work/fresh.txt" ]

let private withWork (fs: MockFileSystem) =
    fs.AddFile(at "work/notes.txt", MockFileData "newer")
    fs.AddFile(at "work/fresh.txt", MockFileData "fresh")
    fs

[<Fact>]
let ``Alt+Shift+V copies the files on the clipboard into the directory, asking before it replaces one`` () =
    let fs = withWork (mockFs ())
    let asked = ResizeArray<string>()

    use window =
        focusedWindow (pasting fs bothOnClipboard asked (ResizeArray()))

    pressKey window KeyMap.pasteFilesKey

    Assert.Equal<string list>([ at "start/notes.txt" ], List.ofSeq asked)
    Assert.Equal("newer", fs.File.ReadAllText(at "start/notes.txt"))
    Assert.Equal("fresh", fs.File.ReadAllText(at "start/fresh.txt"))
    Assert.True(fs.File.Exists(at "work/fresh.txt"))

[<Fact>]
let ``Ctrl+Shift+V moves the files on the clipboard into the directory and empties the clipboard`` () =
    let fs = withWork (mockFs ())
    let emptied = ResizeArray<unit>()

    use window =
        focusedWindow (pasting fs (filesOnClipboard ForMoving [ "work/fresh.txt" ]) (ResizeArray()) emptied)

    pressKey window KeyMap.sparePasteFilesKey

    Assert.Equal("fresh", fs.File.ReadAllText(at "start/fresh.txt"))
    Assert.False(fs.File.Exists(at "work/fresh.txt"))
    Assert.Single emptied |> ignore

let private heldIn (fs: MockFileSystem) =
    ArchiveListing.read fs (FileSystemPath.create (at "start/backup.zip")) ArchivePath.root
    |> Expect.ok
    |> List.map (_.Name >> EntryName.value)
    |> List.sort

[<Fact>]
let ``Alt+Shift+V inside an archive adds the files on the clipboard to it, asking before it replaces one`` () =
    let fs = withWork (Marbas.Tests.State.ArchiveFixture.packed ())
    let asked = ResizeArray<string>()

    use window =
        focusedWindow (pasting fs bothOnClipboard asked (ResizeArray()))

    pressKey window Key.CursorDown
    pressKey window Key.Enter
    pressKey window KeyMap.pasteFilesKey

    Assert.Equal<string list>([ at "start/backup.zip/notes.txt" ], List.ofSeq asked)
    Assert.Equal<string list>([ "docs"; "fresh.txt"; "notes.txt" ], heldIn fs)
    Assert.True(fs.File.Exists(at "work/fresh.txt"))

[<Fact>]
let ``Ctrl+Shift+V inside an archive moves the files on the clipboard into it`` () =
    let fs = withWork (Marbas.Tests.State.ArchiveFixture.packed ())
    let emptied = ResizeArray<unit>()

    use window =
        focusedWindow (pasting fs (filesOnClipboard ForMoving [ "work/fresh.txt" ]) (ResizeArray()) emptied)

    pressKey window Key.CursorDown
    pressKey window Key.Enter
    pressKey window KeyMap.sparePasteFilesKey

    Assert.Equal<string list>([ "docs"; "fresh.txt"; "notes.txt" ], heldIn fs)
    Assert.False(fs.File.Exists(at "work/fresh.txt"))
    Assert.Single emptied |> ignore

[<Fact>]
let ``a clipboard holding no files pastes nothing and says nothing`` () =
    let fs = withWork (mockFs ())
    let reported = ResizeArray<string>()

    use window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                Prompts.ReportError = fun report -> reported.Add report.Title }

    pressKey window KeyMap.pasteFilesKey

    Assert.False(fs.File.Exists(at "start/fresh.txt"))
    Assert.Empty reported
