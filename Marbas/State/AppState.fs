module Marbas.State.AppState

open System.IO.Abstractions
open FsToolkit.ErrorHandling
open Marbas.Collections
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.State

type Tab =
    { Left: PanelState
      Right: PanelState
      Active: PanelSide }

type HistoryReport =
    | Unreported
    | Reported

type HistoryTab =
    | RunHistoryTab
    | ViewHistoryTab
    | FolderHistoryTab

type HistoryChoice =
    | OpenPlace of Place
    | RunAgain of LaunchRequest
    | Revisit of FileVisit
    | GoTo of Place

type HistoryAnswer =
    { Tab: HistoryTab
      Choice: HistoryChoice option }

type AppState =
    { Tabs: Zipper<Tab>
      LastSearch: FindResults option
      Programs: ProgramSettings
      History: History
      HistoryReport: HistoryReport
      HistoryTab: HistoryTab }

    member private this.Focused = this.Tabs |> Zipper.current

    member this.Left = this.Focused.Left

    member this.Right = this.Focused.Right

    member this.Active = this.Focused.Active

type TransferSource =
    | FromDisk of paths: FileSystemPath list
    | FromArchive of archive: FileSystemPath * entries: ArchivePath list
    | FromPlaces of groups: TransferSource list

type TransferTarget =
    | ToDirectory of directory: FileSystemPath
    | ToArchive of archive: FileSystemPath * level: ArchivePath

let private restoredPanel (settings: PanelSettings) =
    { Path = OnDisk settings.Path
      Entries = Array.empty
      Cursor = 0
      Remembered = settings.Cursor
      Selected = Set.empty
      Sort = settings.Sort
      Hidden = settings.Hidden
      Search = None }

let private restoredTab (tab: TabSettings) =
    { Left = restoredPanel tab.Left
      Right = restoredPanel tab.Right
      Active = tab.Active }

let restore (settings: Settings) =
    { Tabs = settings.Tabs |> Zipper.map restoredTab
      LastSearch = None
      Programs = settings.Programs
      History = History.empty
      HistoryReport = Unreported
      HistoryTab = FolderHistoryTab }

let create (path: FileSystemPath) = restore (Settings.forPath path)

let private panelSettings (fs: IFileSystem) (p: PanelState) : PanelSettings =
    { Path = PanelLocation.holding fs p.Path
      Sort = p.Sort
      Hidden = p.Hidden
      Cursor = PanelState.standingName p }

let private tabSettings (fs: IFileSystem) (tab: Tab) : TabSettings =
    { Left = panelSettings fs tab.Left
      Right = panelSettings fs tab.Right
      Active = tab.Active }

let settings (fs: IFileSystem) (state: AppState) : Settings =
    { Tabs = state.Tabs |> Zipper.map (tabSettings fs)
      Programs = state.Programs }

let withPrograms (programs: ProgramSettings) (state: AppState) = { state with Programs = programs }

let programFor role (name: EntryName) (state: AppState) =
    ProgramSettings.programFor role name state.Programs

let panel side (state: AppState) =
    match side with
    | Left -> state.Left
    | Right -> state.Right

let activePanel (state: AppState) = panel state.Active state

let watchedLocations (state: AppState) =
    [ state.Left.Path; state.Right.Path ]
    |> List.choose PanelLocation.watched
    |> List.distinct

let private updateTab f (state: AppState) =
    { state with Tabs = state.Tabs |> Zipper.update f }

let private updatePanel side f (state: AppState) =
    updateTab
        (fun tab ->
            match side with
            | Left -> { tab with Left = f tab.Left }
            | Right -> { tab with Right = f tab.Right })
        state

let closeSearch side state =
    updatePanel side PanelState.withoutSearch state

let activate side state =
    state
    |> updateTab (fun tab -> { tab with Active = side })
    |> closeSearch Left
    |> closeSearch Right

let closeTab (state: AppState) =
    { state with Tabs = state.Tabs |> Zipper.remove }

let switchTab position (state: AppState) =
    let leaving = state |> closeSearch Left |> closeSearch Right

    { leaving with Tabs = leaving.Tabs |> Zipper.refocus position }

