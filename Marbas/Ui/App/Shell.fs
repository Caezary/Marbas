module Marbas.Ui.App.Shell

open System.IO.Abstractions
open Marbas.Collections
open Marbas.Domain
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Ui.Keys
open Marbas.Ui.Messages
open Marbas.Ui.Dialogs

[<Literal>]
let SelectPrompt = "Select"

[<Literal>]
let DeselectPrompt = "Deselect"

[<Literal>]
let MkdirPrompt = "Create directory"

[<Literal>]
let NewFilePrompt = "Create file"

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
      ListDrives: unit -> DriveRow list
      OnQuit: unit -> unit
      ReportError: string -> string -> unit
      ChooseDrive: DriveRow list -> DriveChoice option
      AskPattern: string -> string option
      AskName: string -> string -> string option
      AskTarget: string -> string -> string option
      Confirm: string -> string -> bool
      SendToTrash: FileSystemPath -> Result<unit, Operations.OperationError>
      ShowFile: FileWindow.Source -> unit
      RunEditor: ProgramPath option -> FileSystemPath -> Result<unit, Operations.OperationError>
      Launch: LaunchRequest -> Result<unit, LaunchError>
      AskFind: MarkedGround -> HiddenEntries -> int option -> FindDialog.FindChoice option
      PathDirectories: FileSystemPath list
      RunSearch: SearchRun -> (FindHit -> unit) -> (FindAnswer -> unit) -> unit
      LinkKinds: LinkKind list
      AskLink: LinkKind list -> string -> (string -> LinkKind -> LinkRefusal option) -> LinkChoice option
      RefuseLink: LinkRequest -> LinkRefusal option
      MakeLink: LinkRequest -> Result<unit, Operations.OperationError>
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
      RunMeasure:
          string
              -> (Operations.BatchControl -> Measurement list)
              -> (Result<Measurement list, Operations.OperationError> -> unit)
              -> unit }

let private patternPrompt mode =
    match mode with
    | Select -> SelectPrompt
    | Deselect -> DeselectPrompt

let private askOverwrite (deps: Dependencies) target =
    deps.AskOverwrite OverwriteQuestion.Title (OverwriteQuestion.describe target)

type private PasswordOutcome =
    | NotNeeded
    | NotGiven
    | Given of password: ArchivePassword

let private asking (deps: Dependencies) (archive: FileSystemPath) =
    FileSystemPath.name deps.FileSystem archive
    |> PasswordQuestion.describe
    |> deps.AskPassword PasswordQuestion.Title
    |> Option.bind ArchivePassword.tryCreate
    |> Option.map Given
    |> Option.defaultValue NotGiven

let private passwordFor (deps: Dependencies) (archive: FileSystemPath) =
    match Archive.needsPassword deps.FileSystem archive with
    | Ok true -> asking deps archive
    | Ok false
    | Error _ -> NotNeeded

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

let private showPath (deps: Dependencies) path =
    match FileWindow.openFile deps.FileSystem path with
    | Ok source ->
        use source = source
        deps.ShowFile source
    | Error error -> deps.ReportError ContentErrorMessage.Title (ContentErrorMessage.describe error)

let private showEntry (deps: Dependencies) archive password entry =
    match FileWindow.openEntry deps.FileSystem password archive entry with
    | Ok source ->
        use source = source
        deps.ShowFile source
    | Error error -> deps.ReportError ArchiveErrorMessage.Title (ArchiveErrorMessage.describe error)

let private showHit (deps: Dependencies) (hit: FindHit) =
    match hit.Location with
    | OnDisk _
    | SearchResults _ -> showPath deps (FindHit.spelled deps.FileSystem hit)
    | InArchive(archive, level) ->
        let entry = ArchivePath.child level hit.Entry.Name

        match passwordFor deps archive with
        | NotGiven -> ()
        | NotNeeded -> showEntry deps archive None entry
        | Given password -> showEntry deps archive (Some password) entry

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

let private refusedWhole archive error : Operations.ItemResult =
    { Source = Operations.ItemOnDisk archive
      Outcome = Error(Operations.ArchiveFailed error) }

let private itemsOf archive result =
    match result with
    | Ok items -> items
    | Error error -> [ refusedWhole archive error ]

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

