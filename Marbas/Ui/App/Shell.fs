module Marbas.Ui.App.Shell

open System.IO.Abstractions
open Marbas.Collections
open Marbas.Domain
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
let DeleteTitle = "Deleting"

[<Literal>]
let WipeTitle = "Wiping"

[<Literal>]
let MeasureTitle = "Measuring"

[<Literal>]
let PackTitle = "Packing"

[<Literal>]
let UnpackTitle = "Unpacking"

type Dependencies =
    { FileSystem: IFileSystem
      StartPath: FileSystemPath
      SettingsPath: FileSystemPath
      WorkspacePath: FileSystemPath
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
      RunEditor: ProgramPath option -> FileSystemPath -> Result<unit, Operations.OperationError>
      Launch: LaunchRequest -> Result<unit, LaunchError>
      HandlersFor: EntryName -> Handler list
      AskHandler: EntryName -> Handler list -> ProgramPath option -> OpenWithChoice option
      ChooseTab: Zipper<Tab> -> int option
      EditProperties: EntryProperties -> EntryProperties option
      EditPrograms: ProgramSettings -> ProgramPath option -> ProgramSettings option
      AskOverwrite: string -> string -> Operations.OverwriteAnswer
      AskArchive: string -> ArchiveChoice option
      AskPassword: string -> string -> string option
      WatchModifiers: (Set<Modifier> -> unit) -> unit
      WatchLocations: FileSystemPath list -> (unit -> unit) -> unit
      RunBatch:
          string -> (Operations.BatchControl -> Operations.ItemResult list) -> (Operations.ItemResult list -> unit) -> unit
      RunMeasure: string -> (Operations.BatchControl -> Measurement list) -> (Measurement list -> unit) -> unit }

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

let private approvedEntries (deps: Dependencies) targetDirectory archive entries =
    let spelled =
        entries
        |> List.map (fun entry -> ArchivePath.spelled deps.FileSystem archive entry, entry)

    let allowed =
        Operations.approve deps.FileSystem (askOverwrite deps) (List.map fst spelled) targetDirectory
        |> Set.ofList

    spelled |> List.filter (fst >> allowed.Contains) |> List.map snd

let private unpackTargets (deps: Dependencies) targetDirectory (archive: FileSystemPath) =
    match ArchiveListing.read deps.FileSystem archive ArchivePath.root with
    | Error _ -> [ ArchivePath.root ]
    | Ok entries ->
        entries
        |> List.map (_.Name >> ArchivePath.child ArchivePath.root)
        |> approvedEntries deps targetDirectory archive

let private showEntry (deps: Dependencies) archive password entry =
    match FileWindow.openEntry deps.FileSystem password archive entry with
    | Ok source ->
        use source = source
        deps.ShowFile source
    | Error error -> deps.ReportError ArchiveErrorMessage.Title (ArchiveErrorMessage.describe error)

let private approvedInto (deps: Dependencies) archive level sources =
    let taken =
        ArchiveListing.read deps.FileSystem archive level
        |> Result.map (List.map _.Name >> Set.ofList)
        |> Result.defaultValue Set.empty

    let landing source =
        FileSystemPath.name deps.FileSystem source
        |> ArchivePath.child level
        |> ArchivePath.spelled deps.FileSystem archive

    let occupied target =
        taken.Contains(FileSystemPath.name deps.FileSystem target)

    Operations.approveInto landing occupied (askOverwrite deps) sources

let private approvedAcross (deps: Dependencies) source target level entries =
    let taken =
        ArchiveListing.read deps.FileSystem target level
        |> Result.map (List.map _.Name >> Set.ofList)
        |> Result.defaultValue Set.empty

    let spelled =
        entries
        |> List.map (fun entry -> ArchivePath.spelled deps.FileSystem source entry, entry)

    let landing entry =
        FileSystemPath.name deps.FileSystem entry
        |> ArchivePath.child level
        |> ArchivePath.spelled deps.FileSystem target

    let occupied landed =
        taken.Contains(FileSystemPath.name deps.FileSystem landed)

    let allowed =
        Operations.approveInto landing occupied (askOverwrite deps) (List.map fst spelled)
        |> Set.ofList

    spelled |> List.filter (fst >> allowed.Contains) |> List.map snd

