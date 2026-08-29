module Marbas.Ui.App.Shell

open System.IO.Abstractions
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Ui.Keys
open Marbas.Ui.Messages

[<Literal>]
let SelectPrompt = "Select"

[<Literal>]
let DeselectPrompt = "Deselect"

[<Literal>]
let MkdirPrompt = "Create directory"

[<Literal>]
let NewFilePrompt = "Create file"

[<Literal>]
let RootPrompt = "Select root"

[<Literal>]
let MovePrompt = "Move or rename"

[<Literal>]
let CopyTitle = "Copying"

[<Literal>]
let MoveTitle = "Moving"

[<Literal>]
let PackTitle = "Packing"

[<Literal>]
let UnpackTitle = "Unpacking"

type Dependencies =
    { FileSystem: IFileSystem
      StartPath: FileSystemPath
      SettingsPath: FileSystemPath
      Roots: RootChoice
      OnQuit: unit -> unit
      ReportError: string -> string -> unit
      ChooseOption: string -> string list -> string option
      AskPattern: string -> string option
      AskName: string -> string -> string option
      AskTarget: string -> string -> string option
      Confirm: string -> string -> bool
      SendToTrash: FileSystemPath -> Result<unit, Operations.OperationError>
      ShowFile: FileWindow.Source -> unit
      RunEditor: EditorPath option -> FileSystemPath -> Result<unit, Operations.OperationError>
      EditProperties: EntryProperties -> EntryProperties option
      EditEditors: EditorSettings -> EditorPath option -> EditorSettings option
      AskOverwrite: string -> string -> Operations.OverwriteAnswer
      AskArchive: string -> ArchiveChoice option
      AskPassword: string -> string -> string option
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

type private PasswordOutcome =
    | NotNeeded
    | NotGiven
    | Given of password: ArchivePassword

let private passwordFor (deps: Dependencies) (archive: FileSystemPath) =
    if Archive.needsPassword deps.FileSystem archive then
        FileSystemPath.name deps.FileSystem archive
        |> PasswordQuestion.describe
        |> deps.AskPassword PasswordQuestion.Title
        |> Option.bind ArchivePassword.tryCreate
        |> Option.map Given
        |> Option.defaultValue NotGiven
    else
        NotNeeded

let private unpackTargets (deps: Dependencies) targetDirectory (archive: FileSystemPath) =
    let named path =
        FileSystemPath.name deps.FileSystem path |> ArchivePath.child ArchivePath.root

    match ArchiveListing.read deps.FileSystem archive ArchivePath.root with
    | Error _ -> [ ArchivePath.root ]
    | Ok entries ->
        entries
        |> List.map (_.Name >> EntryName.value >> FileSystemPath.combine deps.FileSystem archive)
        |> fun candidates -> Operations.approve deps.FileSystem (askOverwrite deps) candidates targetDirectory
        |> List.map named

let private unpackPlan (deps: Dependencies) targetDirectory archive =
    let planned password =
        match unpackTargets deps targetDirectory archive with
        | [] -> None
        | targets ->
            Some
                { Request =
                    { Archive = archive
                      Password = password
                      Destination = targetDirectory }
                  Targets = targets }

    match passwordFor deps archive with
    | NotNeeded -> planned None
    | Given password -> planned (Some password)
    | NotGiven -> None

let interpret (deps: Dependencies) (dispatch: Message -> unit) (effect: Effect) =
    match effect with
    | ReportListingError error ->
        deps.ReportError ListingErrorMessage.Title (ListingErrorMessage.describe error)
    | ReportOperationError error ->
        deps.ReportError OperationErrorMessage.Title (OperationErrorMessage.describe error)
    | ReportItemFailures failures ->
        deps.ReportError OperationErrorMessage.Title (OperationErrorMessage.describeFailures failures)
    | ConfirmDelete request ->
        if deps.Confirm (DeleteConfirmation.title request) (DeleteConfirmation.describe request) then
            dispatch (DeleteConfirmed request.Mode)
    | DeleteTargets(sources, mode) ->
        let remove =
            match mode with
            | ToTrash -> deps.SendToTrash
            | Permanently -> Operations.delete deps.FileSystem

        Operations.removeAll deps.FileSystem remove sources |> DeleteFinished |> dispatch
    | CopyTargets(sources, targetDirectory) ->
        startBatch deps CopyTitle Operations.copyAll (CopyFinished >> dispatch) sources targetDirectory
    | MoveTargets(sources, targetDirectory) ->
        startBatch deps MoveTitle Operations.moveAll (MoveFinished >> dispatch) sources targetDirectory
    | PromptForArchive suggestion ->
        match deps.AskArchive(FileSystemPath.value suggestion) with
        | Some choice -> dispatch (ArchiveEntered choice)
        | None -> ()
    | ArchiveTargets(sources, archive) ->
        let target = archive.Path

        let allowed =
            FileSystemPath.kind deps.FileSystem target = NoEntry
            || deps.Confirm OverwriteQuestion.Title (OverwriteQuestion.describe target)

        if allowed then
            deps.RunBatch PackTitle (fun control -> Archive.write deps.FileSystem control archive sources) (fun results ->
                dispatch (PackFinished(FileSystemPath.name deps.FileSystem target, results)))
    | ExtractTargets(archives, targetDirectory) ->
        match archives |> List.choose (unpackPlan deps targetDirectory) with
        | [] -> ()
        | plans ->
            deps.RunBatch
                UnpackTitle
                (fun control -> Archive.extractAll deps.FileSystem control plans)
                (UnpackFinished >> dispatch)
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
    | EditFile(path, editor) -> deps.RunEditor editor path |> EditFinished |> dispatch
    | ShowProperties properties ->
        match deps.EditProperties properties with
        | Some changed -> dispatch (PropertiesEntered changed)
        | None -> ()
    | ShowSettings(editors, candidate) ->
        match deps.EditEditors editors candidate with
        | Some changed -> dispatch (EditorsEntered changed)
        | None -> ()
    | PromptForTarget suggestion ->
        match deps.AskTarget MovePrompt (FileSystemPath.value suggestion) with
        | Some text -> dispatch (TargetEntered text)
        | None -> ()
    | PromptForName suggestion ->
        let offered = suggestion |> Option.map EntryName.value |> Option.defaultValue ""

        match deps.AskName MkdirPrompt offered |> Option.bind EntryName.tryCreate with
        | Some name -> dispatch (NameEntered name)
        | None -> ()
    | PromptForFileName ->
        match deps.AskName NewFilePrompt "" |> Option.bind EntryName.tryCreate with
        | Some name -> dispatch (FileNameEntered name)
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
