module Marbas.State.Update

open System.IO.Abstractions
open Marbas.Collections
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.State.AppState

type Message =
    | CursorMoved of PanelCursor
    | PanelFocused of PanelSide
    | EntryOpened
    | WalkIntoRequested
    | RefreshRequested
    | ExternalChangeDetected
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
    | LaunchFinished of outcome: Result<unit, LaunchError>
    | OpenWithRequested
    | HandlerChosen of path: FileSystemPath * choice: OpenWithChoice
    | ElevatedRunRequested
    | NewFileRequested
    | FileNameEntered of name: EntryName
    | PropertiesEntered of EntryProperties
    | SettingsRequested
    | ProgramsEntered of ProgramSettings
    | RootRequested of PanelSide
    | PathOpened of PanelSide * FileSystemPath
    | TabOpened
    | TabClosed
    | TabsRequested
    | TabSwitched of position: int
    | TabCycled of TabStep
    | SessionSaveRequested
    | QuitRequested

type DeleteRequest =
    { Targets: EntryName list
      Scope: DeleteScope
      Mode: DeleteMode }

type Effect =
    | ReportListingError of ListingError
    | ReportOperationError of Operations.OperationError
    | ReportLaunchError of LaunchError
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
    | CopyEntriesAcross of
        source: FileSystemPath *
        entries: ArchivePath list *
        target: FileSystemPath *
        level: ArchivePath
    | MoveEntriesAcross of
        source: FileSystemPath *
        entries: ArchivePath list *
        target: FileSystemPath *
        level: ArchivePath
    | RenameTarget of source: FileSystemPath * newName: EntryName
    | ViewFile of path: FileSystemPath
    | ViewArchiveEntry of archive: FileSystemPath * entry: ArchivePath
    | EditFile of path: FileSystemPath * editor: ProgramPath option
    | RunProgram of LaunchRequest
    | ShowHandlers of path: FileSystemPath * chosen: ProgramPath option
    | ShowProperties of EntryProperties
    | ShowSettings of ProgramSettings * candidate: ProgramPath option
    | PromptForTarget of suggestion: FileSystemPath
    | PromptForArchive of suggestion: FileSystemPath
    | PromptForName of suggestion: EntryName option
    | PromptForFileName
    | PromptForPattern of SelectionMode
    | PromptForRoot of PanelSide
    | ShowTabs of Zipper<Tab>
    | WatchLocations of paths: FileSystemPath list
    | SaveSettings of Settings
    | Quit

type UpdateResult =
    { State: AppState
      Effects: Effect list }

let private stay state = { State = state; Effects = [] }

let private request state effect = { State = state; Effects = [ effect ] }

let private changing (fs: IFileSystem) state archives effect =
    match archives |> List.tryFind (Archive.changeableKind fs >> Option.isNone) with
    | None -> request state effect
    | Some unchangeable -> request state (ReportOperationError(Operations.ArchiveFailed(KindCannotBeChanged unchangeable)))

let private saving (fs: IFileSystem) state =
    request state (SaveSettings(settings fs state))

let private standingOn (fs: IFileSystem) state =
    match readTabAgain fs state with
    | Ok updated -> saving fs updated
    | Error error ->
        { State = state
          Effects = [ SaveSettings(settings fs state); ReportListingError error ] }

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

let private startingAs elevation state directory program target =
    request
        state
        (RunProgram
            { Target = target
              Program = program
              WorkingDirectory = directory
              Elevation = elevation })

let private running state directory program target =
    startingAs AsUser state directory program target

let private opening (fs: IFileSystem) state directory path =
    match programFor ForOpening (FileSystemPath.name fs path) state with
    | Some program -> running state directory (Some program) path
    | None ->
        match ArchiveReader.kindOf fs path with
        | Some _ -> enter fs state |> applyListing state
        | None -> running state directory None path

