module Marbas.State.Update

open System.IO.Abstractions
open Marbas.Collections
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Locations
open Marbas.Find
open Marbas.FileSystem.DirectoryListing
open Marbas.Messages
open Marbas.State.AppState

type Message =
    | CursorMoved of PanelCursor
    | PanelFocused of PanelSide
    | EntryOpened
    | WalkIntoRequested
    | UpOneLevelRequested
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
    | LikenessRequested of SelectionMode * Likeness
    | AllFilesRequested of SelectionMode
    | InversionRequested
    | MarksRecalled
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
    | EditorStarted of outcome: Result<unit, Operations.OperationError>
    | LaunchFinished of request: LaunchRequest * outcome: Result<unit, LaunchError>
    | UserMenuRequested
    | MenuCommandChosen of command: MenuCommand * listFile: FileSystemPath
    | CommandFinished of run: CommandRun * outcome: Result<unit, LaunchError>
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
    | HistoryChanged of History
    | HistoryUnwritten of HistoryError
    | HistoryRequested
    | HistoryAnswered of HistoryAnswer
    | FavoriteKeepRequested
    | FavoriteRequested of FavoriteSlot
    | FavoriteFound of Place
    | FavoritesRequested
    | NamesCopyRequested
    | PathsCopyRequested
    | LinkTargetsCopyRequested
    | ClipboardPathRequested
    | ClipboardPathRead of FileSystemPath
    | ClipboardRefused of ClipboardError
    | FilesOnClipboardRequested of ClipboardIntent
    | ClipboardExtractFinished of
        groups: TransferSource list *
        scratch: FileSystemPath *
        results: Operations.ItemResult list
    | FilesPasteRequested
    | ClipboardFilesRead of files: ClipboardFiles * target: TransferTarget
    | QuitRequested

type Effect =
    | Report of Problem
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
    | RunCommand of CommandRun
    | ShowUserMenu
    | ShowHandlers of path: FileSystemPath * chosen: ProgramPath option
    | ShowProperties of EntryProperties
    | ShowSettings of ProgramSettings * candidate: ProgramPath option
    | PromptForTarget of suggestion: FileSystemPath
    | PromptForArchive of suggestion: FileSystemPath
    | PromptForLink of sources: FileSystemPath list * directory: FileSystemPath * suggestion: FileSystemPath
    | PromptForName
    | PromptForFileName
    | PromptForPattern of SelectionMode
    | PromptForRoot of PanelSide
    | ShowTabs of Zipper<Tab>
    | WatchLocations of paths: FileSystemPath list
    | ChangeHistory of HistoryChange
    | ShowHistory of HistoryTab
    | PromptForFavoriteSlot of Place
    | LookUpFavorite of FavoriteSlot
    | ShowFavorites of current: Place option
    | PutOnClipboard of ClipboardText
    | ReadClipboardPath of directory: FileSystemPath
    | PutFilesOnClipboard of ClipboardFiles
    | ExtractForClipboard of groups: TransferSource list
    | ReadClipboardFiles of target: TransferTarget
    | MovePastedFiles of files: ClipboardFiles * target: TransferTarget
    | SweepLeftovers
    | SaveSettings of Settings
    | Quit

type UpdateResult =
    { State: AppState
      Effects: Effect list }

let private stay state = { State = state; Effects = [] }

let private request state effect = { State = state; Effects = [ effect ] }

let private reporting state error =
    request state (Report(Problem.Operation error))

let private requestUnlessRefused state refusals effect =
    match List.tryPick id refusals with
    | None -> request state effect
    | Some error -> reporting state error

let private unchangeableArchive (fs: IFileSystem) archives =
    archives
    |> List.tryFind (Archive.changeableKind fs >> Option.isNone)
    |> Option.map (KindCannotBeChanged >> Operations.ArchiveFailed)

let private requestIfChangeable (fs: IFileSystem) state archives effect =
    requestUnlessRefused state [ unchangeableArchive fs archives ] effect

let private places source =
    match source with
    | FromPlaces groups -> groups
    | group -> [ group ]

