module Marbas.FileSystem.Operations

open System
open System.ComponentModel
open System.IO.Abstractions
open FsToolkit.ErrorHandling
open Marbas.Domain
open Marbas.FileSystem.DirectoryListing

type OperationError =
    | SourceNotFound of path: FileSystemPath
    | TargetExists of path: FileSystemPath
    | AccessDenied of path: FileSystemPath
    | Cancelled of path: FileSystemPath
    | TargetWithinSource of path: FileSystemPath
    | EntryOutsideTarget of path: FileSystemPath
    | ArchiveEntryNotEditable of path: FileSystemPath
    | ArchiveEntryNotRunnable of path: FileSystemPath
    | ArchiveEntryNotCut of path: FileSystemPath
    | NoProgramAssociated of path: FileSystemPath
    | StartDeclined of path: FileSystemPath
    | SymbolicLinkNotPermitted of path: FileSystemPath
    | LinkRefused of refusal: LinkRefusal * path: FileSystemPath
    | NotInSearchResults of refusal: ResultsRefusal
    | ArchiveFailed of error: ArchiveError
    | BatchFailed of message: string
    | OperationFailed of path: FileSystemPath * message: string

type ItemSource =
    | ItemOnDisk of path: FileSystemPath
    | ItemInArchive of archive: FileSystemPath * entry: ArchivePath
    | WholeBatch of title: string

type ItemResult =
    { Source: ItemSource
      Outcome: Result<unit, OperationError> }

module ItemSource =

    let name (fs: IFileSystem) source =
        match source with
        | ItemOnDisk path -> FileSystemPath.name fs path
        | ItemInArchive(archive, entry) ->
            ArchivePath.name entry |> Option.defaultValue (FileSystemPath.name fs archive)
        | WholeBatch title -> EntryName.create title

type OverwriteAnswer =
    | Yes
    | No
    | YesToAll
    | NoToAll
    | Cancel

type BatchControl =
    { Report: FileSystemPath -> unit
      IsCancelled: unit -> bool }

let unattended =
    { Report = ignore
      IsCancelled = fun () -> false }

type FileSystemAction = IFileSystem -> FileSystemPath -> unit

[<Literal>]
let private NoAssociation = 1155

[<Literal>]
let private PromptCancelled = 1223

[<Literal>]
let private PrivilegeNotHeld = 0x80070522

let mapException (path: FileSystemPath) (ex: exn) =
    match ex with
    | :? UnauthorizedAccessException -> AccessDenied path
    | :? IO.IOException as refusal when refusal.HResult = PrivilegeNotHeld -> SymbolicLinkNotPermitted path
    | :? Win32Exception as refusal ->
        match refusal.NativeErrorCode with
        | NoAssociation -> NoProgramAssociated path
        | PromptCancelled -> StartDeclined path
        | _ -> OperationFailed(path, refusal.Message)
    | _ -> OperationFailed(path, ex.Message)

let attempting (path: FileSystemPath) action =
    try
        Ok(action ())
    with ex ->
        Error(mapException path ex)

let guarded (title: string) (work: BatchControl -> ItemResult list) control =
    try
        work control
    with ex ->
        [ { Source = WholeBatch title
            Outcome = Error(BatchFailed ex.Message) } ]

let private attempt fs path (action: FileSystemAction) =
    attempting path (fun () -> action fs path)

let private createDirectory (fs: IFileSystem) (path: FileSystemPath) =
    let raw = FileSystemPath.value path
    fs.Directory.CreateDirectory raw |> ignore

let private deleteEmptyDirectory (fs: IFileSystem) (path: FileSystemPath) =
    let raw = FileSystemPath.value path
    fs.Directory.Delete raw

let private deleteFile (fs: IFileSystem) (path: FileSystemPath) =
    let raw = FileSystemPath.value path
    fs.File.Delete raw

let private copyFile (fs: IFileSystem) overwrite (source: FileSystemPath) (target: FileSystemPath) =
    let rawSource = FileSystemPath.value source
    let rawTarget = FileSystemPath.value target
    fs.File.Copy(rawSource, rawTarget, overwrite)

