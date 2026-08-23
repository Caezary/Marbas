module Marbas.State.AppState

open System.IO.Abstractions
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing

type PanelSide =
    | Left
    | Right

type PanelCursor =
    { Side: PanelSide
      Index: int }

type SelectionMode =
    | Select
    | Deselect

type CursorDirection =
    | Upward
    | Downward

type DeleteScope =
    | NamedEntriesOnly
    | WithDirectoryContents

type DeleteMode =
    | ToTrash
    | Permanently

type PanelState =
    { Path: FileSystemPath
      Entries: Entry list
      Cursor: int
      Selected: Set<EntryName>
      Sort: SortOrder
      Hidden: HiddenEntries
      Search: SearchText option }

type AppState =
    { Left: PanelState
      Right: PanelState
      Active: PanelSide
      LastCreatedDirectory: EntryName option }

type FocusTarget =
    | Top
    | ByName of EntryName

let private panelFrom (settings: PanelSettings) =
    { Path = settings.Path
      Entries = []
      Cursor = 0
      Selected = Set.empty
      Sort = settings.Sort
      Hidden = settings.Hidden
      Search = None }

let fromSettings (settings: Settings) =
    { Left = panelFrom settings.Left
      Right = panelFrom settings.Right
      Active = Left
      LastCreatedDirectory = settings.LastCreatedDirectory }

let create (path: FileSystemPath) = fromSettings (Settings.forPath path)

let private panelSettings (p: PanelState) : PanelSettings =
    { Path = p.Path
      Sort = p.Sort
      Hidden = p.Hidden }

let settings (state: AppState) : Settings =
    { Left = panelSettings state.Left
      Right = panelSettings state.Right
      LastCreatedDirectory = state.LastCreatedDirectory }

let panel side state =
    match side with
    | Left -> state.Left
    | Right -> state.Right

let activePanel state = panel state.Active state

let private updatePanel side f (state: AppState) =
    match side with
    | Left -> { state with Left = f state.Left }
    | Right -> { state with Right = f state.Right }

let private withoutSearch (p: PanelState) = { p with Search = None }

let closeSearch side state = updatePanel side withoutSearch state

let activate side state =
    { state with Active = side } |> closeSearch Left |> closeSearch Right

let opposite side =
    match side with
    | Left -> Right
    | Right -> Left

let toggleActive state = activate (opposite state.Active) state

let private withCursor index (p: PanelState) = { p with Cursor = index }

let private withCursorWithin index (p: PanelState) =
    { p with Cursor = index |> min (p.Entries.Length - 1) |> max 0 }

let setCursor side index state =
    updatePanel side (withCursor index) state

let private focusFirstMatch (p: PanelState) =
    match p.Search with
    | None -> p
    | Some text ->
        match p.Entries |> List.tryFindIndex (fun e -> SearchText.matches text e.Name) with
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

let loadPanelFocusing (fs: IFileSystem) side (path: FileSystemPath) focus state =
    let previous = panel side state
    let sort = previous.Sort
    let hidden = previous.Hidden

    DirectoryListing.listForDisplay fs sort hidden path
    |> Result.map (fun entries ->
        let cursor =
            match focus with
            | ByName name -> entries |> List.tryFindIndex (fun e -> e.Name = name) |> Option.defaultValue 0
            | Top -> 0

        updatePanel
            side
            (fun _ ->
                { Path = path
                  Entries = entries
                  Cursor = cursor
                  Selected = Set.empty
                  Sort = sort
                  Hidden = hidden
                  Search = None })
            state)

let loadPanel fs side (path: FileSystemPath) state =
    loadPanelFocusing fs side path Top state

let cursorEntry panel = panel.Entries |> List.tryItem panel.Cursor

let private cursorFocus panel =
    match cursorEntry panel with
    | Some entry -> ByName entry.Name
    | None -> Top

let private reorder (p: PanelState) =
    let entries = DirectoryListing.order p.Sort p.Entries

    let cursor =
        cursorEntry p
        |> Option.bind (fun entry -> entries |> List.tryFindIndex (fun e -> e.Name = entry.Name))
        |> Option.defaultValue 0

    { p with
        Entries = entries
        Cursor = cursor }

let sortBy (key: SortKey) side state =
    updatePanel side (fun p -> reorder { p with Sort = SortOrder.switchTo key p.Sort }) state

let private selectable entry = entry.Name <> DirectoryListing.ParentName

