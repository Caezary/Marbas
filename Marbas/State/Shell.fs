module Marbas.State.Shell

open Marbas.Domain
open Marbas.FileSystem
open Marbas.State
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.State.Messages
open Marbas.State.Ports

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
    deps.Log(ErrorReported(title, message))
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

let private changeHistory (deps: Dependencies) dispatch change =
    match History.change deps.FileSystem deps.HistoryPath (deps.Clock()) change with
    | Ok history ->
        dispatch (HistoryChanged history)
        Some history
    | Error error ->
        dispatch (HistoryUnwritten error)
        None

let private typedBefore (deps: Dependencies) kind =
    History.load deps.FileSystem deps.HistoryPath
    |> Result.map (History.inputs kind >> List.map _.Item)
    |> Result.defaultValue []

let private viewerSearches (deps: Dependencies) dispatch kind : SearchHistory =
    { Texts = fun () -> typedBefore deps kind
      Remember = fun text -> changeHistory deps dispatch (Remember(Typed(kind, text))) |> ignore }

let private showOpened (deps: Dependencies) dispatch title describe opened =
    match opened with
    | Ok source ->
        use source = source

        deps.Prompts.ShowFile
            { Source = source
              Searches = viewerSearches deps dispatch SearchedText
              ByteSearches = viewerSearches deps dispatch SearchedBytes }
    | Error error -> report deps title (describe error)

let private showPath (deps: Dependencies) dispatch path =
    FileWindow.openFile deps.FileSystem path
    |> showOpened deps dispatch ContentErrorMessage.Title ContentErrorMessage.describe

let private showEntry (deps: Dependencies) dispatch archive password entry =
    FileWindow.openEntry deps.FileSystem password archive entry
    |> showOpened deps dispatch ArchiveErrorMessage.Title ArchiveErrorMessage.describe

let private showHit (deps: Dependencies) dispatch (hit: FindHit) =
    match hit.Location with
    | OnDisk _
    | SearchResults _ -> showPath deps dispatch (FindHit.spelled deps.FileSystem hit)
    | InArchive(archive, level) ->
        let entry = ArchivePath.child level hit.Entry.Name

        withPassword deps archive (fun password -> showEntry deps dispatch archive password entry)

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

let private clipboardPlan (deps: Dependencies) scratch group =
    match group with
    | FromArchive(archive, entries) -> planFor deps scratch archive (fun () -> entries)
    | FromDisk _
    | FromPlaces _ -> None

let private extractForClipboard (deps: Dependencies) dispatch groups =
    match Workspace.scratch deps.FileSystem deps.WorkspacePath with
    | Error error -> report deps OperationErrorMessage.Title (OperationErrorMessage.describe error)
    | Ok scratch ->
        let finished results =
            dispatch (ClipboardExtractFinished(groups, scratch, results))

        match groups |> List.choose (clipboardPlan deps scratch) with
        | [] -> finished []
        | plans ->
            deps.Runners.Batch UnpackTitle (fun control -> Archive.extractAll deps.FileSystem control plans) finished

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
            Archive.addEntries fs control targetArchive level approved
            |> itemsOf targetArchive)
    | FromArchive(archive, entries), ToArchive(targetArchive, level) ->
        archiveToArchivePlan deps archive targetArchive level entries
        |> Option.map (fun plan control ->
            Archive.copyToArchive fs control plan.Request plan.Targets targetArchive level
            |> itemsOf archive)
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
            Archive.moveFromDisk fs control targetArchive level approved
            |> itemsOf targetArchive)
    | FromArchive(archive, entries), ToArchive(targetArchive, level) ->
        gated deps archive (fun () ->
            archiveToArchivePlan deps archive targetArchive level entries
            |> Option.map (fun plan control ->
                Archive.moveToArchive fs control plan.Request plan.Targets targetArchive level
                |> itemsOf archive))
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

let private emptyClipboard (deps: Dependencies) dispatch =
    match deps.Clipboard.Empty() with
    | Ok() -> ()
    | Error error -> dispatch (ClipboardRefused error)

