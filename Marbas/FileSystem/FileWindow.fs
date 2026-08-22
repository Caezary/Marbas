module Marbas.FileSystem.FileWindow

open System
open System.IO
open System.IO.Abstractions

type Source(path: FileSystemPath, stream: Stream, encoding: TextEncoding) =

    member _.Path = path

    member _.Encoding = encoding

    member _.Length = stream.Length

    member _.Read(offset: int64, count: int) =
        let start = max 0L offset
        let length = int (min (int64 (max 0 count)) (max 0L (stream.Length - start)))

        if length = 0 then
            Array.empty
        else
            let bytes = Array.zeroCreate length
            stream.Seek(start, SeekOrigin.Begin) |> ignore
            stream.ReadExactly(bytes, 0, length)
            bytes

    interface IDisposable with
        member _.Dispose() = stream.Dispose()

let private probeOf (stream: Stream) =
    let length = int (min stream.Length (int64 TextEncoding.ProbeLength))
    let probe = Array.zeroCreate length
    stream.ReadExactly(probe, 0, length)
    probe

let openFile (fs: IFileSystem) path : Result<Source, ContentError> =
    match FileSystemPath.kind fs path with
    | FileEntry ->
        try
            let stream =
                fs.File.Open(FileSystemPath.value path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)

            Ok(new Source(path, stream, TextEncoding.detect (probeOf stream)))
        with ex ->
            Error(ContentError.mapException path ex)
    | _ -> Error(FileNotFound path)