let private refusedWhole (deps: Dependencies) archive error : Operations.ItemResult =
    { Name = FileSystemPath.name deps.FileSystem archive
      Outcome = Error(Operations.ArchiveFailed error) }

let private itemsOf (deps: Dependencies) archive result =
    match result with
    | Ok items -> items
    | Error error -> [ refusedWhole deps archive error ]

let private changingArchive (deps: Dependencies) archive work =
    match Archive.changeRefusal deps.FileSystem archive with
    | Some refusal -> deps.ReportError ArchiveErrorMessage.Title (ArchiveErrorMessage.describe refusal)
    | None -> work ()

let private planFor (deps: Dependencies) targetDirectory archive (targets: unit -> ArchivePath list) =
    let planned password =
        match targets () with
        | [] -> None
        | approved ->
            Some
                { Request =
                    { Archive = archive
                      Password = password
                      Destination = targetDirectory }
                  Targets = approved }

    match passwordFor deps archive with
    | NotNeeded -> planned None
    | Given password -> planned (Some password)
    | NotGiven -> None

let private unpackPlan (deps: Dependencies) targetDirectory archive =
    planFor deps targetDirectory archive (fun () -> unpackTargets deps targetDirectory archive)

let private extractPlan (deps: Dependencies) targetDirectory archive entries =
    planFor deps targetDirectory archive (fun () -> approvedEntries deps targetDirectory archive entries)

let private acrossPlan (deps: Dependencies) source target level entries =
    planFor deps deps.WorkspacePath source (fun () -> approvedAcross deps source target level entries)

let private crossing (deps: Dependencies) dispatch title carry finished source entries target level =
    changingArchive deps target (fun () ->
        match acrossPlan deps source target level entries with
        | None -> ()
        | Some plan ->
            deps.RunBatch
                title
                (fun control ->
                    carry deps.FileSystem control plan.Request plan.Targets target level
                    |> itemsOf deps source)
                (finished >> dispatch))