let private forgetMoved (deps: Dependencies) dispatch files (results: Operations.ItemResult list) =
    let moved = results |> List.exists (_.Outcome >> Result.isOk)
    let stillHeld = deps.Clipboard.GetFiles() = Ok(Some files)

    if moved && stillHeld then
        emptyClipboard deps dispatch

let private movePasted (deps: Dependencies) dispatch files target =
    let finished results =
        dispatch (MoveFinished results)
        forgetMoved deps dispatch files results

    runPlaces deps MoveTitle [ movePiece deps target (FromDisk(ClipboardFiles.paths files)) ] finished

let private deleteTitle mode =
    match mode with
    | ByWiping -> WipeTitle
    | ToTrash
    | Permanently -> DeleteTitle

let private reportFavorites (deps: Dependencies) error =
    report deps FavoritesErrorMessage.Title (FavoritesErrorMessage.describe error)

let private changeFavorites (deps: Dependencies) change =
    match Favorites.change deps.FileSystem deps.FavoritesPath change with
    | Ok favorites -> Some favorites
    | Error error ->
        reportFavorites deps error
        None

let private reportUserMenu (deps: Dependencies) error =
    report deps UserMenuErrorMessage.Title (UserMenuErrorMessage.describe error)

let private changeUserMenu (deps: Dependencies) change =
    match UserMenu.change deps.FileSystem deps.UserMenuPath change with
    | Ok menu -> Some menu
    | Error error ->
        reportUserMenu deps error
        None

let private showUserMenu (deps: Dependencies) dispatch =
    match UserMenu.load deps.FileSystem deps.UserMenuPath with
    | Error error -> reportUserMenu deps error
    | Ok menu ->
        deps.Prompts.ChooseFromUserMenu
            { Menu = menu
              Change = changeUserMenu deps }
        |> Option.iter (fun command ->
            MenuCommandChosen(command, Workspace.listFile deps.FileSystem deps.WorkspacePath)
            |> dispatch)

let private rememberTyped (deps: Dependencies) dispatch kind text =
    EnteredText.tryCreate text
    |> Option.iter (fun typed -> changeHistory deps dispatch (Remember(Typed(kind, typed))) |> ignore)

let private claimWorkspace (deps: Dependencies) =
    match Workspace.claim deps.FileSystem deps.Processes.Owner deps.WorkspacePath with
    | Ok() -> ()
    | Error error ->
        WorkspaceUnclaimed(deps.WorkspacePath, OperationErrorMessage.describe error)
        |> deps.Log

let private logKept (deps: Dependencies) (refusal: Workspace.SweepRefusal) =
    LeftoverKept(refusal.Directory, OperationErrorMessage.describe refusal.Reason)
    |> deps.Log

let private sweepLeftovers (deps: Dependencies) () =
    claimWorkspace deps

    Workspace.sweep deps.FileSystem deps.Processes.IsRunning (deps.Clock()) deps.WorkspacePath
    |> List.iter (logKept deps)