let private opened (fs: IFileSystem) state =
    let panel = activePanel state

    match cursorFileInArchive panel with
    | Some(archive, entry) ->
        request state (ReportOperationError(Operations.ArchiveEntryNotRunnable(ArchivePath.spelled fs archive entry)))
    | None ->
        match cursorFilePath fs panel, activeDirectory state with
        | Some path, Some directory -> opening fs state directory path
        | _ -> enter fs state |> applyListing state

let private revealed (fs: IFileSystem) panel directory =
    match cursorEntry panel with
    | Some entry when entry.Name = DirectoryListing.ParentName -> Some directory
    | _ -> cursorDirectoryPath fs panel

let private refusingInArchive (fs: IFileSystem) state panel archive level =
    let named =
        cursorEntryInArchive panel
        |> Option.map snd
        |> Option.defaultValue level

    request state (ReportOperationError(Operations.ArchiveEntryNotRunnable(ArchivePath.spelled fs archive named)))

let private openedWith (fs: IFileSystem) state =
    let panel = activePanel state

    match panel.Path with
    | InArchive(archive, level) -> refusingInArchive fs state panel archive level
    | OnDisk directory ->
        match cursorFilePath fs panel with
        | Some path -> request state (ShowHandlers(path, programFor ForOpening (FileSystemPath.name fs path) state))
        | None ->
            match revealed fs panel directory with
            | Some target -> running state directory None target
            | None -> stay state

let private runElevated (fs: IFileSystem) state =
    let panel = activePanel state

    match panel.Path with
    | InArchive(archive, level) -> refusingInArchive fs state panel archive level
    | OnDisk directory ->
        match cursorExecutablePath fs panel with
        | Some path -> startingAs AsAdministrator state directory None path
        | None ->
            match cursorFilePath fs panel |> Option.orElseWith (fun () -> cursorDirectoryPath fs panel) with
            | Some path -> request state (ReportLaunchError(NotAProgram path))
            | None -> stay state

let private chosen (fs: IFileSystem) state path (choice: OpenWithChoice) =
    let remembered =
        match FileExtension.ofName (FileSystemPath.name fs path) with
        | Some extension when choice.Remember ->
            withPrograms (ProgramSettings.associate ForOpening extension choice.Program state.Programs) state
        | _ -> state

    match activeDirectory remembered with
    | Some directory -> running remembered directory (Some choice.Program) path
    | None -> stay remembered

