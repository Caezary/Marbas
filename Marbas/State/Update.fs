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
    | DeleteScopeLoaded of scope: DeleteScope * mode: DeleteMode
    | DeleteConfirmed of DeleteMode
    | DeleteFinished of Operations.ItemResult list
    | CopyRequested
    | CopyFinished of Operations.ItemResult list
    | MoveRequested
    | TargetEntered of text: string
    | MoveFinished of Operations.ItemResult list
    | FindRequested
    | FindEntered of FindQuery
    | LastResultsRequested
    | SearchAnswered of FindAnswer
    | LinkRequested
    | LinkEntered of LinkChoice
    | LinksMade of made: FileSystemPath list * results: Operations.ItemResult list
    | PackRequested
    | ArchiveEntered of ArchiveChoice
    | PackFinished of archive: EntryName * results: Operations.ItemResult list
    | UnpackRequested
    | UnpackFinished of results: Operations.ItemResult list
    | RenameFinished of newName: EntryName * outcome: Result<unit, Operations.OperationError>
    | ViewRequested
    | MeasureFinished of measured: Result<Measurement list, Operations.OperationError>
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
    | VolumeRootRequested
    | PathOpened of PanelSide * FileSystemPath
    | DriveChosen of PanelSide * DriveRow
    | DriveHandedOver of DriveRow
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
    | InspectDelete of question: DeleteQuestion * mode: DeleteMode
    | ConfirmDelete of DeleteRequest
    | ArchiveTargets of sources: FileSystemPath list * request: ArchiveRequest
    | ExtractTargets of archives: FileSystemPath list * targetDirectory: FileSystemPath
    | CopyPlaces of groups: TransferSource list * target: TransferTarget
    | MovePlaces of groups: TransferSource list * target: TransferTarget
    | DeletePlaces of groups: TransferSource list * mode: DeleteMode
    | PromptForFind of ground: MarkedGround * hidden: HiddenEntries * lastResults: int option
    | RunSearch of query: FindQuery * location: PanelLocation * marked: PanelLocation list
    | ShowLastResults of results: FindResults * gone: int
    | MakeLinks of requests: LinkRequest list
    | RenameTarget of source: FileSystemPath * newName: EntryName
    | ViewFile of path: FileSystemPath
    | MeasureTargets of targets: MeasureTarget list
    | ViewArchiveEntry of archive: FileSystemPath * entry: ArchivePath
    | EditFile of path: FileSystemPath * editor: ProgramPath option
    | RunProgram of LaunchRequest
    | ShowHandlers of path: FileSystemPath * chosen: ProgramPath option
    | ShowProperties of EntryProperties
    | ShowSettings of ProgramSettings * candidate: ProgramPath option
    | PromptForTarget of suggestion: FileSystemPath
    | PromptForArchive of suggestion: FileSystemPath
    | PromptForLink of sources: FileSystemPath list * directory: FileSystemPath * suggestion: FileSystemPath
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

let private reporting state error = request state (ReportOperationError error)

let private changing (fs: IFileSystem) state archives effect =
    match archives |> List.tryFind (Archive.changeableKind fs >> Option.isNone) with
    | None -> request state effect
    | Some unchangeable ->
        KindCannotBeChanged unchangeable
        |> Operations.ArchiveFailed
        |> reporting state

let private places source =
    match source with
    | FromPlaces groups -> groups
    | group -> [ group ]

let private sourceArchives groups =
    groups
    |> List.choose (function
        | FromArchive(archive, _) -> Some archive
        | FromDisk _
        | FromPlaces _ -> None)

let private targetArchives target =
    match target with
    | ToArchive(archive, _) -> [ archive ]
    | ToDirectory _ -> []

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
    | Error error -> reporting state error

let private applyBatch state result =
    match result with
    | Error error -> reporting state error
    | Ok(updated, results) ->
        match Operations.failures results with
        | [] -> stay updated
        | failed -> request updated (ReportItemFailures failed)

let private sentToRoot (fs: IFileSystem) (state: AppState) =
    let panel = activePanel state
    let standing = PanelLocation.holding fs panel.Path

    match FileSystemPath.root fs standing with
    | Some root when OnDisk root <> panel.Path -> loadPanel fs state.Active root state |> applyListing state
    | _ -> stay state

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