let interpret (deps: Dependencies) (dispatch: Message -> unit) (effect: Effect) =
    deps.Log(EffectInterpreted effect)

    match effect with
    | ReportListingError error -> report deps (ListingErrorMessage.title error) (ListingErrorMessage.describe error)
    | ReportUnverifiedHits refused ->
        report deps ArchiveErrorMessage.Title (ArchiveErrorMessage.describeUnverified refused)
    | ReportOperationError error -> report deps OperationErrorMessage.Title (OperationErrorMessage.describe error)
    | ReportLaunchError error -> report deps LaunchErrorMessage.Title (LaunchErrorMessage.describe error)
    | ReportCommandError error -> report deps LaunchErrorMessage.CommandTitle (LaunchErrorMessage.describe error)
    | ReportExpansionRefusal refusal ->
        report deps ExpansionRefusalMessage.Title (ExpansionRefusalMessage.describe refusal)
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
        deps.Prompts.AskArchive
            { Suggestion = suggestion
              Remembered = typedBefore deps ArchiveTarget }
        |> Option.iter (fun choice ->
            rememberTyped deps dispatch ArchiveTarget choice.Target
            dispatch (ArchiveEntered choice))
    | PromptForFind(ground, hidden, found) ->
        let asked =
            deps.Prompts.AskFind
                { Ground = ground
                  Hidden = hidden
                  Found = found
                  Masks = typedBefore deps FindMasks
                  Texts = typedBefore deps SearchedText }

        match asked with
        | Some({ Choice = Search query } as typed) ->
            rememberTyped deps dispatch FindMasks typed.Masks
            rememberTyped deps dispatch SearchedText typed.Text
            dispatch (FindEntered query)
        | Some { Choice = LastResults } -> dispatch LastResultsRequested
        | None -> ()
    | RunSearch(query, location, marked) ->
        let ground =
            FindGround.resolve deps.FileSystem location marked deps.Processes.PathDirectories query.Scope

        deps.Runners.Search (Walk(query, ground)) (showHit deps dispatch) (SearchAnswered >> dispatch)
    | ShowLastResults(results, gone) ->
        deps.Runners.Search (Reopen(results, gone)) (showHit deps dispatch) (SearchAnswered >> dispatch)
    | PromptForLink(sources, directory, suggestion) ->
        let refusalOf text kind =
            Links.requests deps.FileSystem kind sources directory text
            |> Links.sharedRefusal deps.Processes.RefuseLink

        deps.Prompts.AskLink
            { Kinds = deps.Processes.LinkKinds
              Suggestion = suggestion
              Refusal = refusalOf
              Remembered = typedBefore deps LinkDestination }
        |> Option.iter (fun choice ->
            rememberTyped deps dispatch LinkDestination choice.Target
            dispatch (LinkEntered choice))
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
        let finished outcome =
            dispatch (RenameFinished(newName, outcome))

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
    | ViewFile path -> showPath deps dispatch path
    | ViewArchiveEntry(archive, entry) ->
        withPassword deps archive (fun password -> showEntry deps dispatch archive password entry)
    | EditFile(path, editor) -> deps.Processes.StartEditor editor path |> EditorStarted |> dispatch
    | RunProgram request -> dispatch (LaunchFinished(request, deps.Processes.Launch request))
    | RunCommand run -> dispatch (CommandFinished(run, deps.Processes.RunCommand run))
    | ShowUserMenu -> showUserMenu deps dispatch
    | ShowHandlers(path, chosen) ->
        let name = FileSystemPath.name deps.FileSystem path

        deps.Prompts.AskHandler
            { Name = name
              Handlers = deps.Processes.HandlersFor name
              Chosen = chosen
              Remembered = typedBefore deps OpenWithProgram }
        |> Option.iter (fun choice ->
            rememberTyped deps dispatch OpenWithProgram (ProgramPath.value choice.Program)
            dispatch (HandlerChosen(path, choice)))
    | ShowTabs tabs -> deps.Prompts.ChooseTab tabs |> Option.iter (TabSwitched >> dispatch)
    | ShowProperties properties ->
        deps.Prompts.EditProperties properties
        |> Option.iter (PropertiesEntered >> dispatch)
    | ShowSettings(programs, candidate) ->
        deps.Prompts.EditPrograms
            { Programs = programs
              Candidate = candidate }
        |> Option.iter (ProgramsEntered >> dispatch)
    | PromptForTarget suggestion ->
        deps.Prompts.AskTarget
            { Title = MovePrompt
              Suggestion = suggestion
              Remembered = typedBefore deps CopyOrMoveTarget }
        |> Option.iter (fun text ->
            rememberTyped deps dispatch CopyOrMoveTarget text
            dispatch (TargetEntered text))
    | PromptForName ->
        let remembered = typedBefore deps NewDirectory

        deps.Prompts.AskName
            { Title = MkdirPrompt
              Suggestion = List.tryHead remembered
              Remembered = remembered }
        |> Option.bind EntryName.tryCreate
        |> Option.iter (NameEntered >> dispatch)
    | PromptForFileName ->
        deps.Prompts.AskName
            { Title = NewFilePrompt
              Suggestion = None
              Remembered = typedBefore deps NewFile }
        |> Option.iter (fun text ->
            rememberTyped deps dispatch NewFile text
            EntryName.tryCreate text |> Option.iter (FileNameEntered >> dispatch))
    | PromptForPattern mode ->
        deps.Prompts.AskPattern
            { Title = patternTitle mode
              Remembered = typedBefore deps SelectionPattern }
        |> Option.iter (fun text ->
            rememberTyped deps dispatch SelectionPattern text

            NamePattern.tryCreate text
            |> Option.iter (fun pattern -> dispatch (PatternEntered(mode, pattern))))
    | PromptForRoot side ->
        match
            deps.Prompts.ChooseDrive
                { Side = side
                  Drives = deps.Processes.ListDrives() }
        with
        | Some(StandOn row) -> dispatch (DriveChosen(side, row))
        | Some(HandOver row) -> dispatch (DriveHandedOver row)
        | None -> ()
    | WatchLocations paths -> deps.Watches.Locations paths (fun () -> dispatch ExternalChangeDetected)
    | ChangeHistory change -> changeHistory deps dispatch change |> ignore
    | ReportHistoryError error -> report deps HistoryErrorMessage.Title (HistoryErrorMessage.describe error)
    | ShowHistory tab ->
        match History.load deps.FileSystem deps.HistoryPath with
        | Ok history ->
            dispatch (HistoryChanged history)

            deps.Prompts.ChooseHistory
                { History = history
                  Tab = tab
                  Now = deps.Clock()
                  Change = changeHistory deps dispatch }
            |> HistoryAnswered
            |> dispatch
        | Error error -> report deps HistoryErrorMessage.Title (HistoryErrorMessage.describe error)
    | PromptForFavoriteSlot place ->
        deps.Prompts.AskFavoriteSlot place
        |> Option.iter (fun slot -> changeFavorites deps (Keep(slot, place)) |> ignore)
    | ShowFavorites current ->
        match Favorites.load deps.FileSystem deps.FavoritesPath with
        | Ok favorites ->
            deps.Prompts.ChooseFavorite
                { Favorites = favorites
                  Current = current
                  Change = changeFavorites deps }
            |> Option.iter (FavoriteFound >> dispatch)
        | Error error -> reportFavorites deps error
    | LookUpFavorite slot ->
        match Favorites.load deps.FileSystem deps.FavoritesPath with
        | Ok favorites -> Favorites.placeIn slot favorites |> Option.iter (FavoriteFound >> dispatch)
        | Error error -> reportFavorites deps error
    | ReadClipboardPath directory ->
        match deps.Clipboard.GetText() with
        | Error error -> dispatch (ClipboardRefused error)
        | Ok text ->
            text
            |> Option.bind (PastedPath.read deps.FileSystem deps.Processes.EnvironmentVariable directory)
            |> Option.iter (ClipboardPathRead >> dispatch)
    | PutOnClipboard text ->
        match deps.Clipboard.SetText text with
        | Ok() -> ()
        | Error error -> dispatch (ClipboardRefused error)
    | PutFilesOnClipboard files ->
        match deps.Clipboard.SetFiles files with
        | Ok() -> ()
        | Error error -> dispatch (ClipboardRefused error)
    | ExtractForClipboard groups -> extractForClipboard deps dispatch groups
    | ReadClipboardFiles target ->
        match deps.Clipboard.GetFiles() with
        | Error error -> dispatch (ClipboardRefused error)
        | Ok files -> files |> Option.iter (fun held -> dispatch (ClipboardFilesRead(held, target)))
    | MovePastedFiles(files, target) -> movePasted deps dispatch files target
    | ReportClipboardError error -> report deps ClipboardErrorMessage.Title (ClipboardErrorMessage.describe error)
    | SweepLeftovers -> deps.Runners.Background(sweepLeftovers deps)
    | SaveSettings settings ->
        match Settings.save deps.FileSystem deps.SettingsPath settings with
        | Ok() -> ()
        | Error error -> report deps SettingsErrorMessage.Title (SettingsErrorMessage.describe error)
    | Quit ->
        Workspace.release deps.FileSystem deps.WorkspacePath
        deps.OnQuit()
