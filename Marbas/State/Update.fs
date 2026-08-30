module Marbas.State.Update

open System.IO.Abstractions
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.State.AppState

type Message =
    | CursorMoved of PanelCursor
    | PanelFocused of PanelSide
    | EntryOpened
    | RefreshRequested
    | SelectionToggled of CursorDirection
    | SelectionToggledTo of PanelCursor
    | SortRequested of SortKey
    | HiddenToggled
    | SearchTyped of character: char
    | SearchBackspaced
    | SearchCancelled
    | PatternRequested of SelectionMode
    | PatternEntered of SelectionMode * NamePattern
    | MakeDirectoryRequested
    | NameEntered of name: EntryName
    | DeleteRequested of DeleteMode
    | DeleteConfirmed of DeleteMode
    | DeleteFinished of Operations.ItemResult list
    | CopyRequested
    | CopyFinished of Operations.ItemResult list
    | MoveRequested
    | TargetEntered of text: string
    | MoveFinished of Operations.ItemResult list
    | PackRequested
    | ArchiveEntered of ArchiveChoice
    | PackFinished of archive: EntryName * results: Operations.ItemResult list
    | UnpackRequested
    | UnpackFinished of results: Operations.ItemResult list
    | RenameFinished of newName: EntryName * outcome: Result<unit, Operations.OperationError>
    | ViewRequested
    | EditRequested
    | EditFinished of outcome: Result<unit, Operations.OperationError>
    | NewFileRequested
    | FileNameEntered of name: EntryName
    | PropertiesEntered of EntryProperties
    | SettingsRequested
    | EditorsEntered of EditorSettings
    | RootRequested of PanelSide
    | PathOpened of PanelSide * FileSystemPath
    | QuitRequested

type DeleteRequest =
    { Targets: EntryName list
      Scope: DeleteScope
      Mode: DeleteMode }

type Effect =
    | ReportListingError of ListingError
    | ReportOperationError of Operations.OperationError
    | ReportItemFailures of Operations.ItemResult list
    | ConfirmDelete of DeleteRequest
    | DeleteTargets of sources: FileSystemPath list * mode: DeleteMode
    | CopyTargets of sources: FileSystemPath list * targetDirectory: FileSystemPath
    | MoveTargets of sources: FileSystemPath list * targetDirectory: FileSystemPath
    | ArchiveTargets of sources: FileSystemPath list * request: ArchiveRequest
    | ExtractTargets of archives: FileSystemPath list * targetDirectory: FileSystemPath
    | ExtractEntries of archive: FileSystemPath * entries: ArchivePath list * targetDirectory: FileSystemPath
    | AddEntries of archive: FileSystemPath * level: ArchivePath * sources: FileSystemPath list
    | MoveEntriesIn of archive: FileSystemPath * level: ArchivePath * sources: FileSystemPath list
    | MoveEntriesOut of archive: FileSystemPath * entries: ArchivePath list * targetDirectory: FileSystemPath
    | RemoveEntries of archive: FileSystemPath * entries: ArchivePath list
    | RenameTarget of source: FileSystemPath * newName: EntryName
    | ViewFile of path: FileSystemPath
    | ViewArchiveEntry of archive: FileSystemPath * entry: ArchivePath
    | EditFile of path: FileSystemPath * editor: EditorPath option
    | ShowProperties of EntryProperties
    | ShowSettings of EditorSettings * candidate: EditorPath option
    | PromptForTarget of suggestion: FileSystemPath
    | PromptForArchive of suggestion: FileSystemPath
    | PromptForName of suggestion: EntryName option
    | PromptForFileName
    | PromptForPattern of SelectionMode
    | PromptForRoot of PanelSide
    | SaveSettings of Settings
    | Quit

type UpdateResult =
    { State: AppState
      Effects: Effect list }

let private stay state = { State = state; Effects = [] }

let private request state effect = { State = state; Effects = [ effect ] }

let private changing (fs: IFileSystem) state archive effect =
    match Archive.changeableKind fs archive with
    | Some _ -> request state effect
    | None -> request state (ReportOperationError(Operations.ArchiveFailed(KindCannotBeChanged archive)))

