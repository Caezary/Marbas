module Marbas.State.AppState

open System.IO.Abstractions
open FsToolkit.ErrorHandling
open Marbas.Collections
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing

type PanelState =
    { Path: PanelLocation
      Entries: Entry array
      Cursor: int
      Remembered: EntryName option
      Selected: Set<EntryName>
      Sort: SortOrder
      Hidden: HiddenEntries
      Search: SearchText option }

type Tab =
    { Left: PanelState
      Right: PanelState
      Active: PanelSide }

type AppState =
    { Tabs: Zipper<Tab>
      LastCreatedDirectory: EntryName option
      LastSearch: FindResults option
      Programs: ProgramSettings }

    member private this.Focused = this.Tabs |> Zipper.current

    member this.Left = this.Focused.Left

    member this.Right = this.Focused.Right

    member this.Active = this.Focused.Active

type FocusTarget =
    | Top
    | ByName of EntryName

type TransferSource =
    | FromDisk of paths: FileSystemPath list
    | FromArchive of archive: FileSystemPath * entries: ArchivePath list
    | FromPlaces of groups: TransferSource list

type TransferTarget =
    | ToDirectory of directory: FileSystemPath
    | ToArchive of archive: FileSystemPath * level: ArchivePath

let private panelFrom (settings: PanelSettings) =
    { Path = OnDisk settings.Path
      Entries = Array.empty
      Cursor = 0
      Remembered = settings.Cursor
      Selected = Set.empty
      Sort = settings.Sort
      Hidden = settings.Hidden
      Search = None }

let private tabFrom (tab: TabSettings) =
    { Left = panelFrom tab.Left
      Right = panelFrom tab.Right
      Active = tab.Active }

let fromSettings (settings: Settings) =
    { Tabs = settings.Tabs |> Zipper.map tabFrom
      LastCreatedDirectory = settings.LastCreatedDirectory
      LastSearch = None
      Programs = settings.Programs }

let create (path: FileSystemPath) = fromSettings (Settings.forPath path)

let private standingName (p: PanelState) =
    match p.Path with
    | SearchResults found -> found.Returning
    | _ ->
        p.Entries
        |> Array.tryItem p.Cursor
        |> Option.filter (fun entry -> entry.Name <> DirectoryListing.ParentName)
        |> Option.map _.Name
        |> Option.orElse p.Remembered

let private panelSettings (fs: IFileSystem) (p: PanelState) : PanelSettings =
    { Path = PanelLocation.holding fs p.Path
      Sort = p.Sort
      Hidden = p.Hidden
      Cursor = standingName p }

let private tabSettings (fs: IFileSystem) (tab: Tab) : TabSettings =
    { Left = panelSettings fs tab.Left
      Right = panelSettings fs tab.Right
      Active = tab.Active }

