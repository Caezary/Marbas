module Marbas.State.Startup

open Marbas.Domain
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.State.Ports

let restored (deps: Dependencies) =
    Settings.load deps.FileSystem (Settings.forPath deps.StartPath) deps.SettingsPath
    |> restore

let private opening (state: AppState) side =
    (panel side state).Path
    |> PanelLocation.directory
    |> Option.map (fun path -> PathOpened(side, path))

let private openings (state: AppState) =
    [ Left; Right ] |> List.choose (opening state)

let firstSteps (deps: Dependencies) (dispatch: Message -> unit) (standing: unit -> AppState) =
    standing () |> openings |> List.iter dispatch

    standing ()
    |> watchedLocations
    |> WatchLocations
    |> Shell.interpret deps dispatch

    Shell.interpret deps dispatch SweepLeftovers