let cycleTab step (state: AppState) =
    let leaving = state |> closeSearch Left |> closeSearch Right

    let walk =
        match step with
        | Forward -> Zipper.forward
        | Backward -> Zipper.backward

    { leaving with Tabs = leaving.Tabs |> walk }

let opposite side =
    match side with
    | Left -> Right
    | Right -> Left

let toggleActive (state: AppState) = activate (opposite state.Active) state

let setCursor side index state =
    updatePanel side (PanelState.withCursor index) state

let typeIntoSearch side character state =
    updatePanel side (PanelState.typeIntoSearch character) state

let backspaceSearch side state =
    updatePanel side PanelState.backspaceSearch state

let private everythingRead entries =
    { SearchListing.Listed = entries
      SearchListing.Refused = [] }

let private listing (fs: IFileSystem) sort hidden location =
    match location with
    | OnDisk path -> DirectoryListing.listForDisplay fs sort hidden path |> Result.map everythingRead
    | InArchive(archive, level) ->
        ArchiveListing.listForDisplay fs sort hidden archive level
        |> Result.map everythingRead
    | SearchResults found -> SearchListing.listForDisplay fs sort hidden found |> Ok

let private loadLocationWithRefusals (fs: IFileSystem) side (location: PanelLocation) focus state =
    let previous = panel side state
    let sort = previous.Sort
    let hidden = previous.Hidden

    listing fs sort hidden location
    |> Result.map (fun listing ->
        let entries = listing.Listed

        let cursor =
            match focus with
            | ByName name -> entries |> Array.tryFindIndex (fun e -> e.Name = name) |> Option.defaultValue 0
            | Top -> 0

        updatePanel
            side
            (fun _ ->
                { Path = location
                  Entries = entries
                  Cursor = cursor
                  Remembered = None
                  Selected = Set.empty
                  Sort = sort
                  Hidden = hidden
                  Search = None })
            state,
        listing.Refused)

let loadLocationFocusing (fs: IFileSystem) side (location: PanelLocation) focus state =
    loadLocationWithRefusals fs side location focus state |> Result.map fst

let loadPanelFocusing (fs: IFileSystem) side (path: FileSystemPath) focus state =
    loadLocationFocusing fs side (OnDisk path) focus state

let loadPanel fs side (path: FileSystemPath) state =
    loadPanelFocusing fs side path (PanelState.rememberedFocus (panel side state)) state

let private focusByNameOrTop name =
    match name with
    | Some name -> ByName name
    | None -> Top

let private steppingOffResults (fs: IFileSystem) side state =
    match (panel side state).Path with
    | SearchResults found ->
        loadLocationFocusing fs side found.Origin (focusByNameOrTop found.Returning) state
        |> Result.defaultValue state
    | _ -> state

let openTab (fs: IFileSystem) (state: AppState) =
    let clone (tab: Tab) =
        { tab with
            Left = PanelState.cleared tab.Left
            Right = PanelState.cleared tab.Right }

    let cloned = state.Tabs |> Zipper.current |> clone

    { state with
        Tabs = state.Tabs |> Zipper.insert cloned }
    |> steppingOffResults fs Left
    |> steppingOffResults fs Right

let sortBy (key: SortKey) side state =
    updatePanel side (PanelState.sortBy key) state

let toggleSelectionShortOf side target state =
    updatePanel side (PanelState.toggleSelectionShortOf target) state

let toggleSelection side state =
    updatePanel side PanelState.toggleSelection state

let toggleSelectionMoving direction side state =
    updatePanel side (PanelState.toggleSelectionMoving direction) state

let applyPattern mode side pattern state =
    updatePanel side (PanelState.applyPattern mode pattern) state

let private asOperationError error =
    match error with
    | DirectoryNotFound path -> Operations.SourceNotFound path
    | DirectoryListing.AccessDenied path -> Operations.AccessDenied path
    | ListingFailed(path, message) -> Operations.OperationFailed(path, message)
    | ArchiveUnreadable error -> Operations.ArchiveFailed error

