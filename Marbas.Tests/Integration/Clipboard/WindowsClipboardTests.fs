module Marbas.Tests.Integration.Clipboard.WindowsClipboardTests

open System
open System.Collections.Concurrent
open System.IO
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Adapters
open Marbas.Tests
open Marbas.Tests.Integration.Disk

[<Literal>]
let private Win32 = "The adapter speaks the Win32 clipboard"

[<Literal>]
let private AccessDenied = 5

[<Literal>]
let private HeldMilliseconds = 300

[<Literal>]
let private UnansweredOwnerMilliseconds = 5000L

let private clipboard = SystemClipboard.forPlatform Windows

let private text lines =
    ClipboardText.tryCreate lines |> Expect.some

let private files intent (paths: string list) =
    paths
    |> List.map FileSystemPath.create
    |> ClipboardFiles.tryCreate intent
    |> Expect.some

let private withFiles () =
    let scratch = new Scratch()
    scratch.Directory "from/docs"
    scratch.File "from/docs/inner.txt" "inner"
    scratch.File "from/zażółć gęślą.txt" "notes"
    scratch.Directory "to"
    scratch

let private namesIn (directory: string) =
    Directory.GetFileSystemEntries directory
    |> Array.map Path.GetFileName
    |> Array.sort
    |> List.ofArray

[<WindowsFact(Win32)>]
let ``text put by Marbas is read by another program with the Windows break between the lines`` () =
    use _kept = new KeptClipboard()
    clipboard.SetText(text [ "zażółć gęślą.txt"; "docs" ]) |> Expect.ok

    let read = OtherProgram.text ()

    Assert.Equal("zażółć gęślą.txt\r\ndocs", read)

[<WindowsFact(Win32)>]
let ``text put by another program is read by Marbas whole`` () =
    use _kept = new KeptClipboard()
    OtherProgram.putText "C:\\work\\zażółć gęślą.txt\r\nsecond line"

    let read = clipboard.GetText()

    Assert.Equal(Ok(Some "C:\\work\\zażółć gęślą.txt\r\nsecond line"), read)

[<WindowsFact(Win32)>]
let ``text put by Marbas is read back by Marbas`` () =
    use _kept = new KeptClipboard()
    clipboard.SetText(text [ "notes.txt"; "readme.txt" ]) |> Expect.ok

    let read = clipboard.GetText()

    Assert.Equal(Ok(Some "notes.txt\r\nreadme.txt"), read)

[<WindowsFact(Win32)>]
let ``a clipboard emptied by Marbas holds no text for another program`` () =
    use _kept = new KeptClipboard()
    clipboard.SetText(text [ "notes.txt" ]) |> Expect.ok
    clipboard.Empty() |> Expect.ok

    let read = OtherProgram.text ()

    Assert.Equal("", read)

[<WindowsFact(Win32)>]
let ``an emptied clipboard gives Marbas no text`` () =
    use _kept = new KeptClipboard()
    clipboard.SetText(text [ "notes.txt" ]) |> Expect.ok
    clipboard.Empty() |> Expect.ok

    let read = clipboard.GetText()

    Assert.Equal(Ok None, read)

[<WindowsFact(Win32)>]
let ``files put by Marbas replace the text on the clipboard`` () =
    use _kept = new KeptClipboard()
    clipboard.SetText(text [ "notes.txt" ]) |> Expect.ok
    clipboard.SetFiles(files ForCopying [ "C:\\work\\notes.txt" ]) |> Expect.ok

    let read = clipboard.GetText()

    Assert.Equal(Ok None, read)

[<WindowsFact(Win32)>]
let ``files put by Marbas are read by another program in their order`` () =
    use _kept = new KeptClipboard()
    use scratch = withFiles ()
    let paths = [ scratch.At "from/zażółć gęślą.txt"; scratch.At "from/docs" ]
    clipboard.SetFiles(files ForCopying paths) |> Expect.ok

    let read = OtherProgram.files ()

    Assert.Equal<string list>(paths, read)

[<WindowsFact(Win32)>]
let ``files put by Marbas to be copied carry the copy effect`` () =
    use _kept = new KeptClipboard()
    clipboard.SetFiles(files ForCopying [ "C:\\work\\notes.txt" ]) |> Expect.ok

    let effect = OtherProgram.dropEffect ()

    Assert.Equal(Some 1u, effect)

