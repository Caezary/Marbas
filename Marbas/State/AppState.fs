module Marbas.State.AppState

open System.IO.Abstractions
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing

type PanelSide =
    | Left
    | Right

type PanelState =
    { Path: string
      Entries: Entry list }

type AppState =
    { Left: PanelState
      Right: PanelState
      Active: PanelSide }

let create path =
    let panel = { Path = path; Entries = [] }

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

let loadPanel (fs: IFileSystem) side path state =
    DirectoryListing.listForDisplay fs path
    |> Result.map (fun entries -> updatePanel side (fun _ -> { Path = path; Entries = entries }) state)