let private parentLocation (fs: IFileSystem) panel =
    match panel.Path with
    | OnDisk path -> FileSystemPath.parent fs path |> Option.map OnDisk
    | InArchive(archive, level) ->
        match ArchivePath.parent level with
        | Some above -> InArchive(archive, above) |> Some
        | None -> OnDisk(FileSystemPath.directory fs archive) |> Some
    | SearchResults found -> Some found.Origin

let private childLocation (fs: IFileSystem) panel (name: EntryName) =
    match panel.Path with
    | SearchResults found ->
        SearchListing.hitFor found name
        |> Option.bind (fun hit -> PanelLocation.child fs hit.Location hit.Entry.Name)
    | location -> PanelLocation.child fs location name

let private enteredLocation (fs: IFileSystem) panel (entry: Entry) =
    if entry.Name = ParentName then
        parentLocation fs panel
    else
        childLocation fs panel entry.Name

let withLastSearch (results: FindResults) state = { state with LastSearch = Some results }

let standOnHit (fs: IFileSystem) side (hit: FindHit) state =
    let showing =
        match hit.Entry.Visibility with
        | Hidden -> Shown
        | Plain -> (panel side state).Hidden

    state
    |> updatePanel side (fun p -> { p with Hidden = showing })
    |> loadLocationFocusing fs side hit.Location (ByName hit.Entry.Name)

let standInside (fs: IFileSystem) side location state =
    loadLocationFocusing fs side location Top state

let standBeside (fs: IFileSystem) side location name state =
    loadLocationFocusing fs side location (ByName name) state

let showResults (fs: IFileSystem) side (results: FindResults) state =
    let previous = panel side state

    let found =
        { Query = results.Query
          Ground = results.Ground
          Hits = results.Hits
          Shown = FoundEntries.shownBy fs results.Ground results.Hits
          Origin = PanelLocation.origin previous.Path
          Returning = PanelState.standingName previous }

    loadLocationWithRefusals fs side (SearchResults found) Top state

let markedDirectories (fs: IFileSystem) panel =
    PanelState.selectedEntries panel
    |> List.filter (fun entry -> entry.Kind = Directory)
    |> List.choose (enteredLocation fs panel)

let activeDirectory state = PanelState.directory (activePanel state)

let targetDirectory (state: AppState) = PanelState.directory (panel (opposite state.Active) state)

let driveLanding (fs: IFileSystem) side (row: DriveRow) (state: AppState) =
    let otherDirectory = PanelLocation.holding fs (panel (opposite side) state).Path

    match row with
    | HomeDirectory home -> home
    | MountedDrive drive when Drives.holding fs otherDirectory = Some drive.Root -> otherDirectory
    | MountedDrive drive -> drive.Root

let transferTarget (state: AppState) =
    match (panel (opposite state.Active) state).Path with
    | OnDisk directory -> Some(ToDirectory directory)
    | InArchive(archive, level) -> Some(ToArchive(archive, level))
    | SearchResults _ -> None

let private entryPath (fs: IFileSystem) panel (entry: Entry) =
    match panel.Path with
    | SearchResults _ ->
        PanelState.hitAt panel entry
        |> Option.filter (fun hit -> PanelLocation.directory hit.Location |> Option.isSome)
        |> Option.map (FindHit.spelled fs)
    | location -> PanelLocation.combine fs location (EntryName.value entry.Name)

let operationPaths (fs: IFileSystem) panel =
    PanelState.operationTargets panel |> List.choose (entryPath fs panel)

let cursorFilePath (fs: IFileSystem) panel =
    PanelState.cursorEntry panel
    |> Option.filter (fun entry -> entry.Kind = File)
    |> Option.bind (entryPath fs panel)

let cursorExecutablePath (fs: IFileSystem) panel =
    PanelState.cursorEntry panel
    |> Option.filter (fun entry -> entry.Kind = File && entry.Executability = Executable)
    |> Option.bind (entryPath fs panel)

let private groupedByPlace (fs: IFileSystem) (hits: FindHit list) =
    hits
    |> List.groupBy (FindHit.archivePlace >> Option.map fst)
    |> List.map (fun (place, held) ->
        match place with
        | None -> FromDisk(held |> List.map (FindHit.spelled fs))
        | Some archive -> FromArchive(archive, held |> List.choose FindHit.archivePlace |> List.map snd))