let private handedOver state row =
    let root = DriveRow.root row
    running state root None root

let private opening (fs: IFileSystem) state directory path =
    match programFor ForOpening (FileSystemPath.name fs path) state with
    | Some program -> running state directory (Some program) path
    | None ->
        match ArchiveReader.kindOf fs path with
        | Some _ -> enter fs state |> applyListing state
        | None -> running state directory None path

let private opened (fs: IFileSystem) state =
    let panel = activePanel state

    match cursorFileInArchive fs panel with
    | Some(archive, entry) ->
        ArchivePath.spelled fs archive entry
        |> Operations.ArchiveEntryNotRunnable
        |> reporting state
    | None ->
        match cursorFilePath fs panel, cursorDirectory fs panel with
        | Some path, Some directory -> opening fs state directory path
        | _ -> enter fs state |> applyListing state

let private revealed (fs: IFileSystem) panel directory =
    match cursorEntry panel with
    | Some entry when entry.Name = ParentName -> Some directory
    | _ -> cursorDirectoryPath fs panel

let private refusingInArchive (fs: IFileSystem) state panel archive level =
    let named =
        cursorEntryInArchive fs panel
        |> Option.map snd
        |> Option.defaultValue level

    ArchivePath.spelled fs archive named
    |> Operations.ArchiveEntryNotRunnable
    |> reporting state

let private openedWith (fs: IFileSystem) state =
    let panel = activePanel state

    match panel.Path with
    | InArchive(archive, level) -> refusingInArchive fs state panel archive level
    | SearchResults _ -> stay state
    | OnDisk directory ->
        match cursorFilePath fs panel with
        | Some path -> request state (ShowHandlers(path, programFor ForOpening (FileSystemPath.name fs path) state))
        | None ->
            revealed fs panel directory
            |> Option.map (running state directory None)
            |> Option.defaultValue (stay state)

let private runElevated (fs: IFileSystem) state =
    let panel = activePanel state

    match panel.Path with
    | InArchive(archive, level) -> refusingInArchive fs state panel archive level
    | SearchResults _ -> stay state
    | OnDisk directory ->
        match cursorExecutablePath fs panel with
        | Some path -> startingAs AsAdministrator state directory None path
        | None ->
            cursorFilePath fs panel
            |> Option.orElseWith (fun () -> cursorDirectoryPath fs panel)
            |> Option.map (NotAProgram >> ReportLaunchError >> request state)
            |> Option.defaultValue (stay state)

let private chosen (fs: IFileSystem) state path (choice: OpenWithChoice) =
    let remembered =
        match FileExtension.ofName (FileSystemPath.name fs path) with
        | Some extension when choice.Remember ->
            withPrograms (ProgramSettings.associate ForOpening extension choice.Program state.Programs) state
        | _ -> state

    match activeDirectory remembered with
    | Some directory -> running remembered directory (Some choice.Program) path
    | None -> stay remembered

let private refusing state refusal =
    Operations.NotInSearchResults refusal |> reporting state

let private showingProperties state properties =
    match properties with
    | Ok found -> request state (ShowProperties found)
    | Error error -> reporting state error

let private editing (fs: IFileSystem) state =
    let target = activePanel state

    match cursorEntryInArchive fs target with
    | Some(archive, entry) ->
        ArchivePath.spelled fs archive entry
        |> Operations.ArchiveEntryNotEditable
        |> reporting state
    | None ->
        match cursorFilePath fs target, cursorDirectoryPath fs target with
        | Some path, _ -> request state (EditFile(path, programFor ForEditing (FileSystemPath.name fs path) state))
        | _, Some path -> EntryProperties.read fs path |> showingProperties state
        | None, None -> stay state

let private forGood mode =
    match mode with
    | ToTrash -> Permanently
    | Permanently
    | ByWiping -> mode