type private Piece = Operations.BatchControl -> Operations.ItemResult list

let private gated (deps: Dependencies) archive (piece: unit -> Piece option) : Piece option =
    match Archive.changeRefusal deps.FileSystem archive with
    | Some refusal -> Some(fun _ -> [ refusedWhole archive refusal ])
    | None -> piece ()

let private approvedOnDisk (deps: Dependencies) paths directory (piece: FileSystemPath list -> Piece) =
    match Operations.approve deps.FileSystem (askOverwrite deps) paths directory with
    | [] -> None
    | approved -> Some(piece approved)

let private approvedInArchive (deps: Dependencies) archive level paths (piece: FileSystemPath list -> Piece) =
    match approvedInto deps archive level paths with
    | [] -> None
    | approved -> Some(piece approved)

let private copyPiece (deps: Dependencies) target group : Piece option =
    let fs = deps.FileSystem

    match group, target with
    | FromDisk paths, ToDirectory directory ->
        approvedOnDisk deps paths directory (fun approved control -> Operations.copyAll fs control approved directory)
    | FromArchive(archive, entries), ToDirectory directory ->
        extractPlan deps directory archive entries
        |> Option.map (fun plan control -> Archive.extractAll fs control [ plan ])
    | FromDisk paths, ToArchive(into, level) ->
        approvedInArchive deps into level paths (fun approved control ->
            Archive.addEntries fs control into level approved |> itemsOf into)
    | FromArchive(archive, entries), ToArchive(into, level) ->
        acrossPlan deps archive into level entries
        |> Option.map (fun plan control ->
            Archive.copyAcross fs control plan.Request plan.Targets into level |> itemsOf archive)
    | FromPlaces _, _ -> None

let private movePiece (deps: Dependencies) target group : Piece option =
    let fs = deps.FileSystem

    match group, target with
    | FromDisk paths, ToDirectory directory ->
        approvedOnDisk deps paths directory (fun approved control -> Operations.moveAll fs control approved directory)
    | FromArchive(archive, entries), ToDirectory directory ->
        gated deps archive (fun () ->
            extractPlan deps directory archive entries
            |> Option.map (fun plan control ->
                Archive.moveOut fs control plan.Request plan.Targets |> itemsOf archive))
    | FromDisk paths, ToArchive(into, level) ->
        approvedInArchive deps into level paths (fun approved control ->
            Archive.moveIn fs control into level approved |> itemsOf into)
    | FromArchive(archive, entries), ToArchive(into, level) ->
        gated deps archive (fun () ->
            acrossPlan deps archive into level entries
            |> Option.map (fun plan control ->
                Archive.moveAcross fs control plan.Request plan.Targets into level |> itemsOf archive))
    | FromPlaces _, _ -> None

let private deletePiece (deps: Dependencies) mode group : Piece option =
    let fs = deps.FileSystem

    match group, mode with
    | FromDisk paths, ToTrash -> Some(fun _ -> Operations.removeAll deps.SendToTrash paths)
    | FromDisk paths, Permanently -> Some(fun _ -> Operations.removeAll (Operations.delete fs) paths)
    | FromDisk paths, ByWiping -> Some(fun control -> Operations.wipeAll fs control paths)
    | FromArchive(archive, entries), _ ->
        gated deps archive (fun () ->
            Some(fun control -> Archive.removeEntries fs control archive entries |> itemsOf archive))
    | FromPlaces _, _ -> None

let private untilStopped (control: Operations.BatchControl) (pieces: Piece list) =
    let rec running remaining =
        match remaining with
        | [] -> []
        | piece :: rest ->
            match piece control with
            | results when control.IsCancelled() -> results
            | results -> results @ running rest

    running pieces

let private runPlaces (deps: Dependencies) title (pieces: Piece option list) finished =
    match List.choose id pieces with
    | [] -> ()
    | ready -> deps.RunBatch title (fun control -> untilStopped control ready) finished

let private deleteTitle mode =
    match mode with
    | ByWiping -> WipeTitle
    | ToTrash
    | Permanently -> DeleteTitle