let private diskPaths groups =
    groups
    |> List.collect (function
        | FromDisk paths -> paths
        | FromArchive _
        | FromPlaces _ -> [])

let private landingPlace target =
    match target with
    | ToDirectory directory -> directory
    | ToArchive(archive, _) -> archive

let private landingWithinSource (fs: IFileSystem) groups target =
    let landing = landingPlace target

    if Operations.landsWithinSource fs landing (diskPaths groups) then
        Some(Operations.TargetWithinSource landing)
    else
        None

let private requestIfOutsideSource (fs: IFileSystem) state groups target archives effect =
    requestUnlessRefused state [ landingWithinSource fs groups target; unchangeableArchive fs archives ] effect

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

let private rereadAndSave (fs: IFileSystem) state =
    match readTabAgain fs state with
    | Ok updated -> saving fs updated
    | Error error ->
        { State = state
          Effects = [ SaveSettings(settings fs state); Report(Problem.Listing error) ] }

let private applyListing state result =
    match result with
    | Ok updated -> stay updated
    | Error error -> request state (Report(Problem.Listing error))

let private landingOn (fs: IFileSystem) (state: AppState) landing =
    match landing with
    | Into location -> standInside fs state.Active location state
    | Beside(location, name) -> standBeside fs state.Active location name state
    |> applyListing state

let private landingIfAnywhere (fs: IFileSystem) state landing =
    landing |> Option.map (landingOn fs state) |> Option.defaultValue (stay state)

let private applyListingWithRefusals state result =
    match result with
    | Ok(updated, []) -> stay updated
    | Ok(updated, refused) -> request updated (Report(Problem.UnverifiedHits refused))
    | Error error -> request state (Report(Problem.Listing error))

let private applyOperation state result =
    match result with
    | Ok updated -> stay updated
    | Error error -> reporting state error

let private applyMade name state result =
    match result with
    | Error error -> reporting state error
    | Ok updated ->
        { State = updated
          Effects =
            EnteredText.tryCreate (EntryName.value name)
            |> Option.map (fun typed -> ChangeHistory(Remember(Typed(NewDirectory, typed))))
            |> Option.toList }

let private applyBatch state result =
    match result with
    | Error error -> reporting state error
    | Ok(updated, results) ->
        match Operations.failures results with
        | [] -> stay updated
        | failed -> request updated (Report(Problem.ItemFailures failed))

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

let private openFile (fs: IFileSystem) state directory path =
    match programFor ForOpening (FileSystemPath.name fs path) state with
    | Some program -> running state directory (Some program) path
    | None ->
        match ArchiveReader.kindOf fs path with
        | Some _ -> enter fs state |> applyListing state
        | None -> running state directory None path

let private openCursorEntry (fs: IFileSystem) state =
    let panel = activePanel state

    match PanelState.cursorFileInArchive panel with
    | Some(archive, entry) ->
        ArchivePath.spelled fs archive entry
        |> Operations.ArchiveEntryNotRunnable
        |> reporting state
    | None ->
        match cursorFilePath fs panel, cursorDirectory fs panel with
        | Some path, Some directory -> openFile fs state directory path
        | _ -> enter fs state |> applyListing state

let private revealed (fs: IFileSystem) panel directory =
    match PanelState.cursorEntry panel with
    | Some entry when entry.Name = ParentName -> Some directory
    | _ -> cursorDirectoryPath fs panel

let private reportNotRunnable (fs: IFileSystem) state panel archive level =
    let named =
        PanelState.cursorEntryInArchive panel
        |> Option.map snd
        |> Option.defaultValue level

    ArchivePath.spelled fs archive named
    |> Operations.ArchiveEntryNotRunnable
    |> reporting state

