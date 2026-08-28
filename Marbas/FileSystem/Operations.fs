module Marbas.FileSystem.Operations

open System
open System.IO.Abstractions
open FsToolkit.ErrorHandling

type OperationError =
    | SourceNotFound of path: FileSystemPath
    | TargetExists of path: FileSystemPath
    | AccessDenied of path: FileSystemPath
    | Cancelled of path: FileSystemPath
    | TargetWithinSource of path: FileSystemPath
    | OperationFailed of path: FileSystemPath * message: string

type ItemResult =
    { Name: EntryName
      Outcome: Result<unit, OperationError> }

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

let mapException (path: FileSystemPath) (ex: exn) =
    match ex with
    | :? UnauthorizedAccessException -> AccessDenied path
    | _ -> OperationFailed(path, ex.Message)

let private attempt fs path (action: FileSystemAction) =
    try
        action fs path
        Ok()
    with ex ->
        Error(mapException path ex)

let private createDirectory (fs: IFileSystem) (path: FileSystemPath) =
    let raw = FileSystemPath.value path
    fs.Directory.CreateDirectory raw |> ignore

let private deleteDirectory (fs: IFileSystem) (path: FileSystemPath) =
    let raw = FileSystemPath.value path
    fs.Directory.Delete(raw, true)

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
    
let private getFileSystemEntries (fs: IFileSystem) (path: FileSystemPath) =
    let raw = FileSystemPath.value path
    fs.Directory.EnumerateFileSystemEntries raw
    |> Seq.map FileSystemPath.create

let isNonEmptyDirectory (fs: IFileSystem) (path: FileSystemPath) =
    match FileSystemPath.kind fs path with
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

let delete (fs: IFileSystem) (path: FileSystemPath) =
    match FileSystemPath.kind fs path with
    | DirectoryEntry -> attempt fs path deleteDirectory
    | FileEntry -> attempt fs path deleteFile
    | NoEntry -> Error(SourceNotFound path)

let rec private copyTo (fs: IFileSystem) (control: BatchControl) (source: FileSystemPath) (target: FileSystemPath) =
    if control.IsCancelled() then
        Error(Cancelled source)
    else
        match FileSystemPath.kind fs source with
        | DirectoryEntry ->
            result {
                do! attempt fs target createDirectory

                do!
                    getFileSystemEntries fs source
                    |> List.ofSeq
                    |> List.traverseResultM (fun entry ->
                        entry
                        |> FileSystemPath.name fs
                        |> EntryName.value
                        |> FileSystemPath.combine fs target
                        |> copyTo fs control entry)
                    |> Result.ignore
            }
        | _ ->
            control.Report source
            let copyToTarget fs' source' = copyFile fs' true source' target
            attempt fs source copyToTarget

let private inDirectory (fs: IFileSystem) (targetDirectory: FileSystemPath) (source: FileSystemPath) =
    source
    |> FileSystemPath.name fs
    |> EntryName.value
    |> FileSystemPath.combine fs targetDirectory

let copy (fs: IFileSystem) control (targetDirectory: FileSystemPath) (source: FileSystemPath) =
    match FileSystemPath.kind fs source with
    | NoEntry -> Error(SourceNotFound source)
    | _ ->
        source
        |> inDirectory fs targetDirectory
        |> copyTo fs control source

let moveTo (fs: IFileSystem) (control: BatchControl) (source: FileSystemPath) (target: FileSystemPath) =
    match FileSystemPath.kind fs source with
    | NoEntry -> Error(SourceNotFound source)
    | DirectoryEntry ->
        result {
            do! copyTo fs control source target
            do! delete fs source
        }
    | FileEntry when control.IsCancelled() -> Error(Cancelled source)
    | FileEntry ->
        control.Report source
        let moveToTarget fs' source' = moveFile fs' true source' target
        attempt fs source moveToTarget

let move (fs: IFileSystem) control (targetDirectory: FileSystemPath) (source: FileSystemPath) =
    source
    |> inDirectory fs targetDirectory
    |> moveTo fs control source

let rename (fs: IFileSystem) (source: FileSystemPath) (newName: EntryName) =
    let target =
        newName
        |> EntryName.value
        |> FileSystemPath.combine fs (FileSystemPath.directory fs source)

    match FileSystemPath.kind fs target with
    | NoEntry -> moveTo fs unattended source target
    | _ -> Error(TargetExists target)

let itemResult (fs: IFileSystem) source outcome =
    { Name = FileSystemPath.name fs source
      Outcome = outcome }

let private itemResults (fs: IFileSystem) sources outcome =
    sources |> List.map (fun source -> itemResult fs source (outcome source))

let failures (results: ItemResult list) =
    results |> List.filter (_.Outcome >> Result.isError)

let removeAll (fs: IFileSystem) remove sources = itemResults fs sources remove

let deleteAll (fs: IFileSystem) sources = removeAll fs (delete fs) sources

type private StandingAnswer =
    | AskEachTime
    | OverwriteEverything
    | SkipEverything

let approve (fs: IFileSystem) (ask: FileSystemPath -> OverwriteAnswer) sources targetDirectory =
    let answerFor standing target =
        match standing with
        | OverwriteEverything -> Yes
        | SkipEverything -> No
        | AskEachTime -> ask target

    let rec run standing approved remaining =
        match remaining with
        | [] -> List.rev approved
        | source :: rest ->
            let target = inDirectory fs targetDirectory source
            let targetAlreadyExists = FileSystemPath.kind fs target <> NoEntry

            let answer =
                if targetAlreadyExists then
                    answerFor standing target
                else
                    Yes

            match answer with
            | Cancel -> []
            | No -> run standing approved rest
            | NoToAll -> run SkipEverything approved rest
            | Yes -> run standing (source :: approved) rest
            | YesToAll -> run OverwriteEverything (source :: approved) rest

    run AskEachTime [] sources

let allWatched (fs: IFileSystem) (control: BatchControl) apply sources =
    let rec run finished remaining =
        match remaining with
        | [] -> List.rev finished
        | source :: rest ->
            let item = itemResult fs source (apply source)

            if control.IsCancelled() then
                List.rev (item :: finished)
            else
                run (item :: finished) rest

    run [] sources

let copyAll (fs: IFileSystem) control sources targetDirectory =
    allWatched fs control (copy fs control targetDirectory) sources

let moveAll (fs: IFileSystem) control sources targetDirectory =
    allWatched fs control (move fs control targetDirectory) sources