let interpret (deps: Dependencies) (dispatch: Message -> unit) (effect: Effect) =
    match effect with
    | ReportListingError error ->
        deps.ReportError (ListingErrorMessage.title error) (ListingErrorMessage.describe error)
    | ReportOperationError error ->
        deps.ReportError OperationErrorMessage.Title (OperationErrorMessage.describe error)
    | ReportLaunchError error -> deps.ReportError LaunchErrorMessage.Title (LaunchErrorMessage.describe error)
    | ReportItemFailures failures ->
        deps.ReportError OperationErrorMessage.Title (OperationErrorMessage.describeFailures deps.FileSystem failures)
    | ConfirmDelete request ->
        if deps.Confirm (DeleteConfirmation.title request) (DeleteConfirmation.describe request) then
            dispatch (DeleteConfirmed request.Mode)
    | PromptForArchive suggestion ->
        match deps.AskArchive(FileSystemPath.value suggestion) with
        | Some choice -> dispatch (ArchiveEntered choice)
        | None -> ()
    | PromptForFind(ground, hidden, found) ->
        match deps.AskFind ground hidden found with
        | Some(FindDialog.Search query) -> dispatch (FindEntered query)
        | Some FindDialog.LastResults -> dispatch LastResultsRequested
        | None -> ()
    | RunSearch(query, location, marked) ->
        let ground =
            FindGround.resolve deps.FileSystem location marked deps.PathDirectories query.Scope

        deps.RunSearch (Walk(query, ground)) (showHit deps) (SearchAnswered >> dispatch)
    | ShowLastResults(results, gone) ->
        deps.RunSearch (Reopen(results, gone)) (showHit deps) (SearchAnswered >> dispatch)
    | PromptForLink(sources, directory, suggestion) ->
        let refusalOf text kind =
            Links.requests deps.FileSystem kind sources directory text
            |> Links.sharedRefusal deps.RefuseLink

        match deps.AskLink deps.LinkKinds (FileSystemPath.value suggestion) refusalOf with
        | Some choice -> dispatch (LinkEntered choice)
        | None -> ()
    | MakeLinks requests ->
        let outcomes = requests |> List.map (fun link -> link, deps.MakeLink link)

        let made =
            outcomes
            |> List.choose (fun (link, outcome) -> if Result.isOk outcome then Some link.Location else None)

        let results =
            outcomes
            |> List.map (fun (link, outcome) -> Operations.itemResult link.Target outcome)

        dispatch (LinksMade(made, results))
    | ArchiveTargets(sources, archive) ->
        let target = archive.Path

        let allowed =
            FileSystemPath.kind deps.FileSystem target = NoEntry
            || deps.Confirm OverwriteQuestion.Title (OverwriteQuestion.describe target)

        if allowed then
            deps.RunBatch PackTitle (fun control -> Archive.write deps.FileSystem control archive sources) (fun results ->
                dispatch (PackFinished(FileSystemPath.name deps.FileSystem target, results)))
    | CopyPlaces(groups, target) ->
        runPlaces deps CopyTitle (groups |> List.map (copyPiece deps target)) (CopyFinished >> dispatch)
    | MovePlaces(groups, target) ->
        runPlaces deps MoveTitle (groups |> List.map (movePiece deps target)) (MoveFinished >> dispatch)
    | DeletePlaces(groups, mode) ->
        runPlaces deps (deleteTitle mode) (groups |> List.map (deletePiece deps mode)) (DeleteFinished >> dispatch)
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
    | ViewFile path -> showPath deps path
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
        match deps.ChooseDrive(deps.ListDrives()) with
        | Some(StandOn row) -> dispatch (DriveChosen(side, row))
        | Some(HandOver row) -> dispatch (DriveHandedOver row)
        | None -> ()
    | WatchLocations paths -> deps.WatchLocations paths (fun () -> dispatch ExternalChangeDetected)
    | SaveSettings settings ->
        match Settings.save deps.FileSystem deps.SettingsPath settings with
        | Ok() -> ()
        | Error error -> deps.ReportError SettingsErrorMessage.Title (SettingsErrorMessage.describe error)
    | Quit ->
        Workspace.release deps.FileSystem deps.WorkspacePath
        deps.OnQuit()