let private openWithHandler (fs: IFileSystem) state =
    let panel = activePanel state

    match panel.Path with
    | InArchive(archive, level) -> reportNotRunnable fs state panel archive level
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
    | InArchive(archive, level) -> reportNotRunnable fs state panel archive level
    | SearchResults _ -> stay state
    | OnDisk directory ->
        match cursorExecutablePath fs panel with
        | Some path -> startingAs AsAdministrator state directory None path
        | None ->
            cursorFilePath fs panel
            |> Option.orElseWith (fun () -> cursorDirectoryPath fs panel)
            |> Option.map (NotAProgram >> Problem.Launch >> Report >> request state)
            |> Option.defaultValue (stay state)

let private chosen (fs: IFileSystem) state path (choice: OpenWithChoice) =
    let remembered =
        match FileExtension.ofName (FileSystemPath.name fs path), choice.Use with
        | Some extension, AlwaysThisWay ->
            withPrograms (ProgramSettings.associate ForOpening extension choice.Program state.Programs) state
        | _ -> state

    match activeDirectory remembered with
    | Some directory -> running remembered directory (Some choice.Program) path
    | None -> stay remembered

let private reportResultsRefusal state refusal =
    Operations.NotInSearchResults refusal |> reporting state

let private showingProperties state properties =
    match properties with
    | Ok found -> request state (ShowProperties found)
    | Error error -> reporting state error

let private notEditable (fs: IFileSystem) archive entry =
    ArchivePath.spelled fs archive entry |> Operations.ArchiveEntryNotEditable

let private reopening (fs: IFileSystem) state (visit: FileVisit) =
    match visit.Where, visit.How with
    | Place.OnDisk path, Viewed -> Ok(ViewFile path)
    | Place.InArchive(archive, entry), Viewed -> Ok(ViewArchiveEntry(archive, entry))
    | Place.OnDisk path, Edited -> Ok(EditFile(path, programFor ForEditing (FileSystemPath.name fs path) state))
    | Place.InArchive(archive, entry), Edited -> Error(notEditable fs archive entry)

let private stillOnDisk (fs: IFileSystem) place =
    match place with
    | Place.OnDisk path
    | Place.InArchive(path, _) -> FileSystemPath.kind fs path = FileEntry

let private visiting (fs: IFileSystem) state (visit: FileVisit) =
    match reopening fs state visit with
    | Error error -> reporting state error
    | Ok effect when stillOnDisk fs visit.Where ->
        { State = state
          Effects = [ ChangeHistory(Remember(Opened visit)); effect ] }
    | Ok effect -> request state effect

let private editing (fs: IFileSystem) state =
    let target = activePanel state

    match PanelState.cursorEntryInArchive target with
    | Some(archive, entry) -> notEditable fs archive entry |> reporting state
    | None ->
        match cursorFilePath fs target, cursorDirectoryPath fs target with
        | Some path, _ ->
            visiting
                fs
                state
                { Where = Place.OnDisk path
                  How = Edited }
        | _, Some path -> EntryProperties.read fs path |> showingProperties state
        | None, None -> stay state

let private runningFromMenu (fs: IFileSystem) state (command: MenuCommand) listFile =
    match CommandExpansion.expand fs listFile command.Line state with
    | Error refusal -> request state (Report(Problem.Expansion refusal))
    | Ok expanded ->
        { Label = command.Label
          Command = expanded
          Run = command.Run
          Elevation = command.Elevation
          WorkingDirectory = PanelLocation.holding fs (activePanel state).Path }
        |> RunCommand
        |> request state

let private chosenFromHistory (fs: IFileSystem) state choice =
    match choice with
    | None -> stay state
    | Some(OpenPlace place) -> Landing.inside place |> landingOn fs state
    | Some(RunAgain(StartedFile started)) -> request state (RunProgram started)
    | Some(RunAgain(StartedCommand run)) -> request state (RunCommand run)
    | Some(Revisit visit) -> visiting fs state visit
    | Some(GoTo place) -> Landing.beside fs place |> landingIfAnywhere fs state

let private copyingText state lines =
    ClipboardText.tryCreate lines
    |> Option.map (PutOnClipboard >> request state)
    |> Option.defaultValue (stay state)

let private archiveEntries groups =
    groups
    |> List.collect (function
        | FromArchive(archive, entries) -> entries |> List.map (fun entry -> archive, entry)
        | FromDisk _
        | FromPlaces _ -> [])