let interpret (deps: Dependencies) (dispatch: Message -> unit) (effect: Effect) =
    match effect with
    | ReportListingError error ->
        deps.ReportError (ListingErrorMessage.title error) (ListingErrorMessage.describe error)
    | ReportOperationError error ->
        deps.ReportError OperationErrorMessage.Title (OperationErrorMessage.describe error)
    | ReportLaunchError error -> deps.ReportError LaunchErrorMessage.Title (LaunchErrorMessage.describe error)
    | ReportItemFailures failures ->
        deps.ReportError OperationErrorMessage.Title (OperationErrorMessage.describeFailures failures)
    | ConfirmDelete request ->
        if deps.Confirm (DeleteConfirmation.title request) (DeleteConfirmation.describe request) then
            dispatch (DeleteConfirmed request.Mode)
    | DeleteTargets(sources, ByWiping) ->
        deps.RunBatch
            WipeTitle
            (fun control -> Operations.wipeAll deps.FileSystem control sources)
            (DeleteFinished >> dispatch)
    | DeleteTargets(sources, ToTrash) ->
        Operations.removeAll deps.FileSystem deps.SendToTrash sources |> DeleteFinished |> dispatch
    | DeleteTargets(sources, Permanently) ->
        Operations.removeAll deps.FileSystem (Operations.delete deps.FileSystem) sources
        |> DeleteFinished
        |> dispatch
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
    | ExtractEntries(archive, entries, targetDirectory) ->
        match extractPlan deps targetDirectory archive entries with
        | None -> ()
        | Some plan ->
            deps.RunBatch
                CopyTitle
                (fun control -> Archive.extractAll deps.FileSystem control [ plan ])
                (CopyFinished >> dispatch)
    | AddEntries(archive, level, sources) ->
        changingArchive deps archive (fun () ->
            match approvedInto deps archive level sources with
            | [] -> ()
            | approved ->
                deps.RunBatch
                    CopyTitle
                    (fun control ->
                        Archive.addEntries deps.FileSystem control archive level approved
                        |> itemsOf deps archive)
                    (CopyFinished >> dispatch))
    | MoveEntriesIn(archive, level, sources) ->
        changingArchive deps archive (fun () ->
            match approvedInto deps archive level sources with
            | [] -> ()
            | approved ->
                deps.RunBatch
                    MoveTitle
                    (fun control ->
                        Archive.moveIn deps.FileSystem control archive level approved
                        |> itemsOf deps archive)
                    (MoveFinished >> dispatch))
    | MoveEntriesOut(archive, entries, targetDirectory) ->
        changingArchive deps archive (fun () ->
            match extractPlan deps targetDirectory archive entries with
            | None -> ()
            | Some plan ->
                deps.RunBatch
                    MoveTitle
                    (fun control ->
                        Archive.moveOut deps.FileSystem control plan.Request plan.Targets
                        |> itemsOf deps archive)
                    (MoveFinished >> dispatch))
    | CopyEntriesAcross(source, entries, target, level) ->
        crossing deps dispatch CopyTitle Archive.copyAcross CopyFinished source entries target level
    | MoveEntriesAcross(source, entries, target, level) ->
        crossing deps dispatch MoveTitle Archive.moveAcross MoveFinished source entries target level
    | RemoveEntries(archive, entries) ->
        changingArchive deps archive (fun () ->
            deps.RunBatch
                DeleteTitle
                (fun control ->
                    Archive.removeEntries deps.FileSystem control archive entries
                    |> itemsOf deps archive)
                (DeleteFinished >> dispatch))
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
    | MeasureTargets targets ->
        deps.RunMeasure
            MeasureTitle
            (fun control -> DirectoryMeasure.measureAll deps.FileSystem control targets)
            (MeasureFinished >> dispatch)
    | ViewFile path ->
        match FileWindow.openFile deps.FileSystem path with
        | Ok source ->
            use source = source
            deps.ShowFile source
        | Error error -> deps.ReportError ContentErrorMessage.Title (ContentErrorMessage.describe error)
    | ViewArchiveEntry(archive, entry) ->
        match passwordFor deps archive with
        | NotGiven -> ()
        | NotNeeded -> showEntry deps archive None entry
        | Given password -> showEntry deps archive (Some password) entry
    | EditFile(path, editor) -> deps.RunEditor editor path |> EditFinished |> dispatch
    | RunProgram request -> deps.Launch request |> LaunchFinished |> dispatch
    | ShowHandlers(path, chosen) ->
        let name = FileSystemPath.name deps.FileSystem path

        match deps.AskHandler name (deps.HandlersFor name) chosen with
        | Some choice -> dispatch (HandlerChosen(path, choice))
        | None -> ()
    | ShowTabs tabs ->
        match deps.ChooseTab tabs with
        | Some position -> dispatch (TabSwitched position)
        | None -> ()
    | ShowProperties properties ->
        match deps.EditProperties properties with
        | Some changed -> dispatch (PropertiesEntered changed)
        | None -> ()
    | ShowSettings(programs, candidate) ->
        match deps.EditPrograms programs candidate with
        | Some changed -> dispatch (ProgramsEntered changed)
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
    | WatchLocations paths -> deps.WatchLocations paths (fun () -> dispatch ExternalChangeDetected)
    | SaveSettings settings -> Settings.save deps.FileSystem deps.SettingsPath settings
    | Quit ->
        Workspace.release deps.FileSystem deps.WorkspacePath
        deps.OnQuit()
