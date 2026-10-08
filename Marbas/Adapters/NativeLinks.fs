module Marbas.Adapters.NativeLinks

open System
open System.IO
open System.Runtime.InteropServices
open System.Text
open Microsoft.Win32.SafeHandles
open Marbas.Domain
open Marbas.FileSystem

module private Kernel32 =

    [<DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)>]
    extern bool CreateHardLinkW(string fileName, string existingFileName, nativeint securityAttributes)

    [<DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)>]
    extern SafeFileHandle CreateFileW(
        string fileName,
        uint32 desiredAccess,
        uint32 shareMode,
        nativeint securityAttributes,
        uint32 creationDisposition,
        uint32 flagsAndAttributes,
        nativeint templateFile
    )

    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern bool DeviceIoControl(
        SafeFileHandle device,
        uint32 controlCode,
        byte[] inBuffer,
        int inBufferSize,
        nativeint outBuffer,
        int outBufferSize,
        int& bytesReturned,
        nativeint overlapped
    )

module private Libc =

    [<DllImport("libc", SetLastError = true)>]
    extern int link(string existingPath, string newPath)

[<Literal>]
let private GenericWrite = 0x40000000u

[<Literal>]
let private OpenExisting = 3u

[<Literal>]
let private OpenReparsePoint = 0x00200000u

[<Literal>]
let private BackupSemantics = 0x02000000u

[<Literal>]
let private SetReparsePoint = 0x000900A4u

[<Literal>]
let private MountPointTag = 0xA0000003u

[<Literal>]
let private ReparseHeaderLength = 8

[<Literal>]
let private WideNullLength = 2

[<Literal>]
let private NotPermitted = 1

[<Literal>]
let private PermissionDenied = 13

let private windowsFailure () =
    let code = Marshal.GetLastPInvokeError()
    raise (Marshal.GetExceptionForHR(int (0x80070000u ||| uint32 code)))

let private unixFailure () =
    let code = Marshal.GetLastPInvokeError()
    let message = Marshal.GetPInvokeErrorMessage code

    match code with
    | NotPermitted
    | PermissionDenied -> raise (UnauthorizedAccessException message)
    | _ -> raise (IOException message)

let private hardOnWindows (target: FileSystemPath) (location: FileSystemPath) =
    if not (Kernel32.CreateHardLinkW(FileSystemPath.value location, FileSystemPath.value target, 0n)) then
        windowsFailure ()

let private hardOnUnix (target: FileSystemPath) (location: FileSystemPath) =
    if Libc.link (FileSystemPath.value target, FileSystemPath.value location) <> 0 then
        unixFailure ()

let private mountPoint (target: string) =
    let substitute = Encoding.Unicode.GetBytes(@"\??\" + target)
    let print = Encoding.Unicode.GetBytes target
    let pathsLength = substitute.Length + WideNullLength + print.Length + WideNullLength

    use stream = new MemoryStream()
    use writer = new BinaryWriter(stream)
    writer.Write MountPointTag
    writer.Write(uint16 (ReparseHeaderLength + pathsLength))
    writer.Write 0us
    writer.Write 0us
    writer.Write(uint16 substitute.Length)
    writer.Write(uint16 (substitute.Length + WideNullLength))
    writer.Write(uint16 print.Length)
    writer.Write substitute
    writer.Write 0us
    writer.Write print
    writer.Write 0us
    writer.Flush()
    stream.ToArray()

let private pointAt (location: string) (target: string) =
    use handle =
        Kernel32.CreateFileW(location, GenericWrite, 0u, 0n, OpenExisting, OpenReparsePoint ||| BackupSemantics, 0n)

    if handle.IsInvalid then
        windowsFailure ()

    let buffer = mountPoint target
    let mutable returned = 0

    if not (Kernel32.DeviceIoControl(handle, SetReparsePoint, buffer, buffer.Length, 0n, 0, &returned, 0n)) then
        windowsFailure ()

let private junction (target: FileSystemPath) (location: FileSystemPath) =
    let raw = FileSystemPath.value location
    Directory.CreateDirectory raw |> ignore

    try
        pointAt raw (FileSystemPath.value target)
    with _ ->
        Directory.Delete raw
        reraise ()

let forPlatform platform =
    match platform with
    | Windows ->
        { MakeHard = hardOnWindows
          MakeJunction = Some junction }
    | Unix ->
        { MakeHard = hardOnUnix
          MakeJunction = None }