let private handingOver intent paths =
    ClipboardFiles.tryCreate intent paths
    |> Option.map PutFilesOnClipboard
    |> Option.toList

let private filesOnClipboard (fs: IFileSystem) state intent groups =
    match archiveEntries groups, intent with
    | [], _ ->
        { State = state
          Effects = handingOver intent (diskPaths groups) }
    | (archive, entry) :: _, ForMoving ->
        ArchivePath.spelled fs archive entry
        |> Operations.ArchiveEntryNotCut
        |> reporting state
    | _, ForCopying -> request state (ExtractForClipboard groups)

let private landedFile (fs: IFileSystem) scratch (item: Operations.ItemResult) =
    match item.Outcome, item.Source with
    | Ok(), Operations.ItemInArchive(archive, entry) -> Some(Archive.landedAs fs archive scratch entry)
    | _ -> None

let private extractedForClipboard (fs: IFileSystem) state groups scratch results =
    let landed = results |> List.choose (landedFile fs scratch)

    let reported =
        match Operations.failures results with
        | [] -> []
        | failed -> [ Report(Problem.ItemFailures failed) ]

    { State = state
      Effects = handingOver ForCopying (diskPaths groups @ landed) @ reported }

let private forGood mode =
    match mode with
    | ToTrash -> Permanently
    | Permanently
    | ByWiping -> mode

let private askingForDirectoryName (fs: IFileSystem) state =
    match (activePanel state).Path with
    | OnDisk _ -> request state PromptForName
    | InArchive(archive, _) -> requestIfChangeable fs state [ archive ] PromptForName
    | SearchResults _ -> stay state

let private makingDirectory (fs: IFileSystem) state name =
    match (activePanel state).Path with
    | OnDisk directory -> makeDirectory fs state.Active directory name state |> applyMade name state
    | InArchive(archive, level) ->
        makeEntryDirectory fs state.Active archive level name state
        |> applyMade name state
    | SearchResults _ -> stay state

let private inspectingDelete (fs: IFileSystem) state mode =
    let target = activePanel state

    let inspected asked =
        InspectDelete(deleteQuestion fs target, asked)

    match target.Path, transferSource fs target with
    | _, None -> stay state
    | InArchive(archive, _), _
    | SearchResults _, Some(FromArchive(archive, _)) -> requestIfChangeable fs state [ archive ] (inspected Permanently)
    | _, Some(FromPlaces _) -> request state (inspected (forGood mode))
    | _ -> request state (inspected mode)

let private confirmingDelete state scope mode =
    { Targets = PanelState.operationTargets (activePanel state) |> List.map _.Name
      Scope = scope
      Mode = mode }
    |> ConfirmDelete
    |> request state

let private deleting (fs: IFileSystem) state mode =
    match transferSource fs (activePanel state) with
    | None -> stay state
    | Some source -> request state (DeletePlaces(places source, mode))

let private copying (fs: IFileSystem) state =
    match transferSource fs (activePanel state), transferTarget state with
    | None, _
    | _, None -> stay state
    | Some source, Some target ->
        let groups = places source

        requestIfOutsideSource fs state groups target (targetArchives target) (CopyPlaces(groups, target))

let private moving (fs: IFileSystem) state =
    match transferSource fs (activePanel state), transferTarget state with
    | None, _
    | _, None -> stay state
    | Some(FromArchive _ as group), Some(ToDirectory target) ->
        requestIfChangeable fs state (sourceArchives [ group ]) (MovePlaces([ group ], ToDirectory target))
    | Some _, Some(ToDirectory target) -> request state (PromptForTarget target)
    | Some source, Some target ->
        let groups = places source

        let archives = targetArchives target @ sourceArchives groups

        requestIfOutsideSource fs state groups target archives (MovePlaces(groups, target))