let selectedEntries panel =
    panel.Entries |> List.filter (fun e -> panel.Selected.Contains e.Name)

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
    |> List.choose (fun index -> List.tryItem index p.Entries)
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
    |> List.filter (fun e -> selectable e && NamePattern.matches pattern e.Name)
    |> List.map _.Name
    |> Set.ofList

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
    entries |> List.map _.Name |> Set.ofList |> Set.intersect selected

let private keepSurviving selected (p: PanelState) =
    { p with Selected = surviving p.Entries selected }

let private finishedNames (results: Operations.ItemResult list) =
    results |> List.filter (_.Outcome >> Result.isOk) |> List.map _.Name |> Set.ofList

let private keepUnfinished results selected (p: PanelState) =
    keepSurviving (Set.difference selected (finishedNames results)) p

let private asOperationError error =
    match error with
    | DirectoryNotFound path -> Operations.SourceNotFound path
    | DirectoryListing.AccessDenied path -> Operations.AccessDenied path
    | ListingFailed(path, message) -> Operations.OperationFailed(path, message)

let operationTargets panel =
    match selectedEntries panel with
    | [] -> cursorEntry panel |> Option.filter selectable |> Option.toList
    | marked -> marked

let private entryPath (fs: IFileSystem) panel (entry: Entry) =
    FileSystemPath.combine fs panel.Path (EntryName.value entry.Name)

let operationPaths (fs: IFileSystem) panel =
    operationTargets panel |> List.map (entryPath fs panel)

let cursorFilePath (fs: IFileSystem) panel =
    cursorEntry panel
    |> Option.filter (fun entry -> entry.Kind = File)
    |> Option.map (entryPath fs panel)

let deleteScope (fs: IFileSystem) panel =
    let reachesContents =
        operationTargets panel
        |> List.exists (entryPath fs panel >> Operations.isNonEmptyDirectory fs)

    if reachesContents then
        WithDirectoryContents
    else
        NamedEntriesOnly

let private reloadFocusing (fs: IFileSystem) side focus keep state =
    let previous = panel side state

    loadPanelFocusing fs side previous.Path focus state
    |> Result.mapError asOperationError
    |> Result.map (updatePanel side (keep previous))

let private reload (fs: IFileSystem) side keep state =
    reloadFocusing fs side (cursorFocus (panel side state)) keep state

let finishDelete (fs: IFileSystem) results state =
    reload fs state.Active (fun previous -> keepUnfinished results previous.Selected) state
    |> Result.map (fun updated -> updated, results)

let finishTransfer (fs: IFileSystem) results state =
    reload fs state.Active (fun previous -> keepUnfinished results previous.Selected) state
    |> Result.bind (reload fs (opposite state.Active) (fun previous -> keepSurviving previous.Selected))
    |> Result.map (fun updated -> updated, results)

let finishRename (fs: IFileSystem) newName state =
    reloadFocusing fs state.Active (ByName newName) (fun previous -> keepSurviving previous.Selected) state

let makeDirectory (fs: IFileSystem) side name state : Result<AppState, Operations.OperationError> =
    let previous = panel side state

    Operations.makeDirectory fs previous.Path name
    |> Result.bind (fun () ->
        loadPanelFocusing fs side previous.Path (ByName name) state
        |> Result.mapError asOperationError
        |> Result.map (updatePanel side (keepSurviving previous.Selected))
        |> Result.map (fun updated ->
            { updated with
                LastCreatedDirectory = Some name }))

let refresh (fs: IFileSystem) state =
    let panel = activePanel state

    loadPanelFocusing fs state.Active panel.Path (cursorFocus panel) state
    |> Result.map (updatePanel state.Active (keepSurviving panel.Selected))

let toggleHidden (fs: IFileSystem) side state =
    let previous = panel side state

    state
    |> updatePanel side (fun p -> { p with Hidden = HiddenEntries.toggle p.Hidden })
    |> loadPanelFocusing fs side previous.Path (cursorFocus previous)
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

let enter (fs: IFileSystem) state =
    let panel = activePanel state

    match panel.Entries |> List.tryItem panel.Cursor with
    | Some entry when entry.Kind = Directory ->
        if entry.Name = DirectoryListing.ParentName then
            navigateUp fs state.Active panel.Path state
        else
            navigateInto fs state.Active panel.Path entry.Name state
    | _ -> Ok state
