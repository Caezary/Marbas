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
open Marbas.Ui.App.Ports

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
let RenameTitle = "Renaming"

[<Literal>]
let MeasureTitle = "Measuring"

[<Literal>]
let PackTitle = "Packing"

[<Literal>]
let UnpackTitle = "Unpacking"

let private patternTitle mode =
    match mode with
    | Select -> SelectPrompt
    | Deselect -> DeselectPrompt

let private report (deps: Dependencies) title message =
    deps.Prompts.ReportError { Title = title; Message = message }

let private askOverwrite (deps: Dependencies) target =
    deps.Prompts.AskOverwrite { Target = target }

type private PasswordOutcome =
    | OpenWith of password: ArchivePassword option
    | NotGiven

let private asking (deps: Dependencies) (archive: FileSystemPath) =
    { Archive = FileSystemPath.name deps.FileSystem archive }
    |> deps.Prompts.AskPassword
    |> Option.map (Some >> OpenWith)
    |> Option.defaultValue NotGiven

let private passwordOutcome (deps: Dependencies) (archive: FileSystemPath) =
    match Archive.needsPassword deps.FileSystem archive with
    | Ok true -> asking deps archive
    | Ok false
    | Error _ -> OpenWith None

let private withPassword (deps: Dependencies) archive show =
    match passwordOutcome deps archive with
    | OpenWith password -> show password
    | NotGiven -> ()

let private entriesApprovedBySpelling (deps: Dependencies) archive approvePaths entries =
    let spelled =
        entries
        |> List.map (fun entry -> ArchivePath.spelled deps.FileSystem archive entry, entry)

    let allowed = spelled |> List.map fst |> approvePaths |> Set.ofList

    spelled |> List.filter (fst >> allowed.Contains) |> List.map snd

let private entriesApprovedForDirectory (deps: Dependencies) targetDirectory archive entries =
    let approvePaths paths =
        Operations.approve deps.FileSystem (askOverwrite deps) paths targetDirectory

    entriesApprovedBySpelling deps archive approvePaths entries

let private unpackTargets (deps: Dependencies) targetDirectory (archive: FileSystemPath) =
    match ArchiveListing.read deps.FileSystem archive ArchivePath.root with
    | Error _ -> [ ArchivePath.root ]
    | Ok entries ->
        entries
        |> List.map (_.Name >> ArchivePath.child ArchivePath.root)
        |> entriesApprovedForDirectory deps targetDirectory archive

let private showOpened (deps: Dependencies) title describe opened =
    match opened with
    | Ok source ->
        use source = source
        deps.Prompts.ShowFile source
    | Error error -> report deps title (describe error)

let private showPath (deps: Dependencies) path =
    FileWindow.openFile deps.FileSystem path
    |> showOpened deps ContentErrorMessage.Title ContentErrorMessage.describe

let private showEntry (deps: Dependencies) archive password entry =
    FileWindow.openEntry deps.FileSystem password archive entry
    |> showOpened deps ArchiveErrorMessage.Title ArchiveErrorMessage.describe

let private showHit (deps: Dependencies) (hit: FindHit) =
    match hit.Location with
    | OnDisk _
    | SearchResults _ -> showPath deps (FindHit.spelled deps.FileSystem hit)
    | InArchive(archive, level) ->
        let entry = ArchivePath.child level hit.Entry.Name

        withPassword deps archive (fun password -> showEntry deps archive password entry)

let private pathsApprovedForArchive (deps: Dependencies) archive level sources =
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

    Operations.approveLandings landing occupied (askOverwrite deps) sources

let private entriesApprovedForArchive (deps: Dependencies) source target level entries =
    entriesApprovedBySpelling deps source (pathsApprovedForArchive deps target level) entries

let private refusedWhole archive error : Operations.ItemResult =
    { Source = Operations.ItemOnDisk archive
      Outcome = Error(Operations.ArchiveFailed error) }

let private itemsOf archive result =
    match result with
    | Ok items -> items
    | Error error -> [ refusedWhole archive error ]

let private nonEmpty items =
    if List.isEmpty items then None else Some items

let private planFor (deps: Dependencies) targetDirectory archive (targets: unit -> ArchivePath list) =
    let planned password approved =
        { Request =
            { Archive = archive
              Password = password
              Destination = targetDirectory }
          Targets = approved }

    match passwordOutcome deps archive with
    | NotGiven -> None
    | OpenWith password -> targets () |> nonEmpty |> Option.map (planned password)

let private unpackPlan (deps: Dependencies) targetDirectory archive =
    planFor deps targetDirectory archive (fun () -> unpackTargets deps targetDirectory archive)

let private extractPlan (deps: Dependencies) targetDirectory archive entries =
    planFor deps targetDirectory archive (fun () -> entriesApprovedForDirectory deps targetDirectory archive entries)

let private archiveToArchivePlan (deps: Dependencies) source target level entries =
    planFor deps deps.WorkspacePath source (fun () -> entriesApprovedForArchive deps source target level entries)

type private Piece = Operations.BatchControl -> Operations.ItemResult list