let private renamingOrMoving (fs: IFileSystem) state (text: string) =
    let panel = activePanel state

    let standing =
        match panel.Path with
        | SearchResults _ -> Some(PanelLocation.holding fs panel.Path)
        | location -> PanelLocation.directory location

    let trimmed = text.Trim()
    let renamesInPlace = FileSystemPath.isBareName fs trimmed

    match trimmed, transferSource fs panel, standing with
    | "", _, _
    | _, None, _
    | _, _, None -> stay state
    | _ when renamesInPlace && PanelState.showsResults panel -> reportResultsRefusal state Renaming
    | _, Some(FromDisk [ single ]), _ when renamesInPlace ->
        request state (RenameTarget(single, EntryName.create trimmed))
    | _, Some(FromArchive _), _ -> stay state
    | _, Some source, Some place ->
        let groups = places source
        let target = ToDirectory(FileSystemPath.combine fs place trimmed)

        requestIfOutsideSource fs state groups target [] (MovePlaces(groups, target))

let private askingToFind state =
    let panel = activePanel state

    let found = state.LastSearch |> Option.map (_.Hits >> List.length)

    request state (PromptForFind(PanelState.markedGround panel, panel.Hidden, found))

let private searching (fs: IFileSystem) state query =
    let panel = activePanel state

    request state (RunSearch(query, PanelLocation.origin panel.Path, markedDirectories fs panel))

let private showingLastResults (fs: IFileSystem) state =
    match state.LastSearch with
    | None -> stay state
    | Some results ->
        let surviving, gone = FindResults.surviving fs results

        request (withLastSearch surviving state) (ShowLastResults(surviving, gone))

let private landingFromSearch (fs: IFileSystem) state (answer: FindAnswer) =
    let remembered = withLastSearch answer.Results state

    match answer.Landing with
    | None -> stay remembered
    | Some(AtHit hit) -> standOnHit fs state.Active hit remembered |> applyListing remembered
    | Some(At landing) -> landingOn fs remembered landing
    | Some InPanel ->
        showResults fs state.Active answer.Results remembered
        |> applyListingWithRefusals remembered

let private askingForLink (fs: IFileSystem) state =
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

let private linking (fs: IFileSystem) state (choice: LinkChoice) =
    match activeDirectory state with
    | None -> stay state
    | Some directory ->
        match Links.requests fs choice.Kind (operationPaths fs (activePanel state)) directory choice.Target with
        | [] -> stay state
        | requests -> request state (MakeLinks requests)

let private askingForArchive (fs: IFileSystem) state =
    match PanelState.operationTargets (activePanel state), archiveSuggestion fs ArchiveFormat.initial state with
    | [], _
    | _, None -> stay state
    | _, Some suggestion -> request state (PromptForArchive suggestion)

let private packing (fs: IFileSystem) state (choice: ArchiveChoice) =
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

let private unpacking (fs: IFileSystem) state =
    match operationPaths fs (activePanel state), targetDirectory state with
    | [], _
    | _, None -> stay state
    | paths, Some target -> request state (ExtractTargets(paths, target))

let private renamed (fs: IFileSystem) state newName outcome =
    match outcome with
    | Error error -> reporting state error
    | Ok() -> finishRename fs newName state |> applyOperation state

let private viewing (fs: IFileSystem) state =
    let target = activePanel state

    match cursorFilePath fs target, PanelState.cursorFileInArchive target with
    | Some path, _ ->
        visiting
            fs
            state
            { Where = Place.OnDisk path
              How = Viewed }
    | _, Some(archive, entry) ->
        visiting
            fs
            state
            { Where = Place.InArchive(archive, entry)
              How = Viewed }
    | None, None ->
        match measureTargets fs target with
        | [] -> stay state
        | targets -> request state (MeasureTargets targets)

let private askingForFileName state =
    match activeDirectory state with
    | None -> stay state
    | Some _ -> request state PromptForFileName

let private makingFile (fs: IFileSystem) state name =
    match activeDirectory state with
    | None -> stay state
    | Some directory ->
        match makeFile fs state.Active directory name state with
        | Error error -> reporting state error
        | Ok(updated, path) ->
            visiting
                fs
                updated
                { Where = Place.OnDisk path
                  How = Edited }

