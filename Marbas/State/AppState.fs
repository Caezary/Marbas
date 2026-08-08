module Marbas.State.AppState

open System.IO.Abstractions
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing

type PanelSide =
    | Left
    | Right

type PanelState =
    { Path: FileSystemPath
      Entries: Entry list
      Cursor: int
      Selected: Set<string> }

type AppState =
    { Left: PanelState
      Right: PanelState
      Active: PanelSide }

type FocusTarget =
    | Top
    | ByName of string

let create (path: FileSystemPath) =
    let panel =
        { Path = path
          Entries = []
          Cursor = 0
          Selected = Set.empty }

    { Left = panel
      Right = panel
      Active = Left }

let panel side state =
    match side with
    | Left -> state.Left
    | Right -> state.Right

let activePanel state = panel state.Active state

let private updatePanel side f state =
    match side with
    | Left -> { state with Left = f state.Left }
    | Right -> { state with Right = f state.Right }

let activate side state = { state with Active = side }

let toggleActive state =
    { state with
        Active =
            match state.Active with
            | Left -> Right
            | Right -> Left }

let private withCursor index (p: PanelState) = { p with Cursor = index }

let private moveCursorBy delta (p: PanelState) =
    { p with Cursor = max 0 (min (p.Cursor + delta) (p.Entries.Length - 1)) }

let setCursor side index state =
    updatePanel side (withCursor index) state

let loadPanelFocusing (fs: IFileSystem) side (path: FileSystemPath) focus state =
    DirectoryListing.listForDisplay fs path
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
                  Selected = Set.empty })
            state)

let loadPanel fs side (path: FileSystemPath) state =
    loadPanelFocusing fs side path Top state

let cursorEntry panel = panel.Entries |> List.tryItem panel.Cursor

let private cursorFocus panel =
    match cursorEntry panel with
    | Some entry -> ByName entry.Name
    | None -> Top

let private selectable entry = entry.Name <> DirectoryListing.ParentName

let selectedEntries panel =
    panel.Entries |> List.filter (fun e -> panel.Selected.Contains e.Name)

let private toggle name selected =
    if Set.contains name selected then
        Set.remove name selected
    else
        Set.add name selected

let private toggleSelectedAtCursor (p: PanelState) =
    match cursorEntry p with
    | Some entry when selectable entry -> { p with Selected = toggle entry.Name p.Selected }
    | _ -> p

let toggleSelection side state =
    updatePanel side toggleSelectedAtCursor state

let advanceCursor side state =
    updatePanel side (moveCursorBy 1) state

let toggleSelectionAndAdvance side state =
    state |> toggleSelection side |> advanceCursor side

let private matching pattern panel =
    panel.Entries
    |> List.filter (fun e -> selectable e && NamePattern.matches pattern e.Name)
    |> List.map _.Name
    |> Set.ofList

let private selectMatchingIn pattern (p: PanelState) =
    { p with Selected = Set.union p.Selected (matching pattern p) }

let private deselectMatchingIn pattern (p: PanelState) =
    { p with Selected = Set.difference p.Selected (matching pattern p) }

let selectMatching side pattern state =
    updatePanel side (selectMatchingIn pattern) state

let deselectMatching side pattern state =
    updatePanel side (deselectMatchingIn pattern) state

let private surviving entries selected =
    entries |> List.map _.Name |> Set.ofList |> Set.intersect selected

let private keepSurviving selected (p: PanelState) =
    { p with Selected = surviving p.Entries selected }

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
    FileSystemPath.combine fs panel.Path entry.Name

let hasNonEmptyDirectory (fs: IFileSystem) panel =
    operationTargets panel
    |> List.exists (entryPath fs panel >> Operations.isNonEmptyDirectory fs)

let deleteTargets (fs: IFileSystem) side state =
    let previous = panel side state

    let results =
        operationTargets previous
        |> List.map (entryPath fs previous)
        |> Operations.deleteAll fs

    loadPanelFocusing fs side previous.Path (cursorFocus previous) state
    |> Result.mapError asOperationError
    |> Result.map (updatePanel side (keepSurviving previous.Selected))
    |> Result.map (fun updated -> updated, results)

let makeDirectory (fs: IFileSystem) side name state : Result<AppState, Operations.OperationError> =
    let previous = panel side state

    Operations.makeDirectory fs previous.Path name
    |> Result.bind (fun () ->
        loadPanelFocusing fs side previous.Path (ByName name) state
        |> Result.mapError asOperationError
        |> Result.map (updatePanel side (keepSurviving previous.Selected)))

let refresh (fs: IFileSystem) state =
    let panel = activePanel state

    loadPanelFocusing fs state.Active panel.Path (cursorFocus panel) state
    |> Result.map (updatePanel state.Active (keepSurviving panel.Selected))

let private navigateUp (fs: IFileSystem) side (from: FileSystemPath) state =
    match FileSystemPath.parent fs from with
    | None -> Ok state
    | Some parent ->
        let focus = from |> FileSystemPath.name fs |> ByName
        loadPanelFocusing fs side parent focus state

let private navigateInto (fs: IFileSystem) side (parentPath: FileSystemPath) name state =
    let targetPath = FileSystemPath.combine fs parentPath name
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