let private fromResults (fs: IFileSystem) panel =
    match PanelState.operationTargets panel |> List.choose (PanelState.hitAt panel) |> groupedByPlace fs with
    | [] -> None
    | [ single ] -> Some single
    | several -> Some(FromPlaces several)

let transferSource (fs: IFileSystem) panel =
    match panel.Path, PanelState.operationTargets panel with
    | _, [] -> None
    | OnDisk _, _ -> Some(FromDisk(operationPaths fs panel))
    | InArchive(archive, level), targets ->
        Some(FromArchive(archive, targets |> List.map (fun entry -> ArchivePath.child level entry.Name)))
    | SearchResults _, _ -> fromResults fs panel

let cursorDirectory (fs: IFileSystem) panel =
    match panel.Path with
    | SearchResults _ ->
        PanelState.cursorEntry panel
        |> Option.bind (PanelState.hitAt panel)
        |> Option.map (fun hit -> PanelLocation.holding fs hit.Location)
    | location -> PanelLocation.directory location

let cursorDirectoryPath (fs: IFileSystem) panel =
    PanelState.cursorEntry panel
    |> Option.filter (fun entry -> entry.Kind = Directory && PanelState.selectable entry)
    |> Option.bind (entryPath fs panel)

let measureTargets (fs: IFileSystem) panel =
    let considered =
        match PanelState.selectedEntries panel with
        | [] -> PanelState.cursorEntry panel |> Option.toList
        | marked -> marked

    considered
    |> List.filter (fun entry -> entry.Kind = Directory)
    |> List.choose (fun entry ->
        enteredLocation fs panel entry
        |> Option.map (fun location ->
            { Name = entry.Name
              Location = location }))

let applyMeasurements side (measured: Measurement list) state =
    updatePanel side (PanelState.withMeasurements measured) state

type DeleteQuestion =
    { Probed: FileSystemPath list
      AtLeast: DeleteScope }

let private reachingContents targetsDirectory =
    if targetsDirectory then WithDirectoryContents else NamedEntriesOnly

let deleteQuestion (fs: IFileSystem) panel =
    match panel.Path with
    | OnDisk _ ->
        { Probed = operationPaths fs panel
          AtLeast = NamedEntriesOnly }
    | InArchive _ ->
        { Probed = []
          AtLeast = PanelState.targetsDirectory panel |> reachingContents }
    | SearchResults _ ->
        { Probed = operationPaths fs panel
          AtLeast = PanelState.targetsDirectoryInArchive panel |> reachingContents }

let deleteScope (fs: IFileSystem) (question: DeleteQuestion) =
    match question.AtLeast with
    | WithDirectoryContents -> WithDirectoryContents
    | NamedEntriesOnly -> question.Probed |> List.exists (Operations.isNonEmptyDirectory fs) |> reachingContents

let private sourceOf (fs: IFileSystem) panel (name: EntryName) =
    childLocation fs panel name
    |> Option.bind (function
        | OnDisk path -> Some(Operations.ItemOnDisk path)
        | InArchive(archive, entry) -> Some(Operations.ItemInArchive(archive, entry))
        | SearchResults _ -> None)

let private sourcesWhere wanted (results: Operations.ItemResult list) =
    results |> List.filter (_.Outcome >> wanted) |> List.map _.Source |> Set.ofList

let private keepUnfinished (fs: IFileSystem) results selected (p: PanelState) =
    let finished = sourcesWhere Result.isOk results
    let failed = sourcesWhere Result.isError results

    let unfinished name =
        match sourceOf fs p name with
        | Some source -> failed.Contains source || not (finished.Contains source)
        | None -> true

    PanelState.keepSurviving (Set.filter unfinished selected) p

let private reloadFocusing (fs: IFileSystem) side focus keep state =
    let previous = panel side state

    loadLocationFocusing fs side previous.Path focus state
    |> Result.mapError asOperationError
    |> Result.map (updatePanel side (keep previous))

