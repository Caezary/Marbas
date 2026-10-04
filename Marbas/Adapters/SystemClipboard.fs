module Marbas.Adapters.SystemClipboard

open System
open System.Runtime.InteropServices
open System.Text
open System.Threading
open FsToolkit.ErrorHandling
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.App

module private User32 =

    [<DllImport("user32.dll", SetLastError = true)>]
    extern bool OpenClipboard(nativeint owner)

    [<DllImport("user32.dll", SetLastError = true)>]
    extern bool CloseClipboard()

    [<DllImport("user32.dll", SetLastError = true)>]
    extern bool EmptyClipboard()

    [<DllImport("user32.dll", SetLastError = true)>]
    extern nativeint SetClipboardData(uint32 format, nativeint memory)

    [<DllImport("user32.dll", SetLastError = true)>]
    extern bool IsClipboardFormatAvailable(uint32 format)

    [<DllImport("user32.dll", SetLastError = true)>]
    extern nativeint GetClipboardData(uint32 format)

    [<DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "RegisterClipboardFormatW")>]
    extern uint32 RegisterClipboardFormat(string name)

module private Shell32 =

    [<DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "DragQueryFileW")>]
    extern uint32 DragQueryFile(nativeint drop, uint32 index, StringBuilder file, uint32 length)

module private Kernel32 =

    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern nativeint GlobalAlloc(uint32 flags, unativeint bytes)

    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern nativeint GlobalLock(nativeint memory)

    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern bool GlobalUnlock(nativeint memory)

    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern nativeint GlobalFree(nativeint memory)

[<Literal>]
let private UnicodeText = 13u

[<Literal>]
let private DroppedFiles = 15u

[<Literal>]
let private PreferredDropEffect = "Preferred DropEffect"

[<Literal>]
let private DropEffectCopy = 1u

[<Literal>]
let private DropEffectMove = 2u

[<Literal>]
let private OffsetOfNames = 20

[<Literal>]
let private NoDropPoint = 0

[<Literal>]
let private InClientArea = 0

[<Literal>]
let private WideNames = 1

[<Literal>]
let private EveryDroppedFile = 0xFFFFFFFFu

[<Literal>]
let private Moveable = 0x0002u

[<Literal>]
let private OpenAttempts = 10

[<Literal>]
let private OpenPauseMilliseconds = 10

let private lastFailure () =
    Marshal.GetLastPInvokeError()
    |> Marshal.GetPInvokeErrorMessage
    |> ClipboardFailed
    |> Error

let private freedAfter failure (memory: nativeint) =
    Kernel32.GlobalFree memory |> ignore
    failure

let rec private opened attemptsLeft =
    match User32.OpenClipboard 0n, attemptsLeft with
    | true, _ -> Ok()
    | false, 1 -> lastFailure ()
    | false, _ ->
        Thread.Sleep OpenPauseMilliseconds
        opened (attemptsLeft - 1)

let private filled (bytes: byte array) (memory: nativeint) =
    match Kernel32.GlobalLock memory with
    | 0n -> freedAfter (lastFailure ()) memory
    | locked ->
        Marshal.Copy(bytes, 0, locked, bytes.Length)
        Kernel32.GlobalUnlock memory |> ignore
        Ok memory

let private memoryHolding (bytes: byte array) =
    match Kernel32.GlobalAlloc(Moveable, unativeint bytes.Length) with
    | 0n -> lastFailure ()
    | memory -> filled bytes memory

let private handedOver (format: uint32, bytes: byte array) =
    memoryHolding bytes
    |> Result.bind (fun memory ->
        match User32.SetClipboardData(format, memory) with
        | 0n -> freedAfter (lastFailure ()) memory
        | _ -> Ok())

let private replacedBy blocks =
    if User32.EmptyClipboard() then
        blocks |> List.traverseResultM handedOver |> Result.map ignore
    else
        lastFailure ()

let private whileOpen action () =
    opened OpenAttempts
    |> Result.bind (fun () ->
        try
            action ()
        finally
            User32.CloseClipboard() |> ignore)