[<WindowsFact(Win32)>]
let ``files put by Marbas to be moved carry the move effect`` () =
    use _kept = new KeptClipboard()
    clipboard.SetFiles(files ForMoving [ "C:\\work\\notes.txt" ]) |> Expect.ok

    let effect = OtherProgram.dropEffect ()

    Assert.Equal(Some 2u, effect)

[<WindowsFact(Win32)>]
let ``files put by Marbas are read back by Marbas with their intent`` () =
    use _kept = new KeptClipboard()
    let put = files ForMoving [ "C:\\work\\zażółć gęślą.txt"; "C:\\work\\docs" ]
    clipboard.SetFiles put |> Expect.ok

    let read = clipboard.GetFiles()

    Assert.Equal(Ok(Some put), read)

[<WindowsFact(Win32)>]
let ``a path longer than the classic limit is read back whole`` () =
    use _kept = new KeptClipboard()
    let put = files ForCopying [ "C:\\work\\" + String.replicate 300 "a" + ".txt" ]
    clipboard.SetFiles put |> Expect.ok

    let read = clipboard.GetFiles()

    Assert.Equal(Ok(Some put), read)

[<WindowsTheory(Win32)>]
[<InlineData(1u)>]
[<InlineData(3u)>]
[<InlineData(5u)>]
let ``files offered by another program with any effect but a move alone are read as to be copied`` (effect: uint32) =
    use _kept = new KeptClipboard()
    OtherProgram.putFilesWithEffect [ "C:\\work\\zażółć gęślą.txt"; "C:\\work\\docs" ] effect

    let read = clipboard.GetFiles()

    Assert.Equal(Ok(Some(files ForCopying [ "C:\\work\\zażółć gęślą.txt"; "C:\\work\\docs" ])), read)

[<WindowsTheory(Win32)>]
[<InlineData(2u)>]
[<InlineData(6u)>]
let ``files offered by another program with the move effect and no copy are read as to be moved`` (effect: uint32) =
    use _kept = new KeptClipboard()
    OtherProgram.putFilesWithEffect [ "C:\\work\\notes.txt" ] effect

    let read = clipboard.GetFiles()

    Assert.Equal(Ok(Some(files ForMoving [ "C:\\work\\notes.txt" ])), read)

[<WindowsFact(Win32)>]
let ``files offered by another program with no effect are read as to be copied`` () =
    use _kept = new KeptClipboard()
    OtherProgram.putFiles [ "C:\\work\\notes.txt" ]

    let read = clipboard.GetFiles()

    Assert.Equal(Ok(Some(files ForCopying [ "C:\\work\\notes.txt" ])), read)

[<WindowsFact(Win32)>]
let ``text alone on the clipboard gives Marbas no files`` () =
    use _kept = new KeptClipboard()
    OtherProgram.putText "C:\\work\\notes.txt"

    let read = clipboard.GetFiles()

    Assert.Equal(Ok None, read)

[<WindowsFact(Win32)>]
let ``a clipboard emptied by Marbas holds no files for another program`` () =
    use _kept = new KeptClipboard()
    clipboard.SetFiles(files ForMoving [ "C:\\work\\notes.txt" ]) |> Expect.ok
    clipboard.Empty() |> Expect.ok

    let read = OtherProgram.files ()

    Assert.Empty read

[<WindowsFact(Win32)>]
let ``files put by Marbas to be copied are copied by the paste of the system shell`` () =
    use _kept = new KeptClipboard()
    use scratch = withFiles ()
    let paths = [ scratch.At "from/zażółć gęślą.txt"; scratch.At "from/docs" ]
    clipboard.SetFiles(files ForCopying paths) |> Expect.ok

    OtherProgram.pasteInto (scratch.At "to") (scratch.At "to/docs/inner.txt")

    Assert.Equal<string list>([ "docs"; "zażółć gęślą.txt" ], namesIn (scratch.At "to"))
    Assert.Equal<string list>([ "docs"; "zażółć gęślą.txt" ], namesIn (scratch.At "from"))
    Assert.Equal("inner", File.ReadAllText(scratch.At "to/docs/inner.txt"))

[<WindowsFact(Win32)>]
let ``files put by Marbas to be moved are moved by the paste of the system shell`` () =
    use _kept = new KeptClipboard()
    use scratch = withFiles ()
    let paths = [ scratch.At "from/zażółć gęślą.txt"; scratch.At "from/docs" ]
    clipboard.SetFiles(files ForMoving paths) |> Expect.ok

    OtherProgram.pasteInto (scratch.At "to") (scratch.At "to/docs/inner.txt")

    Assert.Equal<string list>([ "docs"; "zażółć gęślą.txt" ], namesIn (scratch.At "to"))
    Assert.Empty(namesIn (scratch.At "from"))
    Assert.Equal("inner", File.ReadAllText(scratch.At "to/docs/inner.txt"))

