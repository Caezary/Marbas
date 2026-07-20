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
      Cursor: int }

type AppState =
    { Left: PanelState
      Right: PanelState
      Active: PanelSide }

let create path =
    let panel =
        { Path = path
          Entries = []
          Cursor = 0 }

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
                  Cursor = cursor })
            state)

let loadPanel fs side path state =
    loadPanelFocusing fs side path None state

let cursorEntry panel = panel.Entries |> List.tryItem panel.Cursor

let private cursorEntryName panel = cursorEntry panel |> Option.map _.Name

let refresh (fs: IFileSystem) state =
    let panel = activePanel state
    loadPanelFocusing fs state.Active panel.Path (cursorEntryName panel) state

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