let settings (fs: IFileSystem) (state: AppState) : Settings =
    { Tabs = state.Tabs |> Zipper.map (tabSettings fs)
      LastCreatedDirectory = state.LastCreatedDirectory
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

let private withoutSearch (p: PanelState) = { p with Search = None }

let closeSearch side state = updatePanel side withoutSearch state

let activate side state =
    state
    |> updateTab (fun tab -> { tab with Active = side })
    |> closeSearch Left
    |> closeSearch Right

let private cleared (p: PanelState) =
    { p with
        Selected = Set.empty
        Search = None }

let closeTab (state: AppState) =
    { state with Tabs = state.Tabs |> Zipper.remove }

let switchTab position (state: AppState) =
    let leaving = state |> closeSearch Left |> closeSearch Right

    { leaving with Tabs = leaving.Tabs |> Zipper.at position }

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

let private withCursor index (p: PanelState) = { p with Cursor = index }

let private withCursorWithin index (p: PanelState) =
    { p with Cursor = index |> min (p.Entries.Length - 1) |> max 0 }

let setCursor side index state =
    updatePanel side (withCursor index) state

let private focusFirstMatch (p: PanelState) =
    match p.Search with
    | None -> p
    | Some text ->
        match p.Entries |> Array.tryFindIndex (fun e -> SearchText.matches text e.Name) with
        | Some index -> withCursor index p
        | None -> p

let private searchFor text (p: PanelState) = focusFirstMatch { p with Search = text }

let typeIntoSearch side character state =
    updatePanel
        side
        (fun p ->
            match p.Search with
            | None -> searchFor (Some(SearchText.ofChar character)) p
            | Some text -> searchFor (Some(SearchText.append character text)) p)
        state

let backspaceSearch side state =
    updatePanel
        side
        (fun p ->
            match p.Search with
            | None -> p
            | Some text -> searchFor (SearchText.backspace text) p)
        state

let private listing (fs: IFileSystem) sort hidden location =
    match location with
    | OnDisk path -> DirectoryListing.listForDisplay fs sort hidden path
    | InArchive(archive, level) -> ArchiveListing.listForDisplay fs sort hidden archive level
    | SearchResults found -> SearchListing.listForDisplay fs sort hidden found

let loadLocationFocusing (fs: IFileSystem) side (location: PanelLocation) focus state =
    let previous = panel side state
    let sort = previous.Sort
    let hidden = previous.Hidden

    listing fs sort hidden location
    |> Result.map (fun entries ->
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
            state)

let loadPanelFocusing (fs: IFileSystem) side (path: FileSystemPath) focus state =
    loadLocationFocusing fs side (OnDisk path) focus state

let cursorEntry panel = panel.Entries |> Array.tryItem panel.Cursor

let private rememberedFocus (p: PanelState) =
    match p.Remembered with
    | Some name -> ByName name
    | None -> Top

let loadPanel fs side (path: FileSystemPath) state =
    loadPanelFocusing fs side path (rememberedFocus (panel side state)) state

let private cursorFocus panel =
    match cursorEntry panel with
    | Some entry -> ByName entry.Name
    | None -> rememberedFocus panel

let private focusOn name =
    match name with
    | Some name -> ByName name
    | None -> Top

let private steppingOffResults (fs: IFileSystem) side state =
    match (panel side state).Path with
    | SearchResults found ->
        loadLocationFocusing fs side found.Origin (focusOn found.Returning) state
        |> Result.defaultValue state
    | _ -> state

let openTab (fs: IFileSystem) (state: AppState) =
    let clone (tab: Tab) =
        { tab with
            Left = cleared tab.Left
            Right = cleared tab.Right }

    let cloned = state.Tabs |> Zipper.current |> clone

    { state with
        Tabs = state.Tabs |> Zipper.insert cloned }
    |> steppingOffResults fs Left
    |> steppingOffResults fs Right

let private reorder (p: PanelState) =
    let entries = DirectoryListing.order p.Sort p.Entries

    let cursor =
        cursorEntry p
        |> Option.bind (fun entry -> entries |> Array.tryFindIndex (fun e -> e.Name = entry.Name))
        |> Option.defaultValue 0

    { p with
        Entries = entries
        Cursor = cursor }

let sortBy (key: SortKey) side state =
    updatePanel side (fun p -> reorder { p with Sort = SortOrder.switchTo key p.Sort }) state

let private selectable entry = entry.Name <> DirectoryListing.ParentName

let selectedEntries panel =
    panel.Entries
    |> Array.filter (fun e -> panel.Selected.Contains e.Name)
    |> List.ofArray

let private markedAtCursor (p: PanelState) =
    match cursorEntry p with
    | Some entry -> p.Selected.Contains entry.Name
    | None -> false

let private runFrom cursor target =
    if cursor < target then [ cursor .. target - 1 ]
    elif cursor > target then [ target + 1 .. cursor ]
    else [ cursor ]

let private namesOf indexes (p: PanelState) =
    indexes
    |> List.choose (fun index -> Array.tryItem index p.Entries)
    |> List.filter selectable
    |> List.map _.Name
    |> Set.ofList

let private toggleSelectedThrough target (p: PanelState) =
    let run = namesOf (runFrom p.Cursor target) p

    let selected =
        if markedAtCursor p then
            Set.difference p.Selected run
        else
            Set.union p.Selected run

    withCursorWithin target { p with Selected = selected }

let toggleSelectionThrough side target state =
    updatePanel side (toggleSelectedThrough target) state

let toggleSelection side state =
    toggleSelectionThrough side (panel side state).Cursor state

let private step direction =
    match direction with
    | Upward -> -1
    | Downward -> 1

let toggleSelectionMoving direction side state =
    toggleSelectionThrough side ((panel side state).Cursor + step direction) state

let private matching pattern panel =
    panel.Entries
    |> Array.filter (fun e -> selectable e && NamePattern.matches pattern e.Name)
    |> Array.map _.Name
    |> Set.ofArray

let private selectMatchingIn pattern (p: PanelState) =
    { p with Selected = Set.union p.Selected (matching pattern p) }

let private deselectMatchingIn pattern (p: PanelState) =
    { p with Selected = Set.difference p.Selected (matching pattern p) }

let applyPattern mode side pattern state =
    let apply =
        match mode with
        | Select -> selectMatchingIn
        | Deselect -> deselectMatchingIn

    updatePanel side (apply pattern) state

let private surviving entries selected =
    entries |> Array.map _.Name |> Set.ofArray |> Set.intersect selected

let private keepSurviving selected (p: PanelState) =
    { p with Selected = surviving p.Entries selected }

let private asOperationError error =
    match error with
    | DirectoryNotFound path -> Operations.SourceNotFound path
    | DirectoryListing.AccessDenied path -> Operations.AccessDenied path
    | ListingFailed(path, message) -> Operations.OperationFailed(path, message)
    | ArchiveUnreadable error -> Operations.ArchiveFailed error

let operationTargets panel =
    match selectedEntries panel with
    | [] -> cursorEntry panel |> Option.filter selectable |> Option.toList
    | marked -> marked

let private hitAt panel (entry: Entry) =
    match panel.Path with
    | SearchResults found -> SearchListing.hitFor found entry.Name
    | _ -> None

let private locationAbove (fs: IFileSystem) panel =
    match panel.Path with
    | OnDisk path -> FileSystemPath.parent fs path |> Option.map OnDisk
    | InArchive(archive, level) ->
        match ArchivePath.parent level with
        | Some above -> InArchive(archive, above) |> Some
        | None -> OnDisk(FileSystemPath.directory fs archive) |> Some
    | SearchResults found -> Some found.Origin

let private locationOfName (fs: IFileSystem) panel (name: EntryName) =
    match panel.Path with
    | SearchResults found ->
        SearchListing.hitFor found name
        |> Option.bind (fun hit -> PanelLocation.into fs hit.Location hit.Entry.Name)
    | location -> PanelLocation.into fs location name

let private locationOf (fs: IFileSystem) panel (entry: Entry) =
    if entry.Name = ParentName then
        locationAbove fs panel
    else
        locationOfName fs panel entry.Name

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

let showResults (fs: IFileSystem) side (results: FindResults) state =
    let previous = panel side state

    let found =
        { Query = results.Query
          Ground = results.Ground
          Hits = results.Hits
          Shown = FoundEntries.shownBy fs results.Ground results.Hits
          Origin = PanelLocation.origin previous.Path
          Returning = standingName previous }

    loadLocationFocusing fs side (SearchResults found) Top state

let markedDirectories (fs: IFileSystem) panel =
    selectedEntries panel
    |> List.filter (fun entry -> entry.Kind = Directory)
    |> List.choose (locationOf fs panel)

let markedGround panel =
    if selectedEntries panel |> List.exists (fun entry -> entry.Kind = Directory) then
        DirectoriesMarked
    else
        NoDirectoryMarked

let directoryOf panel = PanelLocation.directory panel.Path

let activeDirectory state = directoryOf (activePanel state)

let targetDirectory (state: AppState) = directoryOf (panel (opposite state.Active) state)

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
        hitAt panel entry
        |> Option.filter (fun hit -> PanelLocation.directory hit.Location |> Option.isSome)
        |> Option.map (FindHit.spelled fs)
    | location -> PanelLocation.combine fs location (EntryName.value entry.Name)

let operationPaths (fs: IFileSystem) panel =
    operationTargets panel |> List.choose (entryPath fs panel)

let cursorFilePath (fs: IFileSystem) panel =
    cursorEntry panel
    |> Option.filter (fun entry -> entry.Kind = File)
    |> Option.bind (entryPath fs panel)

let cursorExecutablePath (fs: IFileSystem) panel =
    cursorEntry panel
    |> Option.filter (fun entry -> entry.Kind = File && entry.Executability = Executable)
    |> Option.bind (entryPath fs panel)

let private insideArchive location (name: EntryName) =
    match location with
    | InArchive(archive, level) -> Some(archive, ArchivePath.child level name)
    | OnDisk _
    | SearchResults _ -> None

let private placeOf (hit: FindHit) = insideArchive hit.Location hit.Entry.Name

let private groupedByPlace (fs: IFileSystem) (hits: FindHit list) =
    hits
    |> List.groupBy (placeOf >> Option.map fst)
    |> List.map (fun (place, held) ->
        match place with
        | None -> FromDisk(held |> List.map (FindHit.spelled fs))
        | Some archive -> FromArchive(archive, held |> List.choose placeOf |> List.map snd))

let private fromResults (fs: IFileSystem) panel =
    match operationTargets panel |> List.choose (hitAt panel) |> groupedByPlace fs with
    | [] -> None
    | [ single ] -> Some single
    | several -> Some(FromPlaces several)

let transferSource (fs: IFileSystem) panel =
    match panel.Path, operationTargets panel with
    | _, [] -> None
    | OnDisk _, _ -> Some(FromDisk(operationPaths fs panel))
    | InArchive(archive, level), targets ->
        Some(FromArchive(archive, targets |> List.map (fun entry -> ArchivePath.child level entry.Name)))
    | SearchResults _, _ -> fromResults fs panel

let private entryInArchive (fs: IFileSystem) panel (entry: Entry) =
    match panel.Path with
    | SearchResults _ ->
        hitAt panel entry
        |> Option.bind (fun hit -> insideArchive hit.Location hit.Entry.Name)
    | location -> insideArchive location entry.Name

let cursorEntryInArchive (fs: IFileSystem) panel =
    cursorEntry panel |> Option.filter selectable |> Option.bind (entryInArchive fs panel)

let cursorFileInArchive (fs: IFileSystem) panel =
    cursorEntry panel
    |> Option.filter (fun entry -> entry.Kind = File)
    |> Option.bind (entryInArchive fs panel)

let showsResults panel =
    match panel.Path with
    | SearchResults _ -> true
    | _ -> false

let cursorDirectory (fs: IFileSystem) panel =
    match panel.Path with
    | SearchResults _ ->
        cursorEntry panel
        |> Option.bind (hitAt panel)
        |> Option.map (fun hit -> PanelLocation.holding fs hit.Location)
    | location -> PanelLocation.directory location

let cursorDirectoryPath (fs: IFileSystem) panel =
    cursorEntry panel
    |> Option.filter (fun entry -> entry.Kind = Directory && selectable entry)
    |> Option.bind (entryPath fs panel)

let measureTargets (fs: IFileSystem) panel =
    let considered =
        match selectedEntries panel with
        | [] -> cursorEntry panel |> Option.toList
        | marked -> marked

    considered
    |> List.filter (fun entry -> entry.Kind = Directory)
    |> List.choose (fun entry ->
        locationOf fs panel entry
        |> Option.map (fun location ->
            { Name = entry.Name
              Location = location }))

let applyMeasurements side (measured: Measurement list) state =
    let sizes = measured |> List.map (fun one -> one.Name, one.Size) |> Map.ofList

    let measuredEntry entry =
        match Map.tryFind entry.Name sizes with
        | Some size -> { entry with Measured = Some size }
        | None -> entry

    updatePanel side (fun p -> reorder { p with Entries = p.Entries |> Array.map measuredEntry }) state

type DeleteQuestion =
    { Probed: FileSystemPath list
      ReachesContents: bool }

let private directoryEntriesInArchive (fs: IFileSystem) panel =
    operationTargets panel
    |> List.exists (fun entry -> entry.Kind = Directory && entryInArchive fs panel entry |> Option.isSome)

let deleteQuestion (fs: IFileSystem) panel =
    match panel.Path with
    | OnDisk _ ->
        { Probed = operationPaths fs panel
          ReachesContents = false }
    | InArchive _ ->
        { Probed = []
          ReachesContents = operationTargets panel |> List.exists (fun entry -> entry.Kind = Directory) }
    | SearchResults _ ->
        { Probed = operationPaths fs panel
          ReachesContents = directoryEntriesInArchive fs panel }

let deleteScope (fs: IFileSystem) (question: DeleteQuestion) =
    if
        question.ReachesContents
        || question.Probed |> List.exists (Operations.isNonEmptyDirectory fs)
    then
        WithDirectoryContents
    else
        NamedEntriesOnly

let private sourceOf (fs: IFileSystem) panel (name: EntryName) =
    locationOfName fs panel name
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

    keepSurviving (Set.filter unfinished selected) p

let private reloadFocusing (fs: IFileSystem) side focus keep state =
    let previous = panel side state

    loadLocationFocusing fs side previous.Path focus state
    |> Result.mapError asOperationError
    |> Result.map (updatePanel side (keep previous))

let private reload (fs: IFileSystem) side keep state =
    reloadFocusing fs side (cursorFocus (panel side state)) keep state

let private keepingUnfinished (fs: IFileSystem) results (previous: PanelState) =
    keepUnfinished fs results previous.Selected

let private keepingSelected (previous: PanelState) = keepSurviving previous.Selected

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
        operationTargets (activePanel state)
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
        let standing = directoryOf (panel side state)

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
    |> Result.map (updatePanel side (keepSurviving previous.Selected))
    |> Result.map (fun updated ->
        { updated with
            LastCreatedDirectory = Some name })

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
        return updatePanel side (keepSurviving previous.Selected) loaded, path
    }

