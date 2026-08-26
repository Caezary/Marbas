module Marbas.Adapters.RecycleBin

open System.IO.Abstractions
open Microsoft.VisualBasic.FileIO
open Marbas.FileSystem

type private Recycler = Microsoft.VisualBasic.FileIO.FileSystem

let private interaction = UIOption.OnlyErrorDialogs

let private recycleBin = RecycleOption.SendToRecycleBin

let private refusal = UICancelOption.ThrowException

let send (fs: IFileSystem) (path: FileSystemPath) =
    let raw = FileSystemPath.value path

    try
        match FileSystemPath.kind fs path with
        | NoEntry -> Error(Operations.SourceNotFound path)
        | DirectoryEntry ->
            Recycler.DeleteDirectory(raw, interaction, recycleBin, refusal)
            Ok()
        | FileEntry ->
            Recycler.DeleteFile(raw, interaction, recycleBin, refusal)
            Ok()
    with ex ->
        Error(Operations.mapException path ex)