let private showingSettings (fs: IFileSystem) state =
    let candidate =
        cursorExecutablePath fs (activePanel state)
        |> Option.bind (FileSystemPath.value >> ProgramPath.tryCreate)

    request state (ShowSettings(state.Programs, candidate))

let private launched (fs: IFileSystem) state (started: LaunchRequest) outcome =
    match outcome with
    | Error(ElevationDeclined _) -> stay state
    | Error error -> request state (Report(Problem.Launch error))
    | Ok() when FileSystemPath.kind fs started.Target = FileEntry ->
        request state (ChangeHistory(Remember(Launched started)))
    | Ok() -> stay state

let private reportingHistoryOnce state error =
    match state.HistoryReport with
    | Unreported -> request { state with HistoryReport = Reported } (Report(Problem.History error))
    | Reported -> stay state

let private askingForFavoriteSlot state =
    match Place.ofLocation (activePanel state).Path with
    | Some place -> request state (PromptForFavoriteSlot place)
    | None -> stay state

let private copyingNames state =
    PanelState.operationNames (activePanel state)
    |> List.map EntryName.value
    |> copyingText state

let private copyingPaths (fs: IFileSystem) state =
    activePanel state
    |> fullPathsToCopy fs
    |> List.map FileSystemPath.value
    |> copyingText state

let private copyingLinkTargets (fs: IFileSystem) state =
    activePanel state
    |> placesLedTo fs
    |> List.map FileSystemPath.value
    |> copyingText state

let private readingClipboardPath (fs: IFileSystem) state =
    PanelLocation.holding fs (activePanel state).Path
    |> ReadClipboardPath
    |> request state

let private reportingClipboardOnce state =
    match state.ClipboardReport with
    | Unreported ->
        request
            { state with
                ClipboardReport = Reported }
            (Report(Problem.Clipboard ClipboardUnavailable))
    | Reported -> stay state

let private puttingFilesOnClipboard (fs: IFileSystem) state intent =
    match transferSource fs (activePanel state) with
    | Some source -> filesOnClipboard fs state intent (places source)
    | None -> stay state

let private readingClipboardFiles (fs: IFileSystem) state =
    match (activePanel state).Path with
    | OnDisk directory -> request state (ReadClipboardFiles(ToDirectory directory))
    | InArchive(archive, level) ->
        requestIfChangeable fs state [ archive ] (ReadClipboardFiles(ToArchive(archive, level)))
    | SearchResults _ -> stay state

let private pasting (fs: IFileSystem) state files target =
    let groups = [ FromDisk(ClipboardFiles.paths files) ]

    match ClipboardFiles.intent files with
    | ForCopying -> requestIfOutsideSource fs state groups target [] (CopyPlaces(groups, target))
    | ForMoving -> requestIfOutsideSource fs state groups target [] (MovePastedFiles(files, target))

let private quitting (fs: IFileSystem) state =
    { State = state
      Effects = [ SaveSettings(settings fs state); Quit ] }