let private moveFile (fs: IFileSystem) overwrite (source: FileSystemPath) (target: FileSystemPath) =
    let rawSource = FileSystemPath.value source
    let rawTarget = FileSystemPath.value target
    fs.File.Move(rawSource, rawTarget, overwrite)

let private copyReplacing target fs source = copyFile fs true source target

let private moveReplacing target fs source = moveFile fs true source target

let private getFileSystemEntries (fs: IFileSystem) (path: FileSystemPath) =
    let raw = FileSystemPath.value path
    fs.Directory.EnumerateFileSystemEntries raw
    |> Seq.map FileSystemPath.create

let isNonEmptyDirectory (fs: IFileSystem) (path: FileSystemPath) =
    match FileSystemPath.kind fs path with
    | DirectoryEntry when FileSystemPath.isLink fs path -> false
    | DirectoryEntry ->
        try
            getFileSystemEntries fs path |> Seq.isEmpty |> not
        with _ ->
            true
    | _ -> false

let makeDirectory (fs: IFileSystem) (parent: FileSystemPath) (name: EntryName) =
    let path = FileSystemPath.combine fs parent (EntryName.value name)

    match FileSystemPath.kind fs path with
    | NoEntry -> attempt fs path createDirectory
    | _ -> Error(TargetExists path)

let private createFile (fs: IFileSystem) (path: FileSystemPath) =
    use stream = fs.File.Create(FileSystemPath.value path)
    ignore stream

let makeFile (fs: IFileSystem) (parent: FileSystemPath) (name: EntryName) =
    let path = FileSystemPath.combine fs parent (EntryName.value name)

    match FileSystemPath.kind fs path with
    | DirectoryEntry -> Error(TargetExists path)
    | FileEntry -> Ok path
    | NoEntry -> attempt fs path createFile |> Result.map (fun () -> path)

let rec private deleting (fs: IFileSystem) (control: BatchControl) (child: Child) =
    if control.IsCancelled() then
        Error(Cancelled child.Path)
    else
        control.Report child.Path

        match child.Entry.Kind, child.Entry.LinkTarget with
        | Directory, Some _ -> attempt fs child.Path deleteEmptyDirectory
        | File, _ -> attempt fs child.Path deleteFile
        | Directory, None ->
            result {
                let! below = attempting child.Path (fun () -> children fs child.Path)
                do! below |> List.traverseResultM (deleting fs control) |> Result.ignore
                do! attempt fs child.Path deleteEmptyDirectory
            }

let delete (fs: IFileSystem) (control: BatchControl) (path: FileSystemPath) =
    match existingEntry fs path with
    | None -> Error(SourceNotFound path)
    | Some entry -> deleting fs control { Path = path; Entry = entry }

[<Literal>]
let WipeBufferLength = 65536

let private clearReadOnly (fs: IFileSystem) (path: FileSystemPath) =
    let raw = FileSystemPath.value path
    fs.File.SetAttributes(raw, fs.File.GetAttributes raw &&& ~~~IO.FileAttributes.ReadOnly)

let private overwrite (fs: IFileSystem) (control: BatchControl) (path: FileSystemPath) =
    use stream =
        fs.File.Open(FileSystemPath.value path, IO.FileMode.Open, IO.FileAccess.Write, IO.FileShare.None)

    let buffer = Array.zeroCreate<byte> WipeBufferLength

    let rec pass (remaining: int64) =
        if remaining <= 0L then
            Ok()
        else
            let chunk = int (min remaining (int64 WipeBufferLength))
            Security.Cryptography.RandomNumberGenerator.Fill(Span(buffer, 0, chunk))
            stream.Write(buffer, 0, chunk)
            control.Report path

            if control.IsCancelled() then
                stream.Flush true
                Error(Cancelled path)
            else
                pass (remaining - int64 chunk)

    pass stream.Length
    |> Result.map (fun () ->
        stream.Flush true
        stream.SetLength 0L
        stream.Flush true)

let private anonymousName (fs: IFileSystem) (path: FileSystemPath) =
    let directory = FileSystemPath.directory fs path
    let length = (FileSystemPath.name fs path |> EntryName.value).Length

    let candidate () =
        Security.Cryptography.RandomNumberGenerator.GetHexString(length, true)
        |> FileSystemPath.combine fs directory

    Seq.init 100 (fun _ -> candidate ())
    |> Seq.tryFind (fun target -> FileSystemPath.kind fs target = NoEntry)

