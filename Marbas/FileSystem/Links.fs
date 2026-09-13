namespace Marbas.FileSystem

open System.IO.Abstractions
open Marbas.Domain
open Marbas.FileSystem.Operations

type LinkRequest =
    { Kind: LinkKind
      Target: FileSystemPath
      Location: FileSystemPath }

module Links =

    let private absolute (fs: IFileSystem) (request: LinkRequest) =
        let standing = FileSystemPath.directory fs request.Location |> FileSystemPath.value
        let target = fs.Path.Combine(standing, FileSystemPath.value request.Target)
        FileSystemPath.create (fs.Path.GetFullPath target)

    let private makeSymbolic (fs: IFileSystem) (location: FileSystemPath) (target: FileSystemPath) targetKind =
        let rawLocation = FileSystemPath.value location
        let rawTarget = FileSystemPath.value target

        attempting location (fun () ->
            match targetKind with
            | DirectoryEntry -> fs.Directory.CreateSymbolicLink(rawLocation, rawTarget) |> ignore
            | _ -> fs.File.CreateSymbolicLink(rawLocation, rawTarget) |> ignore)

    let create (fs: IFileSystem) (request: LinkRequest) =
        let target = absolute fs request

        match FileSystemPath.kind fs request.Location, FileSystemPath.kind fs target with
        | FileEntry, _
        | DirectoryEntry, _ -> Error(TargetExists request.Location)
        | NoEntry, NoEntry -> Error(SourceNotFound target)
        | NoEntry, targetKind ->
            match request.Kind with
            | Symbolic -> makeSymbolic fs request.Location target targetKind
            | Hard
            | Junction -> Error(LinkKindUnavailable(request.Kind, request.Location))