let private reload (fs: IFileSystem) side keep state =
    reloadFocusing fs side (PanelState.cursorFocus (panel side state)) keep state

let private keepingUnfinished (fs: IFileSystem) results (previous: PanelState) =
    keepUnfinished fs results previous.Selected

let private keepingSelected (previous: PanelState) = PanelState.keepSurviving previous.Selected

let finishDelete (fs: IFileSystem) results (state: AppState) =
    reload fs state.Active (keepingUnfinished fs results) state
    |> Result.map (fun updated -> updated, results)

let finishTransfer (fs: IFileSystem) results (state: AppState) =
    result {
        let! active = reload fs state.Active (keepingUnfinished fs results) state
        let! updated = reload fs (opposite state.Active) keepingSelected active
        return updated, results
    }

let archiveSuggestion (fs: IFileSystem) format state =
    let named directory =
        PanelState.operationTargets (activePanel state)
        |> List.map _.Name
        |> ArchiveName.suggest fs format directory
        |> EntryName.value

    (activeDirectory state, targetDirectory state)
    ||> Option.map2 (fun source target -> FileSystemPath.combine fs target (named source))

let finishPack (fs: IFileSystem) (archive: EntryName) results (state: AppState) =
    result {
        let! active = reload fs state.Active (keepingUnfinished fs results) state
        let! updated = reloadFocusing fs (opposite state.Active) (ByName archive) keepingSelected active
        return updated, results
    }

let finishLinks (fs: IFileSystem) (made: FileSystemPath list) results (state: AppState) =
    let showing side keep state =
        let standing = PanelState.directory (panel side state)

        match made |> List.tryFind (fun location -> Some(FileSystemPath.directory fs location) = standing) with
        | Some location -> reloadFocusing fs side (ByName(FileSystemPath.name fs location)) keep state
        | None -> reload fs side keep state

    result {
        let! active = showing state.Active (keepingUnfinished fs results) state
        let! updated = showing (opposite state.Active) keepingSelected active
        return updated, results
    }

let finishRename (fs: IFileSystem) newName (state: AppState) =
    reloadFocusing fs state.Active (ByName newName) keepingSelected state

let private madeDirectory (fs: IFileSystem) side location name state =
    let previous = panel side state

    loadLocationFocusing fs side location (ByName name) state
    |> Result.mapError asOperationError
    |> Result.map (updatePanel side (PanelState.keepSurviving previous.Selected))

let makeDirectory (fs: IFileSystem) side directory name state : Result<AppState, Operations.OperationError> =
    Operations.makeDirectory fs directory name
    |> Result.bind (fun () -> madeDirectory fs side (OnDisk directory) name state)

let makeEntryDirectory (fs: IFileSystem) side archive level name state : Result<AppState, Operations.OperationError> =
    Archive.makeEntryDirectory fs archive level name
    |> Result.bind (fun () -> madeDirectory fs side (InArchive(archive, level)) name state)

let makeFile (fs: IFileSystem) side directory name state : Result<AppState * FileSystemPath, Operations.OperationError> =
    let previous = panel side state

    result {
        let! path = Operations.makeFile fs directory name
        let! loaded = loadPanelFocusing fs side directory (ByName name) state |> Result.mapError asOperationError
        return updatePanel side (PanelState.keepSurviving previous.Selected) loaded, path
    }

let private readAgainWithRefusals (fs: IFileSystem) side state =
    let panel = panel side state

    loadLocationWithRefusals fs side panel.Path (PanelState.cursorFocus panel) state
    |> Result.map (fun (loaded, refused) -> updatePanel side (PanelState.keepSurviving panel.Selected) loaded, refused)

let private readAgain (fs: IFileSystem) side state =
    readAgainWithRefusals fs side state |> Result.map fst

let refresh (fs: IFileSystem) (state: AppState) = readAgain fs state.Active state

let refreshWithRefusals (fs: IFileSystem) (state: AppState) =
    readAgainWithRefusals fs state.Active state

let readTabAgain (fs: IFileSystem) state =
    readAgain fs Left state |> Result.bind (readAgain fs Right)

