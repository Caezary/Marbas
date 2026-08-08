module Marbas.Ui.Shell

open System.IO.Abstractions
open Marbas.FileSystem
open Marbas.State.Update

[<Literal>]
let SelectPrompt = "Select"

[<Literal>]
let DeselectPrompt = "Deselect"

[<Literal>]
let MkdirPrompt = "Create directory"

[<Literal>]
let DrivePrompt = "Select drive"

type Dependencies =
    { FileSystem: IFileSystem
      StartPath: string
      OnQuit: unit -> unit
      ReportError: string -> string -> unit
      ChooseOption: string -> string list -> string option
      AskPattern: string -> string option
      AskName: string -> string option
      Confirm: string -> string -> bool
      AskOverwrite: string -> string -> Operations.OverwriteAnswer }

let interpret (deps: Dependencies) (dispatch: Message -> unit) (effect: Effect) =
    match effect with
    | ReportListingError error ->
        deps.ReportError ListingErrorMessage.Title (ListingErrorMessage.describe error)
    | ReportOperationError error ->
        deps.ReportError OperationErrorMessage.Title (OperationErrorMessage.describe error)
    | ReportItemFailures failures ->
        deps.ReportError OperationErrorMessage.Title (OperationErrorMessage.describeFailures failures)
    | ConfirmDelete(names, nonEmptyDirectory) ->
        if deps.Confirm DeleteConfirmation.Title (DeleteConfirmation.describe names nonEmptyDirectory) then
            dispatch DeleteConfirmed
    | CopyTargets(sources, targetDirectory) ->
        let ask target =
            deps.AskOverwrite OverwriteQuestion.Title (OverwriteQuestion.describe target)

        Operations.copyAll deps.FileSystem ask sources targetDirectory
        |> CopyFinished
        |> dispatch
    | PromptForName ->
        match deps.AskName MkdirPrompt with
        | Some name -> dispatch (NameEntered name)
        | None -> ()
    | PromptForPattern(side, index, selecting) ->
        match deps.AskPattern (if selecting then SelectPrompt else DeselectPrompt) with
        | Some pattern -> dispatch (PatternEntered(side, index, selecting, pattern))
        | None -> ()
    | PromptForDrive side ->
        match deps.ChooseOption DrivePrompt (Drives.list deps.FileSystem) with
        | Some drive -> dispatch (PathOpened(side, drive))
        | None -> ()
    | Quit -> deps.OnQuit()