let private gated (deps: Dependencies) archive (piece: unit -> Piece option) : Piece option =
    match Archive.changeRefusal deps.FileSystem archive with
    | Some refusal -> Some(fun _ -> [ refusedWhole archive refusal ])
    | None -> piece ()

let private pieceApprovedForDirectory (deps: Dependencies) paths directory (piece: FileSystemPath list -> Piece) =
    match Operations.approve deps.FileSystem (askOverwrite deps) paths directory with
    | [] -> None
    | approved -> Some(piece approved)

let private pieceApprovedForArchive (deps: Dependencies) archive level paths (piece: FileSystemPath list -> Piece) =
    match pathsApprovedForArchive deps archive level paths with
    | [] -> None
    | approved -> Some(piece approved)

let private copyPiece (deps: Dependencies) target group : Piece option =
    let fs = deps.FileSystem

    match group, target with
    | FromDisk paths, ToDirectory directory ->
        pieceApprovedForDirectory deps paths directory (fun approved control ->
            Operations.copyAll fs control approved directory)
    | FromArchive(archive, entries), ToDirectory directory ->
        extractPlan deps directory archive entries
        |> Option.map (fun plan control -> Archive.extractAll fs control [ plan ])
    | FromDisk paths, ToArchive(targetArchive, level) ->
        pieceApprovedForArchive deps targetArchive level paths (fun approved control ->
            Archive.addEntries fs control targetArchive level approved |> itemsOf targetArchive)
    | FromArchive(archive, entries), ToArchive(targetArchive, level) ->
        archiveToArchivePlan deps archive targetArchive level entries
        |> Option.map (fun plan control ->
            Archive.copyToArchive fs control plan.Request plan.Targets targetArchive level |> itemsOf archive)
    | FromPlaces _, _ -> None

let private movePiece (deps: Dependencies) target group : Piece option =
    let fs = deps.FileSystem

    match group, target with
    | FromDisk paths, ToDirectory directory ->
        pieceApprovedForDirectory deps paths directory (fun approved control ->
            Operations.moveAll fs control approved directory)
    | FromArchive(archive, entries), ToDirectory directory ->
        gated deps archive (fun () ->
            extractPlan deps directory archive entries
            |> Option.map (fun plan control ->
                Archive.moveToDirectory fs control plan.Request plan.Targets |> itemsOf archive))
    | FromDisk paths, ToArchive(targetArchive, level) ->
        pieceApprovedForArchive deps targetArchive level paths (fun approved control ->
            Archive.moveFromDisk fs control targetArchive level approved |> itemsOf targetArchive)
    | FromArchive(archive, entries), ToArchive(targetArchive, level) ->
        gated deps archive (fun () ->
            archiveToArchivePlan deps archive targetArchive level entries
            |> Option.map (fun plan control ->
                Archive.moveToArchive fs control plan.Request plan.Targets targetArchive level |> itemsOf archive))
    | FromPlaces _, _ -> None

let private deletePiece (deps: Dependencies) mode group : Piece option =
    let fs = deps.FileSystem

    match group, mode with
    | FromDisk paths, ToTrash -> Some(fun control -> Operations.trashAll deps.Processes.SendToTrash control paths)
    | FromDisk paths, Permanently -> Some(fun control -> Operations.deleteAll fs control paths)
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
    | ready -> deps.Runners.Batch title (fun control -> untilStopped control ready) finished

let private deleteTitle mode =
    match mode with
    | ByWiping -> WipeTitle
    | ToTrash
    | Permanently -> DeleteTitle

