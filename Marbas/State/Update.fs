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

let update (fs: IFileSystem) (msg: Message) (state: AppState) : UpdateResult =
    match msg with
    | CursorMoved cursor -> stay (setCursor cursor.Side cursor.Index state)
    | PanelFocused side -> stay (activate side state)
    | EntryOpened -> opened fs state
    | WalkIntoRequested -> enter fs state |> applyListing state
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
        | InArchive(archive, _) -> changing fs state [ archive ] (PromptForName state.LastCreatedDirectory)
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
        | InArchive(archive, _), _ -> changing fs state [ archive ] (confirmed Permanently)
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
            changing fs state [ archive ] (AddEntries(archive, level, paths))
        | Some(FromArchive(source, entries)), ToArchive(target, level) ->
            changing fs state [ target ] (CopyEntriesAcross(source, entries, target, level))
    | CopyFinished results -> finishTransfer fs results state |> applyBatch state
    | MoveRequested ->
        match transferSource fs (activePanel state), transferTarget state with
        | None, _ -> stay state
        | Some(FromDisk _), ToDirectory target -> request state (PromptForTarget target)
        | Some(FromArchive(archive, entries)), ToDirectory target ->
            changing fs state [ archive ] (MoveEntriesOut(archive, entries, target))
        | Some(FromDisk paths), ToArchive(archive, level) ->
            changing fs state [ archive ] (MoveEntriesIn(archive, level, paths))
        | Some(FromArchive(source, entries)), ToArchive(target, level) ->
            changing fs state [ target; source ] (MoveEntriesAcross(source, entries, target, level))
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
            | Some path, _ -> request state (EditFile(path, programFor ForEditing (FileSystemPath.name fs path) state))
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
            | Ok(updated, path) -> request updated (EditFile(path, programFor ForEditing name updated))
    | PropertiesEntered properties -> applyProperties fs properties state |> applyOperation state
    | SettingsRequested ->
        let candidate =
            cursorExecutablePath fs (activePanel state)
            |> Option.bind (FileSystemPath.value >> ProgramPath.tryCreate)

        request state (ShowSettings(state.Programs, candidate))
    | ProgramsEntered programs -> stay (withPrograms programs state)
    | EditFinished outcome ->
        match outcome with
        | Error(Operations.StartDeclined _) -> stay state
        | Error error -> request state (ReportOperationError error)
        | Ok() -> refresh fs state |> applyListing state
    | LaunchFinished outcome ->
        match outcome with
        | Error(ElevationDeclined _) -> stay state
        | Error error -> request state (ReportLaunchError error)
        | Ok() -> stay state
    | OpenWithRequested -> openedWith fs state
    | HandlerChosen(path, choice) -> chosen fs state path choice
    | ElevatedRunRequested -> runElevated fs state
    | RootRequested side -> request state (PromptForRoot side)
    | PathOpened(side, path) -> loadPanel fs side path state |> applyListing state
    | TabOpened -> openTab state |> saving fs
    | TabClosed -> closeTab state |> saving fs
    | TabsRequested -> state.Tabs |> ShowTabs |> request state
    | TabSwitched position -> switchTab position state |> standingOn fs
    | TabCycled step -> cycleTab step state |> standingOn fs
    | SessionSaveRequested -> state |> saving fs
    | QuitRequested ->
        { State = state
          Effects = [ SaveSettings(settings fs state); Quit ] }

let step (fs: IFileSystem) (msg: Message) (state: AppState) =
    let result = update fs msg state
    result.State, result.Effects
