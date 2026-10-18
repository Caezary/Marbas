module Marbas.Viewing.FileWindow

open System
open System.IO
open System.IO.Abstractions
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.Archives

type Source(path: FileSystemPath, stream: Stream, encoding: TextEncoding) =

    let available (offset: int64) (count: int) =
        int (min (int64 (max 0 count)) (max 0L (stream.Length - max 0L offset)))

    let fill (offset: int64) (bytes: byte array) length =
        stream.Seek(max 0L offset, SeekOrigin.Begin) |> ignore
        stream.ReadExactly(bytes, 0, length)

    member _.Path = path

    member _.Encoding = encoding

    member _.Length = stream.Length

    member _.Read(offset: int64, count: int) =
        lock stream (fun () ->
            match available offset count with
            | 0 -> Array.empty
            | length ->
                let bytes = Array.zeroCreate length
                fill offset bytes length
                bytes)

    member _.ReadInto(offset: int64, buffer: byte array) =
        lock stream (fun () ->
            let length = available offset buffer.Length

            if length > 0 then
                fill offset buffer length

            length)

    interface IDisposable with
        member _.Dispose() = stream.Dispose()

let private probeOf (stream: Stream) =
    let length = int (min stream.Length (int64 TextEncoding.ProbeLength))
    let probe = Array.zeroCreate length
    stream.ReadExactly(probe, 0, length)
    probe

let private entryStream (fs: IFileSystem) password kind archive segments () =
    match ArchiveReader.openContent fs password kind archive segments with
    | Some content -> content
    | None -> raise (FileNotFoundException(FileSystemPath.value archive))

let openEntry (fs: IFileSystem) password (archive: FileSystemPath) (entry: ArchivePath) : Result<Source, ArchiveError> =
    let spelled = ArchivePath.spelled fs archive entry

    match ArchiveReader.kindOf fs archive with
    | None -> Error(KindCannotBeRead archive)
    | Some kind ->
        let segments = ArchivePath.segments entry

        try
            match ArchiveReader.entryInfo fs password kind archive segments with
            | None -> Error(ArchiveNotFound spelled)
            | Some info ->
                let content =
                    new RewindingStream(info.Size, entryStream fs password kind archive segments)

                Ok(new Source(spelled, content, TextEncoding.detect (probeOf content)))
        with ex ->
            Error(ArchiveReader.mapException archive ex)

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