let private readAgain (fs: IFileSystem) side state =
    let panel = panel side state

    loadLocationFocusing fs side panel.Path (cursorFocus panel) state
    |> Result.map (updatePanel side (keepSurviving panel.Selected))

let refresh (fs: IFileSystem) (state: AppState) = readAgain fs state.Active state

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
    |> updatePanel side (fun p -> { p with Hidden = HiddenEntries.toggle p.Hidden })
    |> loadLocationFocusing fs side previous.Path (cursorFocus previous)
    |> Result.map (updatePanel side (keepSurviving previous.Selected))

let private navigateUp (fs: IFileSystem) side (from: FileSystemPath) state =
    match FileSystemPath.parent fs from with
    | None -> Ok state
    | Some parent ->
        let focus = from |> FileSystemPath.name fs |> ByName
        loadPanelFocusing fs side parent focus state

let private navigateInto (fs: IFileSystem) side (parentPath: FileSystemPath) name state =
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
    | Directory when entry.Name = DirectoryListing.ParentName -> navigateUp fs side path state
    | Directory -> navigateInto fs side path entry.Name state
    | File -> openArchive fs side (FileSystemPath.combine fs path (EntryName.value entry.Name)) state

let private enterInArchive (fs: IFileSystem) side archive level (entry: Entry) state =
    match entry.Kind with
    | Directory when entry.Name = DirectoryListing.ParentName -> climbLevel fs side archive level state
    | Directory -> loadLocationFocusing fs side (InArchive(archive, ArchivePath.child level entry.Name)) Top state
    | File -> Ok state

let private enterInResults (fs: IFileSystem) side panel found (entry: Entry) state =
    match entry.Kind, locationOf fs panel entry with
    | File, _
    | _, None -> Ok state
    | Directory, Some location when entry.Name = ParentName ->
        loadLocationFocusing fs side location (focusOn found.Returning) state
    | Directory, Some location -> loadLocationFocusing fs side location Top state

let enter (fs: IFileSystem) state =
    let panel = activePanel state

    match cursorEntry panel with
    | None -> Ok state
    | Some entry ->
        match panel.Path with
        | OnDisk path -> enterOnDisk fs state.Active path entry state
        | InArchive(archive, level) -> enterInArchive fs state.Active archive level entry state
        | SearchResults found -> enterInResults fs state.Active panel found entry state