let private transition (fs: IFileSystem) (msg: Message) (state: AppState) : UpdateResult =
    match msg with
    | CursorMoved cursor -> setCursor cursor.Side cursor.Index state |> stay
    | PanelFocused side -> activate side state |> stay
    | EntryOpened -> openCursorEntry fs state
    | WalkIntoRequested -> enter fs state |> applyListing state
    | UpOneLevelRequested -> upOneLevel fs state |> applyListing state
    | RefreshRequested -> refreshWithRefusals fs state |> applyListingWithRefusals state
    | ExternalChangeDetected -> state |> readTabQuietly fs |> stay
    | SelectionToggled direction -> toggleSelectionMoving direction state.Active state |> stay
    | SelectionToggledTo cursor -> toggleSelectionShortOf cursor.Side cursor.Index state |> stay
    | SortRequested key -> sortBy key state.Active state |> stay
    | HiddenToggled -> toggleHidden fs state.Active state |> applyListing state
    | SearchTyped character -> typeIntoSearch state.Active character state |> stay
    | SearchBackspaced -> backspaceSearch state.Active state |> stay
    | SearchCancelled -> closeSearch state.Active state |> stay
    | PatternRequested mode -> PromptForPattern mode |> request state
    | PatternEntered(mode, pattern) -> applyPattern mode state.Active pattern state |> stay
    | LikenessRequested(mode, likeness) -> applyLikeness mode state.Active likeness state |> stay
    | AllFilesRequested mode -> applyAllFiles mode state.Active state |> stay
    | InversionRequested -> invertSelection state.Active state |> stay
    | MarksRecalled -> recallMarks state.Active state |> stay
    | MakeDirectoryRequested -> askingForDirectoryName fs state
    | NameEntered name -> makingDirectory fs state name
    | DeleteRequested mode -> inspectingDelete fs state mode
    | DeleteScopeLoaded(scope, mode) -> confirmingDelete state scope mode
    | DeleteConfirmed mode -> deleting fs state mode
    | DeleteFinished results -> finishDelete fs results state |> applyBatch state
    | CopyRequested -> copying fs state
    | CopyFinished results -> finishTransfer fs results state |> applyBatch state
    | MoveRequested -> moving fs state
    | TargetEntered text -> renamingOrMoving fs state text
    | MoveFinished results -> finishTransfer fs results state |> applyBatch state
    | FindRequested -> askingToFind state
    | FindEntered query -> searching fs state query
    | LastResultsRequested -> showingLastResults fs state
    | SearchAnswered answer -> landingFromSearch fs state answer
    | LinkRequested -> askingForLink fs state
    | LinkEntered choice -> linking fs state choice
    | LinksMade(made, results) -> finishLinks fs made results state |> applyBatch state
    | PackRequested -> askingForArchive fs state
    | ArchiveEntered choice -> packing fs state choice
    | PackFinished(archive, results) -> finishPack fs archive results state |> applyBatch state
    | UnpackRequested -> unpacking fs state
    | UnpackFinished results -> finishTransfer fs results state |> applyBatch state
    | RenameFinished(newName, outcome) -> renamed fs state newName outcome
    | ViewRequested -> viewing fs state
    | MeasureFinished(Ok measured) -> applyMeasurements state.Active measured state |> stay
    | MeasureFinished(Error error) -> reporting state error
    | EditRequested -> editing fs state
    | NewFileRequested -> askingForFileName state
    | FileNameEntered name -> makingFile fs state name
    | PropertiesEntered properties -> applyProperties fs properties state |> applyOperation state
    | SettingsRequested -> showingSettings fs state
    | ProgramsEntered programs -> withPrograms programs state |> stay
    | EditorStarted(Error(Operations.StartDeclined _))
    | EditorStarted(Ok()) -> stay state
    | EditorStarted(Error error) -> reporting state error
    | LaunchFinished(started, outcome) -> launched fs state started outcome
    | UserMenuRequested -> request state ShowUserMenu
    | MenuCommandChosen(command, listFile) -> runningFromMenu fs state command listFile
    | CommandFinished(_, Error(ElevationDeclined _)) -> stay state
    | CommandFinished(_, Error error) -> Problem.Command error |> Report |> request state
    | CommandFinished(run, Ok()) -> Ran run |> Remember |> ChangeHistory |> request state
    | OpenWithRequested -> openWithHandler fs state
    | HandlerChosen(path, choice) -> chosen fs state path choice
    | ElevatedRunRequested -> runElevated fs state
    | RootRequested side -> PromptForRoot side |> request state
    | VolumeRootRequested -> sentToRoot fs state
    | PathOpened(side, path) -> loadPanel fs side path state |> applyListing state
    | DriveChosen(side, row) -> loadPanel fs side (driveLanding fs side row state) state |> applyListing state
    | DriveHandedOver row -> handedOver state row
    | TabOpened -> openTab fs state |> saving fs
    | TabClosed -> closeTab state |> saving fs
    | TabsRequested -> state.Tabs |> ShowTabs |> request state
    | TabSwitched position -> switchTab position state |> rereadAndSave fs
    | TabCycled step -> cycleTab step state |> rereadAndSave fs
    | SessionSaveRequested -> state |> saving fs
    | HistoryChanged history -> stay { state with History = history }
    | HistoryUnwritten error -> reportingHistoryOnce state error
    | HistoryRequested -> ShowHistory state.HistoryTab |> request state
    | HistoryAnswered answer -> chosenFromHistory fs { state with HistoryTab = answer.Tab } answer.Choice
    | FavoriteKeepRequested -> askingForFavoriteSlot state
    | FavoriteRequested slot -> LookUpFavorite slot |> request state
    | FavoriteFound place -> Landing.inside place |> landingOn fs state
    | FavoritesRequested -> (activePanel state).Path |> Place.ofLocation |> ShowFavorites |> request state
    | NamesCopyRequested -> copyingNames state
    | PathsCopyRequested -> copyingPaths fs state
    | LinkTargetsCopyRequested -> copyingLinkTargets fs state
    | ClipboardPathRequested -> readingClipboardPath fs state
    | ClipboardPathRead path -> Landing.ofPath fs path |> landingIfAnywhere fs state
    | ClipboardRefused ClipboardUnavailable -> reportingClipboardOnce state
    | ClipboardRefused error -> Problem.Clipboard error |> Report |> request state
    | FilesOnClipboardRequested intent -> puttingFilesOnClipboard fs state intent
    | ClipboardExtractFinished(groups, scratch, results) -> extractedForClipboard fs state groups scratch results
    | FilesPasteRequested -> readingClipboardFiles fs state
    | ClipboardFilesRead(files, target) -> pasting fs state files target
    | QuitRequested -> quitting fs state