let private transition (fs: IFileSystem) (msg: Message) (state: AppState) : UpdateResult =
    match msg with
    | CursorMoved cursor -> stay (setCursor cursor.Side cursor.Index state)
    | PanelFocused side -> stay (activate side state)
    | EntryOpened -> opened fs state
    | WalkIntoRequested -> enter fs state |> applyListing state
    | RefreshRequested -> refresh fs state |> applyListing state
    | ExternalChangeDetected -> state |> readTabQuietly fs |> stay
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
        | SearchResults _ -> stay state
    | NameEntered name ->
        match (activePanel state).Path with
        | OnDisk directory -> makeDirectory fs state.Active directory name state |> applyOperation state
        | InArchive(archive, level) ->
            makeEntryDirectory fs state.Active archive level name state |> applyOperation state
        | SearchResults _ -> stay state
    | DeleteRequested mode ->
        let target = activePanel state

        let inspected asked =
            InspectDelete(deleteQuestion fs target, asked)

        match target.Path, transferSource fs target with
        | _, None -> stay state
        | InArchive(archive, _), _
        | SearchResults _, Some(FromArchive(archive, _)) -> changing fs state [ archive ] (inspected Permanently)
        | _, Some(FromPlaces _) -> request state (inspected (forGood mode))
        | _ -> request state (inspected mode)
    | DeleteScopeLoaded(scope, mode) ->
        request
            state
            (ConfirmDelete
                { Targets = operationTargets (activePanel state) |> List.map _.Name
                  Scope = scope
                  Mode = mode })
    | DeleteConfirmed mode ->
        match transferSource fs (activePanel state) with
        | None -> stay state
        | Some source -> request state (DeletePlaces(places source, mode))
    | DeleteFinished results -> finishDelete fs results state |> applyBatch state
    | CopyRequested ->
        match transferSource fs (activePanel state), transferTarget state with
        | None, _
        | _, None -> stay state
        | Some source, Some target -> changing fs state (targetArchives target) (CopyPlaces(places source, target))
    | CopyFinished results -> finishTransfer fs results state |> applyBatch state
    | MoveRequested ->
        match transferSource fs (activePanel state), transferTarget state with
        | None, _
        | _, None -> stay state
        | Some(FromArchive _ as group), Some(ToDirectory target) ->
            changing fs state (sourceArchives [ group ]) (MovePlaces([ group ], ToDirectory target))
        | Some _, Some(ToDirectory target) -> request state (PromptForTarget target)
        | Some source, Some target ->
            let groups = places source

            changing fs state (targetArchives target @ sourceArchives groups) (MovePlaces(groups, target))
    | TargetEntered text ->
        let panel = activePanel state

        let standing =
            match panel.Path with
            | SearchResults _ -> Some(PanelLocation.holding fs panel.Path)
            | location -> PanelLocation.directory location

        match text.Trim(), transferSource fs panel, standing with
        | "", _, _
        | _, None, _
        | _, _, None -> stay state
        | trimmed, _, _ when showsResults panel && FileSystemPath.isBareName fs trimmed -> refusing state Renaming
        | trimmed, Some(FromDisk [ single ]), _ when FileSystemPath.isBareName fs trimmed ->
            request state (RenameTarget(single, EntryName.create trimmed))
        | _, Some(FromArchive _), _ -> stay state
        | trimmed, Some source, Some place ->
            request state (MovePlaces(places source, ToDirectory(FileSystemPath.combine fs place trimmed)))
    | MoveFinished results -> finishTransfer fs results state |> applyBatch state
    | FindRequested ->
        let panel = activePanel state

        let found = state.LastSearch |> Option.map (_.Hits >> List.length)

        request state (PromptForFind(markedGround panel, panel.Hidden, found))
    | FindEntered query ->
        let panel = activePanel state

        request state (RunSearch(query, PanelLocation.origin panel.Path, markedDirectories fs panel))
    | LastResultsRequested ->
        match state.LastSearch with
        | None -> stay state
        | Some results ->
            let surviving, gone = FindResults.surviving fs results

            request (withLastSearch surviving state) (ShowLastResults(surviving, gone))
    | SearchAnswered answer ->
        let remembered = withLastSearch answer.Results state

        match answer.Landing with
        | None -> stay remembered
        | Some(AtHit hit) -> standOnHit fs state.Active hit remembered |> applyListing remembered
        | Some(AtDirectory location) -> standInside fs state.Active location remembered |> applyListing remembered
        | Some InPanel -> showResults fs state.Active answer.Results remembered |> applyListing remembered
    | LinkRequested ->
        let panel = activePanel state

        match operationPaths fs panel, activeDirectory state with
        | [], _
        | _, None -> stay state
        | sources, Some directory ->
            let place = targetDirectory state |> Option.defaultValue directory

            let suggestion =
                match sources with
                | [ single ] -> FileSystemPath.combine fs place (EntryName.value (FileSystemPath.name fs single))
                | _ -> place

            request state (PromptForLink(sources, directory, suggestion))
    | LinkEntered choice ->
        match activeDirectory state with
        | None -> stay state
        | Some directory ->
            match Links.requests fs choice.Kind (operationPaths fs (activePanel state)) directory choice.Target with
            | [] -> stay state
            | requests -> request state (MakeLinks requests)
    | LinksMade(made, results) -> finishLinks fs made results state |> applyBatch state
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
                Operations.TargetWithinSource target |> reporting state
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
        | Error error -> reporting state error
        | Ok() -> finishRename fs newName state |> applyOperation state
    | ViewRequested ->
        let target = activePanel state

        match cursorFilePath fs target, cursorFileInArchive fs target with
        | Some path, _ -> request state (ViewFile path)
        | _, Some(archive, entry) -> request state (ViewArchiveEntry(archive, entry))
        | None, None ->
            match measureTargets fs target with
            | [] -> stay state
            | targets -> request state (MeasureTargets targets)
    | MeasureFinished(Ok measured) -> stay (applyMeasurements state.Active measured state)
    | MeasureFinished(Error error) -> reporting state error
    | EditRequested -> editing fs state
    | NewFileRequested ->
        match activeDirectory state with
        | None -> stay state
        | Some _ -> request state PromptForFileName
    | FileNameEntered name ->
        match activeDirectory state with
        | None -> stay state
        | Some directory ->
            match makeFile fs state.Active directory name state with
            | Error error -> reporting state error
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
        | Error error -> reporting state error
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
    | VolumeRootRequested -> sentToRoot fs state
    | PathOpened(side, path) -> loadPanel fs side path state |> applyListing state
    | DriveChosen(side, row) -> loadPanel fs side (driveLanding fs side row state) state |> applyListing state
    | DriveHandedOver row -> handedOver state row
    | TabOpened -> openTab fs state |> saving fs
    | TabClosed -> closeTab state |> saving fs
    | TabsRequested -> state.Tabs |> ShowTabs |> request state
    | TabSwitched position -> switchTab position state |> standingOn fs
    | TabCycled step -> cycleTab step state |> standingOn fs
    | SessionSaveRequested -> state |> saving fs
    | QuitRequested ->
        { State = state
          Effects = [ SaveSettings(settings fs state); Quit ] }

