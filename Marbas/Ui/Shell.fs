module Marbas.Ui.Shell

open System.IO.Abstractions
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.State.Update

[<Literal>]
let SelectPrompt = "Select"

[<Literal>]
let DeselectPrompt = "Deselect"

[<Literal>]
let MkdirPrompt = "Create directory"

[<Literal>]
let RootPrompt = "Select root"

[<Literal>]
let MovePrompt = "Move or rename"

[<Literal>]
let CopyTitle = "Copying"

[<Literal>]
let MoveTitle = "Moving"

type Dependencies =
    { FileSystem: IFileSystem
      StartPath: FileSystemPath
      SettingsPath: FileSystemPath
      Roots: RootChoice
      OnQuit: unit -> unit
      ReportError: string -> string -> unit
      ChooseOption: string -> string list -> string option
      AskPattern: string -> string option
      AskName: string -> string option
      AskTarget: string -> string -> string option
      Confirm: string -> string -> bool
      ShowFile: FileWindow.Source -> unit
      RunEditor: FileSystemPath -> Result<unit, Operations.OperationError>
      AskOverwrite: string -> string -> Operations.OverwriteAnswer
      WatchModifiers: (Set<Modifier> -> unit) -> unit
      RunBatch:
          string -> (Operations.BatchControl -> Operations.ItemResult list) -> (Operations.ItemResult list -> unit) -> unit }

let private patternPrompt mode =
    match mode with
    | Select -> SelectPrompt
    | Deselect -> DeselectPrompt

let private askOverwrite (deps: Dependencies) target =
    deps.AskOverwrite OverwriteQuestion.Title (OverwriteQuestion.describe target)

let private startBatch (deps: Dependencies) title operation finished sources targetDirectory =
    match Operations.approve deps.FileSystem (askOverwrite deps) sources targetDirectory with
    | [] -> ()
    | approved -> deps.RunBatch title (fun control -> operation deps.FileSystem control approved targetDirectory) finished

let interpret (deps: Dependencies) (dispatch: Message -> unit) (effect: Effect) =
    match effect with
    | ReportListingError error ->
        deps.ReportError ListingErrorMessage.Title (ListingErrorMessage.describe error)
    | ReportOperationError error ->
        deps.ReportError OperationErrorMessage.Title (OperationErrorMessage.describe error)
    | ReportItemFailures failures ->
        deps.ReportError OperationErrorMessage.Title (OperationErrorMessage.describeFailures failures)
    | ConfirmDelete request ->
        if deps.Confirm DeleteConfirmation.Title (DeleteConfirmation.describe request) then
            dispatch DeleteConfirmed
    | CopyTargets(sources, targetDirectory) ->
        startBatch deps CopyTitle Operations.copyAll (CopyFinished >> dispatch) sources targetDirectory
    | MoveTargets(sources, targetDirectory) ->
        startBatch deps MoveTitle Operations.moveAll (MoveFinished >> dispatch) sources targetDirectory
    | RenameTarget(source, newName) ->
        Operations.rename deps.FileSystem source newName
        |> fun outcome -> RenameFinished(newName, outcome)
        |> dispatch
    | ViewFile path ->
        match FileWindow.openFile deps.FileSystem path with
        | Ok source ->
            use source = source
            deps.ShowFile source
        | Error error -> deps.ReportError ContentErrorMessage.Title (ContentErrorMessage.describe error)
    | EditFile path -> deps.RunEditor path |> EditFinished |> dispatch
    | PromptForTarget suggestion ->
        match deps.AskTarget MovePrompt (FileSystemPath.value suggestion) with
        | Some text -> dispatch (TargetEntered text)
        | None -> ()
    | PromptForName ->
        match deps.AskName MkdirPrompt |> Option.bind EntryName.tryCreate with
        | Some name -> dispatch (NameEntered name)
        | None -> ()
    | PromptForPattern mode ->
        match deps.AskPattern (patternPrompt mode) |> Option.bind NamePattern.tryCreate with
        | Some pattern -> dispatch (PatternEntered(mode, pattern))
        | None -> ()
    | PromptForRoot side ->
        let roots = Roots.list deps.FileSystem deps.Roots |> List.map FileSystemPath.value

        match deps.ChooseOption RootPrompt roots with
        | Some root -> dispatch (PathOpened(side, FileSystemPath.create root))
        | None -> ()
    | SaveSettings settings -> Settings.save deps.FileSystem deps.SettingsPath settings
    | Quit -> deps.OnQuit()