let private settingOnWindows blocks =
    whileOpen (fun () -> replacedBy blocks) ()

let private nullTerminated (text: string) = Encoding.Unicode.GetBytes(text + "\000")

let private setOnWindows (text: ClipboardText) =
    settingOnWindows [ UnicodeText, nullTerminated (ClipboardText.joined Environment.NewLine text) ]

let private dropFilesHeader =
    [ OffsetOfNames; NoDropPoint; NoDropPoint; InClientArea; WideNames ]
    |> List.map BitConverter.GetBytes
    |> Array.concat

let private dropFiles (paths: FileSystemPath list) =
    let names = paths |> List.map (FileSystemPath.value >> nullTerminated)

    Array.concat (dropFilesHeader :: names @ [ nullTerminated "" ])

let private dropEffect intent =
    match intent with
    | ForCopying -> DropEffectCopy
    | ForMoving -> DropEffectMove

let private setFilesOnWindows (files: ClipboardFiles) =
    match User32.RegisterClipboardFormat PreferredDropEffect with
    | 0u -> lastFailure ()
    | effectFormat ->
        settingOnWindows
            [ DroppedFiles, dropFiles (ClipboardFiles.paths files)
              effectFormat, BitConverter.GetBytes(dropEffect (ClipboardFiles.intent files)) ]

let private textHeld (memory: nativeint) =
    match Kernel32.GlobalLock memory with
    | 0n -> lastFailure ()
    | locked ->
        let text = Marshal.PtrToStringUni locked
        Kernel32.GlobalUnlock memory |> ignore
        Ok(Some text)

let private heldText () =
    if User32.IsClipboardFormatAvailable UnicodeText then
        match User32.GetClipboardData UnicodeText with
        | 0n -> lastFailure ()
        | memory -> textHeld memory
    else
        Ok None

let private droppedFile drop (index: uint32) =
    let length = Shell32.DragQueryFile(drop, index, null, 0u)
    let name = StringBuilder(int length + 1)
    Shell32.DragQueryFile(drop, index, name, length + 1u) |> ignore
    name.ToString() |> FileSystemPath.create

let private droppedFiles drop =
    let count = Shell32.DragQueryFile(drop, EveryDroppedFile, null, 0u)
    List.init (int count) (uint32 >> droppedFile drop)

let private effectHeld (memory: nativeint) =
    match Kernel32.GlobalLock memory with
    | 0n -> None
    | locked ->
        let effect = Marshal.ReadInt32 locked |> uint32
        Kernel32.GlobalUnlock memory |> ignore
        Some effect

let private heldEffect () =
    match User32.RegisterClipboardFormat PreferredDropEffect with
    | 0u -> None
    | format when User32.IsClipboardFormatAvailable format ->
        match User32.GetClipboardData format with
        | 0n -> None
        | memory -> effectHeld memory
    | _ -> None

let private intentOf effect =
    match effect with
    | Some held when held &&& DropEffectMove <> 0u && held &&& DropEffectCopy = 0u -> ForMoving
    | _ -> ForCopying

let private heldFiles () =
    if User32.IsClipboardFormatAvailable DroppedFiles then
        match User32.GetClipboardData DroppedFiles with
        | 0n -> lastFailure ()
        | drop -> droppedFiles drop |> ClipboardFiles.tryCreate (intentOf (heldEffect ())) |> Ok
    else
        Ok None

let private emptied () =
    if User32.EmptyClipboard() then Ok() else lastFailure ()

let private unavailable _ = Error ClipboardUnavailable

let forPlatform platform : Ports.Clipboard =
    match platform with
    | Windows ->
        { SetText = setOnWindows
          GetText = whileOpen heldText
          SetFiles = setFilesOnWindows
          GetFiles = whileOpen heldFiles
          Empty = whileOpen emptied }
    | Unix ->
        { SetText = unavailable
          GetText = unavailable
          SetFiles = unavailable
          GetFiles = unavailable
          Empty = unavailable }
