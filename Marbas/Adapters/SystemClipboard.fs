module Marbas.Adapters.SystemClipboard

open System
open System.Runtime.InteropServices
open System.Threading
open Marbas.Domain
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

let private filled (text: string) (memory: nativeint) =
    match Kernel32.GlobalLock memory with
    | 0n -> freedAfter (lastFailure ()) memory
    | locked ->
        Marshal.Copy(text.ToCharArray(), 0, locked, text.Length)
        Marshal.WriteInt16(locked, text.Length * sizeof<char>, 0s)
        Kernel32.GlobalUnlock memory |> ignore
        Ok memory

let private memoryHolding (text: string) =
    match Kernel32.GlobalAlloc(Moveable, unativeint ((text.Length + 1) * sizeof<char>)) with
    | 0n -> lastFailure ()
    | memory -> filled text memory

let private handedOver (memory: nativeint) =
    match User32.SetClipboardData(UnicodeText, memory) with
    | 0n -> freedAfter (lastFailure ()) memory
    | _ -> Ok()

let private replaced (text: string) =
    if User32.EmptyClipboard() then
        memoryHolding text |> Result.bind handedOver
    else
        lastFailure ()

let private setOnWindows (text: ClipboardText) =
    opened OpenAttempts
    |> Result.bind (fun () ->
        try
            replaced (ClipboardText.joined Environment.NewLine text)
        finally
            User32.CloseClipboard() |> ignore)

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

let private getOnWindows () =
    opened OpenAttempts
    |> Result.bind (fun () ->
        try
            heldText ()
        finally
            User32.CloseClipboard() |> ignore)

let private unavailable _ = Error ClipboardUnavailable

let forPlatform platform : Ports.Clipboard =
    match platform with
    | Windows ->
        { SetText = setOnWindows
          GetText = getOnWindows }
    | Unix ->
        { SetText = unavailable
          GetText = unavailable }