let private anonymize (fs: IFileSystem) (path: FileSystemPath) =
    match anonymousName fs path with
    | Some target ->
        attempt fs path (fun fs source -> moveFile fs false source target)
        |> Result.map (fun () -> target)
    | None -> Ok path

let private wipeFile (fs: IFileSystem) (control: BatchControl) (path: FileSystemPath) =
    result {
        do! attempt fs path clearReadOnly
        do! attempting path (fun () -> overwrite fs control path) |> Result.bind id
        let! anonymous = anonymize fs path
        do! attempt fs anonymous deleteFile
    }

let rec private wiping (fs: IFileSystem) (control: BatchControl) (child: Child) =
    if control.IsCancelled() then
        Error(Cancelled child.Path)
    else
        match child.Entry.Kind, child.Entry.LinkTarget with
        | Directory, Some _ -> attempt fs child.Path deleteEmptyDirectory
        | File, Some _ -> attempt fs child.Path deleteFile
        | File, None -> wipeFile fs control child.Path
        | Directory, None ->
            result {
                let! below = attempting child.Path (fun () -> children fs child.Path)
                do! below |> List.traverseResultM (wiping fs control) |> Result.ignore
                do! attempt fs child.Path clearReadOnly
                do! attempt fs child.Path deleteEmptyDirectory
            }

let wipe (fs: IFileSystem) (control: BatchControl) (path: FileSystemPath) =
    if control.IsCancelled() then
        Error(Cancelled path)
    else
        match existingEntry fs path with
        | None -> Error(SourceNotFound path)
        | Some entry -> wiping fs control { Path = path; Entry = entry }

let private copyLink (fs: IFileSystem) (pointingAt: FileSystemPath) (target: FileSystemPath) =
    fs.Directory.CreateSymbolicLink(FileSystemPath.value target, FileSystemPath.value pointingAt)
    |> ignore

let private landingInDirectory (fs: IFileSystem) (targetDirectory: FileSystemPath) (source: FileSystemPath) =
    source
    |> FileSystemPath.name fs
    |> EntryName.value
    |> FileSystemPath.combine fs targetDirectory

let rec private copying (fs: IFileSystem) (control: BatchControl) (child: Child) (target: FileSystemPath) =
    if control.IsCancelled() then
        Error(Cancelled child.Path)
    else
        match child.Entry.Kind, child.Entry.LinkTarget with
        | Directory, Some pointingAt ->
            control.Report child.Path
            attempting child.Path (fun () -> copyLink fs pointingAt target)
        | Directory, None ->
            result {
                do! attempt fs target createDirectory
                let! below = attempting child.Path (fun () -> children fs child.Path)

                do!
                    below
                    |> List.traverseResultM (fun inner ->
                        EntryName.value inner.Entry.Name
                        |> FileSystemPath.combine fs target
                        |> copying fs control inner)
                    |> Result.ignore
            }
        | File, _ ->
            control.Report child.Path
            attempt fs child.Path (copyReplacing target)

let private copyToPath (fs: IFileSystem) (control: BatchControl) (source: FileSystemPath) (target: FileSystemPath) =
    match existingEntry fs source with
    | None -> Error(SourceNotFound source)
    | Some entry -> copying fs control { Path = source; Entry = entry } target

let copy (fs: IFileSystem) control (targetDirectory: FileSystemPath) (source: FileSystemPath) =
    source |> landingInDirectory fs targetDirectory |> copyToPath fs control source

type private DirectoryMove =
    | Renamed
    | RenameRefused
    | RenameFailed of error: OperationError

let private renameDirectory (fs: IFileSystem) (source: FileSystemPath) (target: FileSystemPath) =
    try
        fs.Directory.Move(FileSystemPath.value source, FileSystemPath.value target)
        Renamed
    with
    | :? IO.IOException -> RenameRefused
    | ex -> RenameFailed(mapException source ex)

let private mustFinish (control: BatchControl) =
    { control with IsCancelled = fun () -> false }

let moveByCopying (fs: IFileSystem) (control: BatchControl) (source: FileSystemPath) (target: FileSystemPath) =
    result {
        do! copyToPath fs control source target
        do! delete fs (mustFinish control) source
    }