[<WindowsFact(Win32)>]
let ``a clipboard held open by another program refuses to be written, with the reason the system gives`` () =
    use _kept = new KeptClipboard()
    use _held = OtherProgram.holdingClipboard ()

    let answer = clipboard.SetText(text [ "notes.txt" ])

    Assert.Equal(Error(ClipboardFailed(Marshal.GetPInvokeErrorMessage AccessDenied)), answer)

[<WindowsFact(Win32)>]
let ``a clipboard held open by another program refuses to be read, with the reason the system gives`` () =
    use _kept = new KeptClipboard()
    use _held = OtherProgram.holdingClipboard ()

    let answer = clipboard.GetFiles()

    Assert.Equal(Error(ClipboardFailed(Marshal.GetPInvokeErrorMessage AccessDenied)), answer)

[<WindowsFact(Win32)>]
let ``a clipboard a program opened with no window is opened all the same by another that brings none`` () =
    use _kept = new KeptClipboard()
    use _held = OtherProgram.holdingClipboardWithNoWindow ()

    let answer = OtherProgram.openingWithNoWindow ()

    Assert.Equal(Ok(), answer)

[<WindowsFact(Win32)>]
let ``a clipboard a program opened with no window is refused to another that brings one`` () =
    use _kept = new KeptClipboard()
    use _held = OtherProgram.holdingClipboardWithNoWindow ()

    let answer = OtherProgram.opening ()

    Assert.Equal(Error AccessDenied, answer)

[<WindowsFact(Win32)>]
let ``a clipboard a program opened with a window is refused to another that brings none`` () =
    use _kept = new KeptClipboard()
    use _held = OtherProgram.holdingClipboard ()

    let answer = OtherProgram.openingWithNoWindow ()

    Assert.Equal(Error AccessDenied, answer)

[<WindowsFact(Win32)>]
let ``while Marbas holds the clipboard open a program that brings no window is refused it`` () =
    use _kept = new KeptClipboard()

    let answer = SystemClipboard.whileOpen (OtherProgram.openingWithNoWindow >> Ok) ()

    Assert.Equal(Ok(Error AccessDenied), answer)

[<WindowsFact(Win32)>]
let ``while Marbas holds the clipboard open a program that brings a window is refused it`` () =
    use _kept = new KeptClipboard()

    let answer = SystemClipboard.whileOpen (OtherProgram.opening >> Ok) ()

    Assert.Equal(Ok(Error AccessDenied), answer)

[<WindowsFact(Win32)>]
let ``once Marbas has closed the clipboard another program opens it`` () =
    use _kept = new KeptClipboard()
    SystemClipboard.whileOpen Ok () |> Expect.ok

    let answer = OtherProgram.opening ()

    Assert.Equal(Ok(), answer)

[<WindowsFact(Win32)>]
let ``Marbas owns the clipboard it has written`` () =
    use _kept = new KeptClipboard()
    clipboard.SetText(text [ "notes.txt" ]) |> Expect.ok

    let owner = OtherProgram.ownerProcess ()

    Assert.Equal(Environment.ProcessId, owner)

[<WindowsFact(Win32)>]
let ``another program replaces what Marbas owns without waiting for Marbas to answer`` () =
    use _kept = new KeptClipboard()
    clipboard.SetText(text [ "notes.txt" ]) |> Expect.ok

    let waited = OtherProgram.millisecondsPuttingText "from another program"

    Assert.InRange(waited, 0L, UnansweredOwnerMilliseconds / 2L)

[<WindowsFact(Win32)>]
let ``a second request of Marbas waits until the first has closed the clipboard`` () =
    use _kept = new KeptClipboard()
    let finished = ConcurrentQueue<string>()

    let readingBehindTheFirst () =
        clipboard.GetText() |> ignore
        finished.Enqueue "second"

    let first () =
        let second = Task.Run(Action readingBehindTheFirst)
        Thread.Sleep HeldMilliseconds
        finished.Enqueue "first"
        Ok second

    let waited = SystemClipboard.whileOpen first () |> Result.map _.Wait()

    Assert.Equal(Ok(), waited)
    Assert.Equal<string list>([ "first"; "second" ], List.ofSeq finished)