let private watching before result =
    match watchedLocations result.State with
    | after when after = watchedLocations before -> result
    | after ->
        { result with
            Effects = result.Effects @ [ WatchLocations after ] }

let private movesPanelsItself msg =
    match msg with
    | RefreshRequested
    | ExternalChangeDetected
    | TabOpened
    | TabClosed
    | TabSwitched _
    | TabCycled _ -> true
    | _ -> false

let private landedPlace before after side =
    let placeIn state =
        Place.ofLocation (panel side state).Path

    match placeIn after with
    | Some landed when placeIn before <> Some landed -> Some landed
    | _ -> None

let private remembering msg before result =
    if movesPanelsItself msg then
        result
    else
        let visits =
            [ Left; Right ]
            |> List.choose (landedPlace before result.State)
            |> List.map (Visited >> Remember >> ChangeHistory)

        { result with
            Effects = result.Effects @ visits }

let private keepsMarksToGoBackTo msg =
    match msg with
    | SelectionToggled _
    | SelectionToggledTo _
    | PatternRequested _
    | PatternEntered _
    | LikenessRequested _
    | AllFilesRequested _
    | InversionRequested
    | MarksRecalled
    | CursorMoved _
    | SearchTyped _
    | SearchBackspaced
    | SearchCancelled
    | PanelFocused _
    | ExternalChangeDetected -> true
    | _ -> false

let private forgettingMarks msg result =
    if keepsMarksToGoBackTo msg then
        result
    else
        { result with
            State = forgetMarks result.State }

let private refusedAsSource msg =
    match msg with
    | MakeDirectoryRequested -> Some MakingDirectory
    | NewFileRequested -> Some MakingFile
    | FilesPasteRequested -> Some Receiving
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
    | Some refusal, _ when PanelState.showsResults (activePanel state) -> Some refusal
    | _, Some refusal when PanelState.showsResults (panel (opposite state.Active) state) -> Some refusal
    | _ -> None

let update (fs: IFileSystem) (msg: Message) (state: AppState) : UpdateResult =
    let result =
        match resultsRefusal state msg with
        | Some refusal -> reportResultsRefusal state refusal
        | None -> transition fs msg state

    result |> watching state |> remembering msg state |> forgettingMarks msg

let step (fs: IFileSystem) (msg: Message) (state: AppState) =
    let result = update fs msg state
    result.State, result.Effects