let private watching before result =
    match watchedLocations result.State with
    | after when after = watchedLocations before -> result
    | after ->
        { result with
            Effects = result.Effects @ [ WatchLocations after ] }

let private refusedAsSource msg =
    match msg with
    | MakeDirectoryRequested -> Some MakingDirectory
    | NewFileRequested -> Some MakingFile
    | LinkRequested -> Some Linking
    | PackRequested -> Some Packing
    | UnpackRequested -> Some Unpacking
    | _ -> None

let private refusedAsTarget msg =
    match msg with
    | CopyRequested
    | MoveRequested
    | PackRequested
    | UnpackRequested -> Some Receiving
    | _ -> None

let private resultsRefusal (state: AppState) msg =
    match refusedAsSource msg, refusedAsTarget msg with
    | Some refusal, _ when showsResults (activePanel state) -> Some refusal
    | _, Some refusal when showsResults (panel (opposite state.Active) state) -> Some refusal
    | _ -> None

let update (fs: IFileSystem) (msg: Message) (state: AppState) : UpdateResult =
    let result =
        match resultsRefusal state msg with
        | Some refusal -> refusing state refusal
        | None -> transition fs msg state

    result |> watching state

let step (fs: IFileSystem) (msg: Message) (state: AppState) =
    let result = update fs msg state
    result.State, result.Effects
