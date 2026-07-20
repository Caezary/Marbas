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

/// Loads `path` into `side`, placing the cursor on `cursorOn` when that entry is present.
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

/// Navigates the active panel into the directory under its cursor.
/// Entering ".." moves up and puts the cursor on the directory just left.
let enter (fs: IFileSystem) state =
    let panel = activePanel state

    match panel.Entries |> List.tryItem panel.Cursor with
    | Some entry when entry.Kind = Directory ->
        let current = fs.DirectoryInfo.New panel.Path

        if entry.Name = ".." then
            match current.Parent with
            | null -> Ok state
            | parent -> loadPanelFocusing fs state.Active parent.FullName (Some current.Name) state
        else
            let target = fs.Path.Combine(panel.Path, entry.Name)
            loadPanelFocusing fs state.Active target None state
    | _ -> Ok state
