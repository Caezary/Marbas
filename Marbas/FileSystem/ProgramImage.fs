namespace Marbas.FileSystem

open System
open System.IO
open System.IO.Abstractions
open FsToolkit.ErrorHandling

type ImageSubsystem =
    | WindowSubsystem
    | ConsoleSubsystem
    | OtherSubsystem
    | NotAnImage
    | UnreadableImage

module ProgramImage =

    [<Literal>]
    let private HeaderPointerAt = 0x3CL

    [<Literal>]
    let private SubsystemAfterSignature = 92L

    [<Literal>]
    let private WindowsGui = 2us

    [<Literal>]
    let private WindowsConsole = 3us

    let private dosSignature = "MZ"B

    let private peSignature = "PE\000\000"B

    let private bytesAt (stream: Stream) (offset: int64) (length: int) =
        if offset < 0L || offset + int64 length > stream.Length then
            None
        else
            let buffer = Array.zeroCreate<byte> length
            stream.Position <- offset
            stream.ReadExactly buffer
            Some buffer

    let private signatureAt (stream: Stream) (offset: int64) (signature: byte array) =
        bytesAt stream offset signature.Length
        |> Option.filter ((=) signature)
        |> Option.map ignore

    let private ofNumber (number: uint16) =
        match number with
        | WindowsGui -> WindowSubsystem
        | WindowsConsole -> ConsoleSubsystem
        | _ -> OtherSubsystem

    let private headerSubsystem (stream: Stream) =
        option {
            do! signatureAt stream 0L dosSignature
            let! pointer = bytesAt stream HeaderPointerAt 4
            let header = int64 (BitConverter.ToInt32(pointer, 0))
            do! signatureAt stream header peSignature
            let! number = bytesAt stream (header + SubsystemAfterSignature) 2
            return ofNumber (BitConverter.ToUInt16(number, 0))
        }
        |> Option.defaultValue NotAnImage

    let subsystem (fs: IFileSystem) (path: FileSystemPath) =
        try
            use stream = fs.File.OpenRead(FileSystemPath.value path)
            headerSubsystem stream
        with
        | :? IOException
        | :? UnauthorizedAccessException -> UnreadableImage
