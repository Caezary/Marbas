module Marbas.State.AppState

open System.IO.Abstractions
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing

type PanelSide =
    | Left
    | Right

type PanelState =
    { Path: string
      Entries: Entry list
      Cursor: int
      Selected: Set<string> }

type AppState =
    { Left: PanelState
      Right: PanelState
      Active: PanelSide }

let create path =
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

let setCursor side index state =
    updatePanel side (fun p -> { p with Cursor = index }) state

let loadPanelFocusing (fs: IFileSystem) side path cursorOn state =
    DirectoryListing.listForDisplay fs path
    |> Result.map (fun entries ->
        let cursor =
            cursorOn
            |> Option.bind (fun name -> entries |> List.tryFindIndex (fun e -> e.Name = name))
            |> Option.defaultValue 0

        updatePanel
            side
            (fun _ ->
                { Path = path
                  Entries = entries
                  Cursor = cursor
                  Selected = Set.empty })
            state)

let loadPanel fs side path state =
    loadPanelFocusing fs side path None state

let cursorEntry panel = panel.Entries |> List.tryItem panel.Cursor

let private cursorEntryName panel = cursorEntry panel |> Option.map _.Name

let private selectable entry = entry.Name <> DirectoryListing.ParentName

let selectedEntries panel =
    panel.Entries |> List.filter (fun e -> panel.Selected.Contains e.Name)

let private toggle name selected =
    if Set.contains name selected then
        Set.remove name selected
    else
        Set.add name selected

let toggleSelection side state =
    updatePanel
        side
        (fun p ->
            match cursorEntry p with
            | Some entry when selectable entry ->
                { p with Selected = toggle entry.Name p.Selected }
            | _ -> p)
        state

let advanceCursor side state =
    updatePanel side (fun p -> { p with Cursor = max 0 (min (p.Cursor + 1) (p.Entries.Length - 1)) }) state

let toggleSelectionAndAdvance side state =
    state |> toggleSelection side |> advanceCursor side

let private matching pattern panel =
    panel.Entries
    |> List.filter (fun e -> selectable e && NamePattern.matches pattern e.Name)
    |> List.map _.Name
    |> Set.ofList

let selectMatching side pattern state =
    updatePanel side (fun p -> { p with Selected = Set.union p.Selected (matching pattern p) }) state

let deselectMatching side pattern state =
    updatePanel side (fun p -> { p with Selected = Set.difference p.Selected (matching pattern p) }) state

let private surviving entries selected =
    entries |> List.map _.Name |> Set.ofList |> Set.intersect selected

let refresh (fs: IFileSystem) state =
    let panel = activePanel state

    loadPanelFocusing fs state.Active panel.Path (cursorEntryName panel) state
    |> Result.map (
        updatePanel state.Active (fun p ->
            { p with Selected = surviving p.Entries panel.Selected }))

let private navigateUp (fs: IFileSystem) side (from: string) state =
    let current = fs.DirectoryInfo.New from

    match current.Parent with
    | null -> Ok state
    | parent -> loadPanelFocusing fs side parent.FullName (Some current.Name) state

let private navigateInto (fs: IFileSystem) side parentPath name state =
    loadPanelFocusing fs side (fs.Path.Combine(parentPath, name)) None state

let enter (fs: IFileSystem) state =
    let panel = activePanel state

    match panel.Entries |> List.tryItem panel.Cursor with
    | Some entry when entry.Kind = Directory ->
        if entry.Name = DirectoryListing.ParentName then
            navigateUp fs state.Active panel.Path state
        else
            navigateInto fs state.Active panel.Path entry.Name state
    | _ -> Ok state