let private applyListing state result =
    match result with
    | Ok updated -> stay updated
    | Error error -> request state (ReportListingError error)

let private applyOperation state result =
    match result with
    | Ok updated -> stay updated
    | Error error -> request state (ReportOperationError error)

let private applyBatch state result =
    match result with
    | Error error -> request state (ReportOperationError error)
    | Ok(updated, results) ->
        match Operations.failures results with
        | [] -> stay updated
        | failed -> request updated (ReportItemFailures failed)

let update (fs: IFileSystem) (msg: Message) (state: AppState) : UpdateResult =
    match msg with
    | CursorMoved cursor -> stay (setCursor cursor.Side cursor.Index state)
    | PanelFocused side -> stay (activate side state)
    | EntryOpened -> enter fs state |> applyListing state
    | RefreshRequested -> refresh fs state |> applyListing state
    | SelectionToggled direction -> stay (toggleSelectionMoving direction state.Active state)
    | SelectionToggledTo cursor -> stay (toggleSelectionThrough cursor.Side cursor.Index state)
    | SortRequested key -> stay (sortBy key state.Active state)
    | HiddenToggled -> toggleHidden fs state.Active state |> applyListing state
    | SearchTyped character -> stay (typeIntoSearch state.Active character state)
    | SearchBackspaced -> stay (backspaceSearch state.Active state)
    | SearchCancelled -> stay (closeSearch state.Active state)
    | PatternRequested mode -> request state (PromptForPattern mode)
    | PatternEntered(mode, pattern) -> stay (applyPattern mode state.Active pattern state)
    | MakeDirectoryRequested ->
        match (activePanel state).Path with
        | OnDisk _ -> request state (PromptForName state.LastCreatedDirectory)
        | InArchive(archive, _) -> changing fs state archive (PromptForName state.LastCreatedDirectory)
    | NameEntered name ->
        match (activePanel state).Path with
        | OnDisk directory -> makeDirectory fs state.Active directory name state |> applyOperation state
        | InArchive(archive, level) ->
            makeEntryDirectory fs state.Active archive level name state |> applyOperation state
    | DeleteRequested mode ->
        let target = activePanel state

        let confirmed asked =
            ConfirmDelete
                { Targets = operationTargets target |> List.map _.Name
                  Scope = deleteScope fs target
                  Mode = asked }

        match target.Path, operationTargets target with
        | _, [] -> stay state
        | OnDisk _, _ -> request state (confirmed mode)
        | InArchive(archive, _), _ -> changing fs state archive (confirmed Permanently)
    | DeleteConfirmed mode ->
        match transferSource fs (activePanel state) with
        | None -> stay state
        | Some(FromDisk paths) -> request state (DeleteTargets(paths, mode))
        | Some(FromArchive(archive, entries)) -> request state (RemoveEntries(archive, entries))
    | DeleteFinished results -> finishDelete fs results state |> applyBatch state
    | CopyRequested ->
        match transferSource fs (activePanel state), transferTarget state with
        | None, _ -> stay state
        | Some(FromDisk paths), ToDirectory target -> request state (CopyTargets(paths, target))
        | Some(FromArchive(archive, entries)), ToDirectory target ->
            request state (ExtractEntries(archive, entries, target))
        | Some(FromDisk paths), ToArchive(archive, level) ->
            changing fs state archive (AddEntries(archive, level, paths))
        | Some(FromArchive _), ToArchive _ -> stay state
    | CopyFinished results -> finishTransfer fs results state |> applyBatch state
    | MoveRequested ->
        match transferSource fs (activePanel state), transferTarget state with
        | None, _ -> stay state
        | Some(FromDisk _), ToDirectory target -> request state (PromptForTarget target)
        | Some(FromArchive(archive, entries)), ToDirectory target ->
            changing fs state archive (MoveEntriesOut(archive, entries, target))
        | Some(FromDisk paths), ToArchive(archive, level) ->
            changing fs state archive (MoveEntriesIn(archive, level, paths))
        | Some(FromArchive _), ToArchive _ -> stay state
    | TargetEntered text ->
        match text.Trim(), operationPaths fs (activePanel state), activeDirectory state with
        | "", _, _
        | _, [], _
        | _, _, None -> stay state
        | trimmed, [ single ], _ when FileSystemPath.isBareName fs trimmed ->
            request state (RenameTarget(single, EntryName.create trimmed))
        | trimmed, paths, Some source -> request state (MoveTargets(paths, FileSystemPath.combine fs source trimmed))
    | MoveFinished results -> finishTransfer fs results state |> applyBatch state
    | PackRequested ->
        match operationTargets (activePanel state), archiveSuggestion fs ArchiveFormat.initial state with
        | [], _
        | _, None -> stay state
        | _, Some suggestion -> request state (PromptForArchive suggestion)
    | ArchiveEntered choice ->
        match choice.Target.Trim(), operationPaths fs (activePanel state), activeDirectory state with
        | "", _, _
        | _, [], _
        | _, _, None -> stay state
        | trimmed, paths, Some source ->
            let target = FileSystemPath.combine fs source trimmed

            if Archive.wouldPackItself fs target paths then
                request state (ReportOperationError(Operations.TargetWithinSource target))
            else
                request
                    state
                    (ArchiveTargets(
                        paths,
                        { Path = target
                          Format = choice.Format
                          Compression = choice.Compression
                          Password = choice.Password }
                    ))
    | PackFinished(archive, results) -> finishPack fs archive results state |> applyBatch state
    | UnpackRequested ->
        match operationPaths fs (activePanel state), targetDirectory state with
        | [], _
        | _, None -> stay state
        | paths, Some target -> request state (ExtractTargets(paths, target))
    | UnpackFinished results -> finishTransfer fs results state |> applyBatch state
    | RenameFinished(newName, outcome) ->
        match outcome with
        | Error error -> request state (ReportOperationError error)
        | Ok() -> finishRename fs newName state |> applyOperation state
    | ViewRequested ->
        let target = activePanel state

        match cursorFilePath fs target, cursorFileInArchive target with
        | Some path, _ -> request state (ViewFile path)
        | _, Some(archive, entry) -> request state (ViewArchiveEntry(archive, entry))
        | None, None -> stay state
    | EditRequested ->
        let target = activePanel state

        match cursorEntryInArchive target with
        | Some(archive, entry) ->
            request state (ReportOperationError(Operations.ArchiveEntryNotEditable(ArchivePath.spelled fs archive entry)))
        | None ->
            match cursorFilePath fs target, cursorDirectoryPath fs target with
            | Some path, _ -> request state (EditFile(path, editorFor (FileSystemPath.name fs path) state))
            | _, Some path ->
                match EntryProperties.read fs path with
                | Ok properties -> request state (ShowProperties properties)
                | Error error -> request state (ReportOperationError error)
            | None, None -> stay state
    | NewFileRequested ->
        match activeDirectory state with
        | None -> stay state
        | Some _ -> request state PromptForFileName
    | FileNameEntered name ->
        match activeDirectory state with
        | None -> stay state
        | Some directory ->
            match makeFile fs state.Active directory name state with
            | Error error -> request state (ReportOperationError error)
            | Ok(updated, path) -> request updated (EditFile(path, editorFor name updated))
    | PropertiesEntered properties -> applyProperties fs properties state |> applyOperation state
    | SettingsRequested ->
        let candidate =
            cursorExecutablePath fs (activePanel state)
            |> Option.bind (FileSystemPath.value >> EditorPath.tryCreate)

        request state (ShowSettings(state.Editors, candidate))
    | EditorsEntered editors -> stay (withEditors editors state)
    | EditFinished outcome ->
        match outcome with
        | Error error -> request state (ReportOperationError error)
        | Ok() -> refresh fs state |> applyListing state
    | RootRequested side -> request state (PromptForRoot side)
    | PathOpened(side, path) -> loadPanel fs side path state |> applyListing state
    | QuitRequested ->
        { State = state
          Effects = [ SaveSettings(settings fs state); Quit ] }

let step (fs: IFileSystem) (msg: Message) (state: AppState) =
    let result = update fs msg state
    result.State, result.Effects
