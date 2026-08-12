module Marbas.FileSystem.FileContent

open System
open System.IO.Abstractions
open System.Text

[<Literal>]
let MaxBytes = 1048576

type ContentBody =
    | Text of lines: string list
    | Binary of bytes: byte array

type ContentExtent =
    | Whole
    | Truncated

type Content =
    { Body: ContentBody
      Extent: ContentExtent }

type ContentError =
    | FileNotFound of path: FileSystemPath
    | AccessDenied of path: FileSystemPath
    | ReadFailed of path: FileSystemPath * message: string

let mapException path (ex: exn) =
    match ex with
    | :? UnauthorizedAccessException -> AccessDenied path
    | _ -> ReadFailed(path, ex.Message)

let private withoutTrailingBlank lines =
    match List.tryLast lines with
    | Some "" -> List.truncate (List.length lines - 1) lines
    | _ -> lines

let private splitLines (text: string) =
    text.Split([| "\r\n"; "\n"; "\r" |], StringSplitOptions.None)
    |> List.ofArray
    |> withoutTrailingBlank

let private bodyOf (bytes: byte array) =
    if Array.contains 0uy bytes then
        Binary bytes
    else
        Encoding.UTF8.GetString bytes |> splitLines |> Text

let private readUpToLimit (fs: IFileSystem) (path: FileSystemPath) =
    use stream = fs.File.OpenRead(FileSystemPath.value path)
    let bytes = Array.zeroCreate (int (min stream.Length (int64 MaxBytes)))
    stream.ReadExactly(bytes, 0, bytes.Length)

    { Body = bodyOf bytes
      Extent = if stream.Length > int64 MaxBytes then Truncated else Whole }

let read (fs: IFileSystem) (path: FileSystemPath) : Result<Content, ContentError> =
    match FileSystemPath.kind fs path with
    | FileEntry ->
        try
            Ok(readUpToLimit fs path)
        with ex ->
            Error(mapException path ex)
    | _ -> Error(FileNotFound path)