let moveToPath (fs: IFileSystem) (control: BatchControl) (source: FileSystemPath) (target: FileSystemPath) =
    match FileSystemPath.kind fs source with
    | NoEntry -> Error(SourceNotFound source)
    | DirectoryEntry when control.IsCancelled() -> Error(Cancelled source)
    | DirectoryEntry ->
        control.Report source

        match renameDirectory fs source target with
        | Renamed -> Ok()
        | RenameFailed error -> Error error
        | RenameRefused -> moveByCopying fs control source target
    | FileEntry when control.IsCancelled() -> Error(Cancelled source)
    | FileEntry ->
        control.Report source
        attempt fs source (moveReplacing target)

let move (fs: IFileSystem) control (targetDirectory: FileSystemPath) (source: FileSystemPath) =
    source
    |> landingInDirectory fs targetDirectory
    |> moveToPath fs control source

type RenameStep =
    | RenameDone
    | CopyNeeded of target: FileSystemPath

let startRename (fs: IFileSystem) (source: FileSystemPath) (newName: EntryName) =
    let target =
        newName
        |> EntryName.value
        |> FileSystemPath.combine fs (FileSystemPath.directory fs source)

    match FileSystemPath.kind fs target, FileSystemPath.kind fs source with
    | NoEntry, NoEntry -> Error(SourceNotFound source)
    | NoEntry, FileEntry ->
        attempt fs source (moveReplacing target) |> Result.map (fun () -> RenameDone)
    | NoEntry, DirectoryEntry ->
        match renameDirectory fs source target with
        | Renamed -> Ok RenameDone
        | RenameFailed error -> Error error
        | RenameRefused -> Ok(CopyNeeded target)
    | _ -> Error(TargetExists target)

let itemResult source outcome =
    { Source = ItemOnDisk source
      Outcome = outcome }

let failures (results: ItemResult list) =
    results |> List.filter (_.Outcome >> Result.isError)

let wholeOutcome (results: ItemResult list) =
    match failures results with
    | [] -> Ok()
    | failed :: _ -> failed.Outcome

type private StandingAnswer =
    | AskEachTime
    | OverwriteEverything
    | SkipEverything

let approveLandings
    (landing: FileSystemPath -> FileSystemPath)
    (occupied: FileSystemPath -> bool)
    (ask: FileSystemPath -> OverwriteAnswer)
    sources
    =
    let answerFor standing target =
        match standing with
        | OverwriteEverything -> Yes
        | SkipEverything -> No
        | AskEachTime -> ask target

    let rec run standing approved remaining =
        match remaining with
        | [] -> List.rev approved
        | source :: rest ->
            let target = landing source

            let answer =
                if occupied target then answerFor standing target else Yes

            match answer with
            | Cancel -> []
            | No -> run standing approved rest
            | NoToAll -> run SkipEverything approved rest
            | Yes -> run standing (source :: approved) rest
            | YesToAll -> run OverwriteEverything (source :: approved) rest

    run AskEachTime [] sources

let approve (fs: IFileSystem) (ask: FileSystemPath -> OverwriteAnswer) sources targetDirectory =
    let occupied target = FileSystemPath.kind fs target <> NoEntry

    approveLandings (landingInDirectory fs targetDirectory) occupied ask sources

let allWatched (control: BatchControl) apply sources =
    let rec run finished remaining =
        match remaining with
        | [] -> List.rev finished
        | source :: rest ->
            let item = itemResult source (apply source)

            if control.IsCancelled() then
                List.rev (item :: finished)
            else
                run (item :: finished) rest

    run [] sources

let copyAll (fs: IFileSystem) control sources targetDirectory =
    allWatched control (copy fs control targetDirectory) sources

let moveAll (fs: IFileSystem) control sources targetDirectory =
    allWatched control (move fs control targetDirectory) sources

let wipeAll (fs: IFileSystem) control sources =
    allWatched control (wipe fs control) sources

let deleteAll (fs: IFileSystem) control sources =
    allWatched control (delete fs control) sources

let trashAll send (control: BatchControl) sources =
    let sending source =
        control.Report source
        send source

    allWatched control sending sources