let private transition (fs: IFileSystem) (msg: Message) (state: AppState) : UpdateResult =
    let applyListing' = applyListing state
    let applyOperation' = applyOperation state
    let applyBatch' = applyBatch state
    let changing' = changing fs state
    let saving' = saving fs
    let standingOn' = standingOn fs
    let opened' = opened fs
    let openedWith' = openedWith fs
    let runElevated' = runElevated fs
    let chosen' = chosen fs state
    let settings' = settings fs
    let enter' = enter fs
    let refresh' = refresh fs
    let readTabQuietly' = readTabQuietly fs
    let toggleHidden' = toggleHidden fs
    let loadPanel' = loadPanel fs
    let makeDirectory' = makeDirectory fs
    let makeEntryDirectory' = makeEntryDirectory fs
    let makeFile' = makeFile fs
    let deleteScope' = deleteScope fs
    let transferSource' = transferSource fs
    let operationPaths' = operationPaths fs
    let cursorFilePath' = cursorFilePath fs
    let cursorDirectoryPath' = cursorDirectoryPath fs
    let cursorExecutablePath' = cursorExecutablePath fs
    let archiveSuggestion' = archiveSuggestion fs
    let applyProperties' = applyProperties fs
    let finishDelete' = finishDelete fs
    let finishTransfer' = finishTransfer fs
    let finishPack' = finishPack fs
    let finishRename' = finishRename fs
    let isBareName' = FileSystemPath.isBareName fs
    let combine' = FileSystemPath.combine fs
    let nameOf' = FileSystemPath.name fs
    let spelled' = ArchivePath.spelled fs
    let readProperties' = EntryProperties.read fs
    let wouldPackItself' = Archive.wouldPackItself fs

    match msg with
    | CursorMoved cursor -> stay (setCursor cursor.Side cursor.Index state)
    | PanelFocused side -> stay (activate side state)
    | EntryOpened -> opened' state
    | WalkIntoRequested -> enter' state |> applyListing'
    | RefreshRequested -> refresh' state |> applyListing'
    | ExternalChangeDetected -> state |> readTabQuietly' |> stay
    | SelectionToggled direction -> stay (toggleSelectionMoving direction state.Active state)
    | SelectionToggledTo cursor -> stay (toggleSelectionThrough cursor.Side cursor.Index state)
    | SortRequested key -> stay (sortBy key state.Active state)
    | HiddenToggled -> toggleHidden' state.Active state |> applyListing'
    | SearchTyped character -> stay (typeIntoSearch state.Active character state)
    | SearchBackspaced -> stay (backspaceSearch state.Active state)
    | SearchCancelled -> stay (closeSearch state.Active state)
    | PatternRequested mode -> request state (PromptForPattern mode)
    | PatternEntered(mode, pattern) -> stay (applyPattern mode state.Active pattern state)
    | MakeDirectoryRequested ->
        match (activePanel state).Path with
        | OnDisk _ -> request state (PromptForName state.LastCreatedDirectory)
        | InArchive(archive, _) -> changing' [ archive ] (PromptForName state.LastCreatedDirectory)
    | NameEntered name ->
        match (activePanel state).Path with
        | OnDisk directory -> makeDirectory' state.Active directory name state |> applyOperation'
        | InArchive(archive, level) -> makeEntryDirectory' state.Active archive level name state |> applyOperation'
    | DeleteRequested mode ->
        let target = activePanel state

        let confirmed asked =
            ConfirmDelete
                { Targets = operationTargets target |> List.map _.Name
                  Scope = deleteScope' target
                  Mode = asked }

        match target.Path, operationTargets target with
        | _, [] -> stay state
        | OnDisk _, _ -> request state (confirmed mode)
        | InArchive(archive, _), _ -> changing' [ archive ] (confirmed Permanently)
    | DeleteConfirmed mode ->
        match transferSource' (activePanel state) with
        | None -> stay state
        | Some(FromDisk paths) -> request state (DeleteTargets(paths, mode))
        | Some(FromArchive(archive, entries)) -> request state (RemoveEntries(archive, entries))
    | DeleteFinished results -> finishDelete' results state |> applyBatch'
    | CopyRequested ->
        match transferSource' (activePanel state), transferTarget state with
        | None, _ -> stay state
        | Some(FromDisk paths), ToDirectory target -> request state (CopyTargets(paths, target))
        | Some(FromArchive(archive, entries)), ToDirectory target ->
            request state (ExtractEntries(archive, entries, target))
        | Some(FromDisk paths), ToArchive(archive, level) -> changing' [ archive ] (AddEntries(archive, level, paths))
        | Some(FromArchive(source, entries)), ToArchive(target, level) ->
            changing' [ target ] (CopyEntriesAcross(source, entries, target, level))
    | CopyFinished results -> finishTransfer' results state |> applyBatch'
    | MoveRequested ->
        match transferSource' (activePanel state), transferTarget state with
        | None, _ -> stay state
        | Some(FromDisk _), ToDirectory target -> request state (PromptForTarget target)
        | Some(FromArchive(archive, entries)), ToDirectory target ->
            changing' [ archive ] (MoveEntriesOut(archive, entries, target))
        | Some(FromDisk paths), ToArchive(archive, level) ->
            changing' [ archive ] (MoveEntriesIn(archive, level, paths))
        | Some(FromArchive(source, entries)), ToArchive(target, level) ->
            changing' [ target; source ] (MoveEntriesAcross(source, entries, target, level))
    | TargetEntered text ->
        match text.Trim(), operationPaths' (activePanel state), activeDirectory state with
        | "", _, _
        | _, [], _
        | _, _, None -> stay state
        | trimmed, [ single ], _ when isBareName' trimmed ->
            request state (RenameTarget(single, EntryName.create trimmed))
        | trimmed, paths, Some source -> request state (MoveTargets(paths, combine' source trimmed))
    | MoveFinished results -> finishTransfer' results state |> applyBatch'
    | PackRequested ->
        match operationTargets (activePanel state), archiveSuggestion' ArchiveFormat.initial state with
        | [], _
        | _, None -> stay state
        | _, Some suggestion -> request state (PromptForArchive suggestion)
    | ArchiveEntered choice ->
        match choice.Target.Trim(), operationPaths' (activePanel state), activeDirectory state with
        | "", _, _
        | _, [], _
        | _, _, None -> stay state
        | trimmed, paths, Some source ->
            let target = combine' source trimmed

            if wouldPackItself' target paths then
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
    | PackFinished(archive, results) -> finishPack' archive results state |> applyBatch'
    | UnpackRequested ->
        match operationPaths' (activePanel state), targetDirectory state with
        | [], _
        | _, None -> stay state
        | paths, Some target -> request state (ExtractTargets(paths, target))
    | UnpackFinished results -> finishTransfer' results state |> applyBatch'
    | RenameFinished(newName, outcome) ->
        match outcome with
        | Error error -> request state (ReportOperationError error)
        | Ok() -> finishRename' newName state |> applyOperation'
    | ViewRequested ->
        let target = activePanel state

        match cursorFilePath' target, cursorFileInArchive target with
        | Some path, _ -> request state (ViewFile path)
        | _, Some(archive, entry) -> request state (ViewArchiveEntry(archive, entry))
        | None, None -> stay state
    | EditRequested ->
        let target = activePanel state

        match cursorEntryInArchive target with
        | Some(archive, entry) ->
            request state (ReportOperationError(Operations.ArchiveEntryNotEditable(spelled' archive entry)))
        | None ->
            match cursorFilePath' target, cursorDirectoryPath' target with
            | Some path, _ -> request state (EditFile(path, programFor ForEditing (nameOf' path) state))
            | _, Some path ->
                match readProperties' path with
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
            match makeFile' state.Active directory name state with
            | Error error -> request state (ReportOperationError error)
            | Ok(updated, path) -> request updated (EditFile(path, programFor ForEditing name updated))
    | PropertiesEntered properties -> applyProperties' properties state |> applyOperation'
    | SettingsRequested ->
        let candidate =
            cursorExecutablePath' (activePanel state)
            |> Option.bind (FileSystemPath.value >> ProgramPath.tryCreate)

        request state (ShowSettings(state.Programs, candidate))
    | ProgramsEntered programs -> stay (withPrograms programs state)
    | EditFinished outcome ->
        match outcome with
        | Error(Operations.StartDeclined _) -> stay state
        | Error error -> request state (ReportOperationError error)
        | Ok() -> refresh' state |> applyListing'
    | LaunchFinished outcome ->
        match outcome with
        | Error(ElevationDeclined _) -> stay state
        | Error error -> request state (ReportLaunchError error)
        | Ok() -> stay state
    | OpenWithRequested -> openedWith' state
    | HandlerChosen(path, choice) -> chosen' path choice
    | ElevatedRunRequested -> runElevated' state
    | RootRequested side -> request state (PromptForRoot side)
    | PathOpened(side, path) -> loadPanel' side path state |> applyListing'
    | TabOpened -> openTab state |> saving'
    | TabClosed -> closeTab state |> saving'
    | TabsRequested -> state.Tabs |> ShowTabs |> request state
    | TabSwitched position -> switchTab position state |> standingOn'
    | TabCycled step -> cycleTab step state |> standingOn'
    | SessionSaveRequested -> state |> saving'
    | QuitRequested ->
        { State = state
          Effects = [ SaveSettings(settings' state); Quit ] }

let private watching before result =
    match watchedLocations result.State with
    | after when after = watchedLocations before -> result
    | after ->
        { result with
            Effects = result.Effects @ [ WatchLocations after ] }

let update (fs: IFileSystem) (msg: Message) (state: AppState) : UpdateResult =
    transition fs msg state |> watching state

let step (fs: IFileSystem) (msg: Message) (state: AppState) =
    let result = update fs msg state
    result.State, result.Effects
