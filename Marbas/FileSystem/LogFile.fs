module Marbas.FileSystem.LogFile

open System.IO
open System.IO.Abstractions

[<Literal>]
let FileName = "marbas.log"

[<Literal>]
let KeptBytes = 1048576

let beside (fs: IFileSystem) (settingsPath: FileSystemPath) =
    FileSystemPath.combine fs (FileSystemPath.directory fs settingsPath) FileName

let private longerThanKept (fs: IFileSystem) file =
    fs.File.Exists file && fs.FileInfo.New(file).Length > int64 KeptBytes

let private lastBytesWithTheOneBefore (fs: IFileSystem) file =
    use stream = fs.File.OpenRead file
    let read = Array.zeroCreate<byte> (KeptBytes + 1)
    stream.Seek(-(int64 read.Length), SeekOrigin.End) |> ignore
    stream.ReadExactly read
    read

let private afterFirstLineBreak (bytes: byte array) =
    match Array.tryFindIndex ((=) (byte '\n')) bytes with
    | Some lineBreak -> bytes[lineBreak + 1 ..]
    | None -> [||]

let cut (fs: IFileSystem) (path: FileSystemPath) =
    let file = FileSystemPath.value path

    try
        if longerThanKept fs file then
            let kept = lastBytesWithTheOneBefore fs file |> afterFirstLineBreak
            fs.File.WriteAllBytes(file, kept)

        Ok()
    with ex ->
        Error(LogFileError.mapException path ex)

let append (fs: IFileSystem) (path: FileSystemPath) (lines: string list) =
    try
        fs.Directory.CreateDirectory(FileSystemPath.value (FileSystemPath.directory fs path))
        |> ignore

        fs.File.AppendAllLines(FileSystemPath.value path, lines)
        Ok()
    with ex ->
        Error(LogFileError.mapException path ex)
