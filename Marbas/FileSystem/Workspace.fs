module Marbas.FileSystem.Workspace

open System
open System.IO.Abstractions
open FsToolkit.ErrorHandling
open Marbas.FileSystem.DirectoryListing
open Marbas.FileSystem.Operations

[<Literal>]
let DirectoryName = "Marbas"

[<Literal>]
let OwnerFileName = "owner"

let private UnclaimedKept = TimeSpan.FromDays 1.0

type SweepRefusal =
    { Directory: FileSystemPath
      Reason: OperationError }

type private Standing =
    | InUse
    | LeftBehind

let location (fs: IFileSystem) (temporaryRoot: string) (session: string) =
    let root =
        if String.IsNullOrWhiteSpace temporaryRoot then
            fs.Path.GetTempPath()
        else
            temporaryRoot

    fs.Path.Combine(root, DirectoryName, session) |> FileSystemPath.create

let ensure (fs: IFileSystem) (path: FileSystemPath) =
    match FileSystemPath.kind fs path with
    | DirectoryEntry -> Ok path
    | _ ->
        attempting path (fun () -> fs.Directory.CreateDirectory(FileSystemPath.value path))
        |> Result.map (fun _ -> path)

let scratch (fs: IFileSystem) (workspace: FileSystemPath) =
    let rec free root ordinal =
        let path = FileSystemPath.combine fs root (string (ordinal: int))

        if FileSystemPath.kind fs path = NoEntry then
            path
        else
            free root (ordinal + 1)

    result {
        let! root = ensure fs workspace
        let path = free root 1
        do! attempting path (fun () -> fs.Directory.CreateDirectory(FileSystemPath.value path) |> ignore)
        return path
    }

let claim (fs: IFileSystem) (owner: SessionOwner) (workspace: FileSystemPath) =
    result {
        let! root = ensure fs workspace
        let file = FileSystemPath.combine fs root OwnerFileName
        do! attempting file (fun () -> fs.File.WriteAllLines(FileSystemPath.value file, SessionOwner.lines owner))
    }

let private ownerOf (fs: IFileSystem) (directory: FileSystemPath) =
    let file = FileSystemPath.combine fs directory OwnerFileName

    attempting file (fun () -> fs.File.ReadAllLines(FileSystemPath.value file) |> List.ofArray)
    |> Result.toOption
    |> Option.bind SessionOwner.tryParse

let rec private lastWritten (fs: IFileSystem) (child: Child) =
    match child.Entry.Kind, child.Entry.LinkTarget with
    | Directory, None ->
        children fs child.Path
        |> List.map (lastWritten fs)
        |> List.fold max child.Entry.Modified
    | Directory, Some _
    | File, _ -> child.Entry.Modified

let private standing (fs: IFileSystem) (isRunning: SessionOwner -> bool) (now: DateTimeOffset) (session: Child) =
    match ownerOf fs session.Path with
    | Some owner when isRunning owner -> Ok InUse
    | Some _ -> Ok LeftBehind
    | None ->
        attempting session.Path (fun () -> lastWritten fs session)
        |> Result.map (fun written -> if now - written < UnclaimedKept then InUse else LeftBehind)

let private refusalOf (fs: IFileSystem) isRunning now (session: Child) =
    let removed =
        standing fs isRunning now session
        |> Result.bind (fun found ->
            match found with
            | InUse -> Ok()
            | LeftBehind -> delete fs unattended session.Path)

    match removed with
    | Ok() -> None
    | Error reason ->
        Some
            { Directory = session.Path
              Reason = reason }

let sweep (fs: IFileSystem) (isRunning: SessionOwner -> bool) (now: DateTimeOffset) (own: FileSystemPath) =
    let home = FileSystemPath.directory fs own
    let platform = FileSystemPath.platformOf fs

    let isAnotherSession (child: Child) =
        child.Entry.Kind = Directory
        && not (FileSystemPath.sameEntry platform own child.Path)

    match FileSystemPath.kind fs home with
    | DirectoryEntry ->
        match attempting home (fun () -> children fs home) with
        | Ok found ->
            found
            |> List.filter isAnotherSession
            |> List.choose (refusalOf fs isRunning now)
        | Error reason -> [ { Directory = home; Reason = reason } ]
    | FileEntry
    | NoEntry -> []

let release (fs: IFileSystem) (path: FileSystemPath) =
    match FileSystemPath.kind fs path with
    | DirectoryEntry -> delete fs unattended path |> ignore
    | _ -> ()

let listFile (fs: IFileSystem) (workspace: FileSystemPath) =
    let rec free ordinal =
        let path = FileSystemPath.combine fs workspace $"marked-{ordinal}.txt"

        if FileSystemPath.kind fs path = NoEntry then
            path
        else
            free (ordinal + 1)

    free 1
