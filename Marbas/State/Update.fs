module Marbas.State.Update

open System.IO.Abstractions
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.State.AppState

type Message =
    | CursorMoved of PanelSide * index: int
    | PanelFocused of PanelSide
    | EntryOpened of PanelSide * index: int
    | RefreshRequested of PanelSide * index: int
    | SelectionToggled of PanelSide * index: int
    | PatternRequested of PanelSide * index: int * selecting: bool
    | PatternEntered of PanelSide * index: int * selecting: bool * pattern: string
    | MakeDirectoryRequested
    | NameEntered of name: string
    | DriveRequested of PanelSide
    | PathOpened of PanelSide * path: string
    | QuitRequested

type Effect =
    | ReportListingError of ListingError
    | ReportOperationError of Operations.OperationError
    | PromptForName
    | PromptForPattern of PanelSide * index: int * selecting: bool
    | PromptForDrive of PanelSide
    | Quit

type UpdateResult =
    { State: AppState
      Effects: Effect list }

let private stay state = { State = state; Effects = [] }

let private request state effect = { State = state; Effects = [ effect ] }

let private atCursor side index = activate side >> setCursor side index

let private applyListing state result =
    match result with
    | Ok updated -> stay updated
    | Error error -> request state (ReportListingError error)

let private applyOperation state result =
    match result with
    | Ok updated -> stay updated
    | Error error -> request state (ReportOperationError error)

let update (fs: IFileSystem) (msg: Message) (state: AppState) : UpdateResult =
    match msg with
    | CursorMoved(side, index) -> stay (setCursor side index state)
    | PanelFocused side -> stay (activate side state)
    | EntryOpened(side, index) -> state |> atCursor side index |> enter fs |> applyListing state
    | RefreshRequested(side, index) -> state |> atCursor side index |> refresh fs |> applyListing state
    | SelectionToggled(side, index) -> stay (state |> atCursor side index |> toggleSelectionAndAdvance side)
    | PatternRequested(side, index, selecting) -> request state (PromptForPattern(side, index, selecting))
    | PatternEntered(side, index, selecting, pattern) ->
        let select = if selecting then selectMatching else deselectMatching
        stay (state |> atCursor side index |> select side pattern)
    | MakeDirectoryRequested -> request state PromptForName
    | NameEntered name ->
        match name.Trim() with
        | "" -> stay state
        | trimmed -> makeDirectory fs state.Active trimmed state |> applyOperation state
    | DriveRequested side -> request state (PromptForDrive side)
    | PathOpened(side, path) -> loadPanel fs side (FileSystemPath.create path) state |> applyListing state
    | QuitRequested -> request state Quit