let private ground (fs: IFileSystem) (location: PanelLocation) =
    location
    |> PanelLocation.holding fs
    |> FileSystemPath.nearest fs
    |> Option.orElseWith (fun () -> Drives.list fs None |> List.tryHead |> Option.map DriveRow.root)

let private replanted (fs: IFileSystem) side state =
    match ground fs (panel side state).Path with
    | Some path -> loadPanelFocusing fs side path Top state |> Result.defaultValue state
    | None -> state

let readTabQuietly (fs: IFileSystem) state =
    let reread side state =
        match readAgain fs side state with
        | Ok updated -> updated
        | Error _ -> replanted fs side state

    let keeping side state =
        match (panel side state).Path |> PanelLocation.watched with
        | Some _ -> reread side state
        | None -> state

    state |> keeping Left |> keeping Right

let applyProperties (fs: IFileSystem) (properties: EntryProperties) state =
    EntryProperties.write fs properties
    |> Result.bind (fun () -> refresh fs state |> Result.mapError asOperationError)

let toggleHidden (fs: IFileSystem) side state =
    let previous = panel side state

    state
    |> updatePanel side PanelState.withHiddenToggled
    |> loadLocationFocusing fs side previous.Path (PanelState.cursorFocus previous)
    |> Result.map (updatePanel side (PanelState.keepSurviving previous.Selected))

let private navigateToParent (fs: IFileSystem) side (from: FileSystemPath) state =
    match FileSystemPath.parent fs from with
    | None -> Ok state
    | Some parent ->
        let focus = from |> FileSystemPath.name fs |> ByName
        loadPanelFocusing fs side parent focus state

let private navigateToChild (fs: IFileSystem) side (parentPath: FileSystemPath) name state =
    let targetPath = FileSystemPath.combine fs parentPath (EntryName.value name)
    loadPanelFocusing fs side targetPath Top state

let private openArchive (fs: IFileSystem) side (archive: FileSystemPath) state =
    match ArchiveReader.kindOf fs archive with
    | None -> Ok state
    | Some _ -> loadLocationFocusing fs side (InArchive(archive, ArchivePath.root)) Top state

let private leaveArchive (fs: IFileSystem) side (archive: FileSystemPath) state =
    let focus = archive |> FileSystemPath.name fs |> ByName
    loadPanelFocusing fs side (FileSystemPath.directory fs archive) focus state

let private climbLevel (fs: IFileSystem) side archive level state =
    match ArchivePath.parent level, ArchivePath.name level with
    | Some above, Some left -> loadLocationFocusing fs side (InArchive(archive, above)) (ByName left) state
    | _ -> leaveArchive fs side archive state

let private enterOnDisk (fs: IFileSystem) side path (entry: Entry) state =
    match entry.Kind with
    | Directory when entry.Name = DirectoryListing.ParentName -> navigateToParent fs side path state
    | Directory -> navigateToChild fs side path entry.Name state
    | File -> openArchive fs side (FileSystemPath.combine fs path (EntryName.value entry.Name)) state

let private enterInArchive (fs: IFileSystem) side archive level (entry: Entry) state =
    match entry.Kind with
    | Directory when entry.Name = DirectoryListing.ParentName -> climbLevel fs side archive level state
    | Directory -> loadLocationFocusing fs side (InArchive(archive, ArchivePath.child level entry.Name)) Top state
    | File -> Ok state

let private enterInResults (fs: IFileSystem) side panel found (entry: Entry) state =
    match entry.Kind, enteredLocation fs panel entry with
    | File, _
    | _, None -> Ok state
    | Directory, Some location when entry.Name = ParentName ->
        loadLocationFocusing fs side location (focusByNameOrTop found.Returning) state
    | Directory, Some location -> loadLocationFocusing fs side location Top state

let enter (fs: IFileSystem) state =
    let panel = activePanel state

    match PanelState.cursorEntry panel with
    | None -> Ok state
    | Some entry ->
        match panel.Path with
        | OnDisk path -> enterOnDisk fs state.Active path entry state
        | InArchive(archive, level) -> enterInArchive fs state.Active archive level entry state
        | SearchResults found -> enterInResults fs state.Active panel found entry state
