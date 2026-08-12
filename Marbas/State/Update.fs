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
    | PatternRequested of PanelSide * index: int * SelectionMode
    | PatternEntered of PanelSide * index: int * SelectionMode * NamePattern
    | MakeDirectoryRequested
    | NameEntered of name: EntryName
    | DeleteRequested
    | DeleteConfirmed
    | CopyRequested
    | CopyFinished of Operations.ItemResult list
    | MoveRequested
    | TargetEntered of text: string
    | MoveFinished of Operations.ItemResult list
    | RenameFinished of newName: EntryName * outcome: Result<unit, Operations.OperationError>
    | DriveRequested of PanelSide
    | PathOpened of PanelSide * FileSystemPath
    | QuitRequested

type DeleteRequest =
    { Targets: EntryName list
      Scope: DeleteScope }

type Effect =
    | ReportListingError of ListingError
    | ReportOperationError of Operations.OperationError
    | ReportItemFailures of Operations.ItemResult list
    | ConfirmDelete of DeleteRequest
    | CopyTargets of sources: FileSystemPath list * targetDirectory: FileSystemPath
    | MoveTargets of sources: FileSystemPath list * targetDirectory: FileSystemPath
    | RenameTarget of source: FileSystemPath * newName: EntryName
    | PromptForTarget of suggestion: FileSystemPath
    | PromptForName
    | PromptForPattern of PanelSide * index: int * SelectionMode
    | PromptForDrive of PanelSide
    | Quit

type UpdateResult =
    { State: AppState
      Effects: Effect list }

let private stay state = { State = state; Effects = [] }

let private request state effect = { State = state; Effects = [ effect ] }

let private atCursor side index = activate side >> setCursor side index

let private otherPath state =
    (panel (opposite state.Active) state).Path

let private applyListing state result =
    match result with
    | Ok updated -> stay updated
    | Error error -> request state (ReportListingError error)

let private applyOperation state result =
    match result with
    | Ok updated -> stay updated
    | Error error -> request state (ReportOperationError error)

let private applyBatch state result =
    match result with
    | Error error -> request state (ReportOperationError error)
    | Ok(updated, results) ->
        match Operations.failures results with
        | [] -> stay updated
        | failed -> request updated (ReportItemFailures failed)

let update (fs: IFileSystem) (msg: Message) (state: AppState) : UpdateResult =
    match msg with
    | CursorMoved(side, index) -> stay (setCursor side index state)
    | PanelFocused side -> stay (activate side state)
    | EntryOpened(side, index) -> state |> atCursor side index |> enter fs |> applyListing state
    | RefreshRequested(side, index) -> state |> atCursor side index |> refresh fs |> applyListing state
    | SelectionToggled(side, index) -> stay (state |> atCursor side index |> toggleSelectionAndAdvance side)
    | PatternRequested(side, index, mode) -> request state (PromptForPattern(side, index, mode))
    | PatternEntered(side, index, mode, pattern) ->
        stay (state |> atCursor side index |> applyPattern mode side pattern)
    | MakeDirectoryRequested -> request state PromptForName
    | NameEntered name -> makeDirectory fs state.Active name state |> applyOperation state
    | DeleteRequested ->
        let target = activePanel state

        match operationTargets target with
        | [] -> stay state
        | targets ->
            request
                state
                (ConfirmDelete
                    { Targets = targets |> List.map _.Name
                      Scope = deleteScope fs target })
    | DeleteConfirmed -> deleteTargets fs state.Active state |> applyBatch state
    | CopyRequested ->
        match operationPaths fs (activePanel state) with
        | [] -> stay state
        | paths -> request state (CopyTargets(paths, otherPath state))
    | CopyFinished results -> finishTransfer fs results state |> applyBatch state
    | MoveRequested ->
        match operationPaths fs (activePanel state) with
        | [] -> stay state
        | _ -> request state (PromptForTarget(otherPath state))
    | TargetEntered text ->
        let source = activePanel state

        match text.Trim(), operationPaths fs source with
        | "", _
        | _, [] -> stay state
        | trimmed, [ single ] when FileSystemPath.isBareName fs trimmed ->
            request state (RenameTarget(single, EntryName.create trimmed))
        | trimmed, paths -> request state (MoveTargets(paths, FileSystemPath.combine fs source.Path trimmed))
    | MoveFinished results -> finishTransfer fs results state |> applyBatch state
    | RenameFinished(newName, outcome) ->
        match outcome with
        | Error error -> request state (ReportOperationError error)
        | Ok() -> finishRename fs newName state |> applyOperation state
    | DriveRequested side -> request state (PromptForDrive side)
    | PathOpened(side, path) -> loadPanel fs side path state |> applyListing state
    | QuitRequested -> request state Quit

let step (fs: IFileSystem) (msg: Message) (state: AppState) =
    let result = update fs msg state
    result.State, result.Effects