let interpret (deps: Dependencies) (dispatch: Message -> unit) (effect: Effect) =
    match effect with
    | ReportListingError error ->
        report deps (ListingErrorMessage.title error) (ListingErrorMessage.describe error)
    | ReportUnverifiedHits refused ->
        report deps ArchiveErrorMessage.Title (ArchiveErrorMessage.describeUnverified refused)
    | ReportOperationError error ->
        report deps OperationErrorMessage.Title (OperationErrorMessage.describe error)
    | ReportLaunchError error -> report deps LaunchErrorMessage.Title (LaunchErrorMessage.describe error)
    | ReportItemFailures failures ->
        report deps OperationErrorMessage.Title (OperationErrorMessage.describeFailures deps.FileSystem failures)
    | InspectDelete(question, mode) -> dispatch (DeleteScopeLoaded(deleteScope deps.FileSystem question, mode))
    | ConfirmDelete request ->
        if
            deps.Prompts.Confirm
                { Title = DeleteConfirmation.title request
                  Question = DeleteConfirmation.describe request }
        then
            dispatch (DeleteConfirmed request.Mode)
    | PromptForArchive suggestion ->
        deps.Prompts.AskArchive { Suggestion = suggestion }
        |> Option.iter (ArchiveEntered >> dispatch)
    | PromptForFind(ground, hidden, found) ->
        match deps.Prompts.AskFind { Ground = ground; Hidden = hidden; Found = found } with
        | Some(Search query) -> dispatch (FindEntered query)
        | Some LastResults -> dispatch LastResultsRequested
        | None -> ()
    | RunSearch(query, location, marked) ->
        let ground =
            FindGround.resolve deps.FileSystem location marked deps.Processes.PathDirectories query.Scope

        deps.Runners.Search (Walk(query, ground)) (showHit deps) (SearchAnswered >> dispatch)
    | ShowLastResults(results, gone) ->
        deps.Runners.Search (Reopen(results, gone)) (showHit deps) (SearchAnswered >> dispatch)
    | PromptForLink(sources, directory, suggestion) ->
        let refusalOf text kind =
            Links.requests deps.FileSystem kind sources directory text
            |> Links.sharedRefusal deps.Processes.RefuseLink

        deps.Prompts.AskLink
            { Kinds = deps.Processes.LinkKinds
              Suggestion = suggestion
              Refusal = refusalOf }
        |> Option.iter (LinkEntered >> dispatch)
    | MakeLinks requests ->
        let outcomes = requests |> List.map (fun link -> link, deps.Processes.MakeLink link)

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
            || deps.Prompts.Confirm
                { Title = OverwriteQuestion.Title
                  Question = OverwriteQuestion.describe target }

        if allowed then
            deps.Runners.Batch
                PackTitle
                (fun control -> Archive.write deps.FileSystem control archive sources)
                (fun results -> dispatch (PackFinished(FileSystemPath.name deps.FileSystem target, results)))
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
            deps.Runners.Batch
                UnpackTitle
                (fun control -> Archive.extractAll deps.FileSystem control plans)
                (UnpackFinished >> dispatch)
    | RenameTarget(source, newName) ->
        let finished outcome = dispatch (RenameFinished(newName, outcome))

        match Operations.startRename deps.FileSystem source newName with
        | Error error -> finished (Error error)
        | Ok Operations.RenameDone -> finished (Ok())
        | Ok(Operations.CopyNeeded target) ->
            deps.Runners.Batch
                RenameTitle
                (fun control ->
                    [ Operations.moveByCopying deps.FileSystem control source target
                      |> Operations.itemResult source ])
                (Operations.wholeOutcome >> finished)
    | MeasureTargets targets ->
        deps.Runners.Measure
            MeasureTitle
            (fun control -> DirectoryMeasure.measureAll deps.FileSystem control targets)
            (MeasureFinished >> dispatch)
    | ViewFile path -> showPath deps path
    | ViewArchiveEntry(archive, entry) ->
        withPassword deps archive (fun password -> showEntry deps archive password entry)
    | EditFile(path, editor) -> deps.Processes.RunEditor editor path |> EditFinished |> dispatch
    | RunProgram request -> deps.Processes.Launch request |> LaunchFinished |> dispatch
    | ShowHandlers(path, chosen) ->
        let name = FileSystemPath.name deps.FileSystem path

        deps.Prompts.AskHandler
            { Name = name
              Handlers = deps.Processes.HandlersFor name
              Chosen = chosen }
        |> Option.iter (fun choice -> dispatch (HandlerChosen(path, choice)))
    | ShowTabs tabs -> deps.Prompts.ChooseTab tabs |> Option.iter (TabSwitched >> dispatch)
    | ShowProperties properties ->
        deps.Prompts.EditProperties properties |> Option.iter (PropertiesEntered >> dispatch)
    | ShowSettings(programs, candidate) ->
        deps.Prompts.EditPrograms { Programs = programs; Candidate = candidate }
        |> Option.iter (ProgramsEntered >> dispatch)
    | PromptForTarget suggestion ->
        deps.Prompts.AskTarget { Title = MovePrompt; Suggestion = suggestion }
        |> Option.iter (TargetEntered >> dispatch)
    | PromptForName suggestion ->
        deps.Prompts.AskName { Title = MkdirPrompt; Suggestion = suggestion }
        |> Option.iter (NameEntered >> dispatch)
    | PromptForFileName ->
        deps.Prompts.AskName { Title = NewFilePrompt; Suggestion = None }
        |> Option.iter (FileNameEntered >> dispatch)
    | PromptForPattern mode ->
        deps.Prompts.AskPattern { Title = patternTitle mode }
        |> Option.iter (fun pattern -> dispatch (PatternEntered(mode, pattern)))
    | PromptForRoot side ->
        match deps.Prompts.ChooseDrive(deps.Processes.ListDrives()) with
        | Some(StandOn row) -> dispatch (DriveChosen(side, row))
        | Some(HandOver row) -> dispatch (DriveHandedOver row)
        | None -> ()
    | WatchLocations paths -> deps.Watches.Locations paths (fun () -> dispatch ExternalChangeDetected)
    | ChangeHistory change ->
        match History.change deps.FileSystem deps.HistoryPath (deps.Clock()) change with
        | Ok history -> dispatch (HistoryChanged history)
        | Error error -> dispatch (HistoryUnwritten error)
    | ReportHistoryError error -> report deps HistoryErrorMessage.Title (HistoryErrorMessage.describe error)
    | SaveSettings settings ->
        match Settings.save deps.FileSystem deps.SettingsPath settings with
        | Ok() -> ()
        | Error error -> report deps SettingsErrorMessage.Title (SettingsErrorMessage.describe error)
    | Quit ->
        Workspace.release deps.FileSystem deps.WorkspacePath
        deps.OnQuit()
