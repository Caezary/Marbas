module Marbas.Adapters.Prompts

open System
open System.IO.Abstractions
open Terminal.Gui.App
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Collections
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Programs
open Marbas.Viewing
open Marbas.Messages
open Marbas.Guide
open Marbas.State.AppState
open Marbas.State.Ports
open Marbas.Ui.Viewer
open Marbas.Ui.Dialogs
open Marbas.Ui.Help

let private run<'view, 'answer when 'view :> IRunnable and 'view :> IDisposable>
    (app: IApplication)
    (create: unit -> 'view)
    (read: 'view -> 'answer)
    =
    use dialog = create ()
    app.Run dialog |> ignore
    read dialog

let reportError (app: IApplication) (report: ErrorReport) =
    MessageBox.ErrorQuery(app, report.Title, report.Message, "Ok") |> ignore

let private reportSearch (app: IApplication) (report: SearchReport) =
    match report with
    | TextNotFound _
    | BytesNotFound _ ->
        MessageBox.Query(app, ViewerSearch.Title, ViewerSearch.describe report, "Ok")
        |> ignore
    | SearchFailed _ ->
        reportError
            app
            { Title = ContentErrorMessage.Title
              Message = ViewerSearch.describe report }
    | Refused _ ->
        reportError
            app
            { Title = ViewerSearch.Title
              Message = ViewerSearch.describe report }

let private askSearchWith runPopup (app: IApplication) (offer: SearchOffer) =
    let createViewerSearch () =
        ViewerSearchDialog.create (Refused >> reportSearch app) offer

    runPopup createViewerSearch ViewerSearchDialog.answer

let private reportFault (app: IApplication) fault =
    reportError
        app
        { Title = HelpReport.Title
          Message = HelpReport.describe fault }

let showHelp (app: IApplication) (place: GuidePlace) =
    let ports: HelpView.Ports =
        { Close = _.RequestStop()
          Read = EmbeddedGuide.read
          Report = reportFault app
          AskSearch = askSearchWith (run app) app
          ReportSearch = reportSearch app }

    match HelpState.opening EmbeddedGuide.read place with
    | Ok opened -> run app (fun () -> HelpView.create ports opened) ignore
    | Error fault -> reportFault app fault

let private running<'view, 'answer when 'view :> View and 'view :> IRunnable and 'view :> IDisposable>
    (app: IApplication)

    popup
    (create: unit -> 'view)
    (read: 'view -> 'answer)
    =
    let opened () =
        create ()
        |> HelpOpening.onHelpKey (fun () -> showHelp app (HelpPlaces.ofPopup popup))

    run app opened read

let confirm (app: IApplication) (question: Confirmation) =
    let answer = MessageBox.Query(app, question.Title, question.Question, "_Yes", "_No")
    answer.HasValue && answer.Value = 0

let askOverwrite (app: IApplication) (request: OverwritePrompt) =
    let answer =
        MessageBox.Query(
            app,
            OverwriteQuestion.Title,
            OverwriteQuestion.describe request.Target,
            Array.ofList OverwriteQuestion.Answers
        )

    OverwriteQuestion.answerAt (if answer.HasValue then answer.Value else -1)

let chooseDrive (app: IApplication) (request: DrivePrompt) =
    let screenWidth () = app.Screen.Width

    let createDrive () =
        DriveDialog.create request.Side screenWidth request.Drives

    match request.Drives with
    | [] -> None
    | _ -> running app HelpPlaces.DrivePopup createDrive DriveDialog.choice

let askFavoriteSlot (app: IApplication) (fs: IFileSystem) (place: Place) =
    let createKeepFavorite () =
        KeepFavoriteDialog.create fs app.Screen.Width place

    running app HelpPlaces.KeepFavoritePopup createKeepFavorite KeepFavoriteDialog.slot

let chooseFavorite (app: IApplication) (fs: IFileSystem) (request: FavoritesPrompt) =
    let createFavorites () =
        FavoritesDialog.create fs request.Change request.Current request.Favorites

    running app HelpPlaces.FavoritesPopup createFavorites FavoritesDialog.chosen

let private texts (remembered: EnteredText list) =
    remembered |> List.map EnteredText.value

let askPattern (app: IApplication) (request: PatternPrompt) =
    let createPattern () =
        PatternDialog.create (texts request.Remembered) request.Title

    running app HelpPlaces.PatternPopup createPattern PromptDialog.text

let askName (app: IApplication) (request: NamePrompt) =
    let offered =
        request.Suggestion |> Option.map EnteredText.value |> Option.defaultValue ""

    let createNamePrompt () =
        PromptDialog.offering (texts request.Remembered) request.Title "Name:" offered
        |> PromptDialog.selecting

    running app HelpPlaces.NamePopup createNamePrompt PromptDialog.text

let askTarget (app: IApplication) (request: TargetPrompt) =
    let offered = FileSystemPath.value request.Suggestion

    let createTargetPrompt () =
        PromptDialog.offering (texts request.Remembered) request.Title "To:" offered

    running app HelpPlaces.TargetPopup createTargetPrompt PromptDialog.text

let askPassword (app: IApplication) (request: PasswordPrompt) =
    let label = PasswordQuestion.describe request.Archive

    let createPasswordPrompt () =
        PromptDialog.create PasswordQuestion.Title label "" |> PromptDialog.secret

    running app HelpPlaces.PasswordPopup createPasswordPrompt PromptDialog.password

let private chooseCodePage (app: IApplication) (offer: CodePageOffer) =
    let createCodePage () = CodePageDialog.create offer

    running app HelpPlaces.CodePagePopup createCodePage CodePageDialog.choice

let showFile (app: IApplication) (request: ViewRequest) =
    let progress = ViewerSearch.progress request.Source.Length

    let ports: FileViewer.Ports =
        { Close = _.RequestStop()
          ChooseCodePage = chooseCodePage app
          AskSearch = askSearchWith (running app HelpPlaces.ViewerSearchPopup) app
          Searches = request.Searches
          ByteSearches = request.ByteSearches
          RunSearch = WindowSearchRunner.run app ViewerSearch.Title progress
          RunByteSearch = WindowSearchRunner.run app ViewerSearch.Title progress
          Report = reportSearch app
          ShowHelp = fun () -> showHelp app HelpPlaces.viewer }

    let createFileViewer () = FileViewer.create ports request.Source

    run app createFileViewer ignore

let editProperties (app: IApplication) platform properties =
    let createProperties () =
        PropertiesDialog.create platform properties

    running app HelpPlaces.PropertiesPopup createProperties (PropertiesDialog.properties platform properties)

let askArchive (app: IApplication) retarget (request: PackPrompt) =
    let offered = FileSystemPath.value request.Suggestion

    let createPack () =
        PackDialog.create retarget (texts request.Remembered) offered

    running app HelpPlaces.PackPopup createPack PackDialog.choice

let private askedOf (dialog: FindDialog.FindView) =
    let masks, text = FindDialog.typed dialog

    FindDialog.choice dialog
    |> Option.map (fun choice ->
        { Choice = choice
          Masks = masks
          Text = text })

let askFind (app: IApplication) (request: FindPrompt) =
    let createFind () =
        FindDialog.create request.Ground request.Hidden request.Found (texts request.Masks) (texts request.Texts)

    running app HelpPlaces.FindPopup createFind askedOf

let askLink (app: IApplication) (request: LinkPrompt) =
    let offered = FileSystemPath.value request.Suggestion

    let createLink () =
        LinkDialog.create request.Kinds request.Refusal (texts request.Remembered) offered

    running app HelpPlaces.LinkPopup createLink LinkDialog.choice

let askHandler (app: IApplication) (request: HandlerPrompt) =
    let createOpenWith () =
        OpenWithDialog.create request.Name request.Handlers request.Chosen (texts request.Remembered)

    running app HelpPlaces.OpenWithPopup createOpenWith OpenWithDialog.choice

let editPrograms (app: IApplication) (request: ProgramsPrompt) =
    let createSettings () =
        SettingsDialog.create request.Programs request.Candidate

    running app HelpPlaces.SettingsPopup createSettings SettingsDialog.programs

let chooseHistory (app: IApplication) (fs: IFileSystem) (request: HistoryPrompt) =
    let asking title question =
        confirm app { Title = title; Question = question }

    let createHistory () =
        HistoryDialog.create fs asking request.Change request.History request.Tab request.Now

    running app HelpPlaces.HistoryPopup createHistory HistoryDialog.answer

let chooseTab (app: IApplication) (fs: IFileSystem) (tabs: Zipper<Tab>) =
    let rows =
        tabs |> Zipper.items |> List.mapi (fun index -> TabRow.describe fs (index + 1))

    let createTab () =
        TabDialog.create rows (Zipper.position tabs)

    running app HelpPlaces.TabsPopup createTab TabDialog.choice

let private askMenuEntry (app: IApplication) (prompt: MenuEntryForm.EntryPrompt) =
    let createMenuEntry () = MenuEntryDialog.create prompt

    running app HelpPlaces.MenuEntryPopup createMenuEntry MenuEntryDialog.answer

let private askMenuRemoval (app: IApplication) (prompt: MenuRemoval.RemovalPrompt) =
    let labels =
        (prompt.Offered |> List.map MenuRemoval.label) @ [ MenuRemoval.CancelLabel ]

    let answer =
        MessageBox.Query(app, MenuRemoval.Title, MenuRemoval.question prompt, Array.ofList labels)

    answer
    |> Option.ofNullable
    |> Option.bind (fun index -> List.tryItem index prompt.Offered)

let chooseFromUserMenu (app: IApplication) (request: UserMenuPrompt) =
    let editing: UserMenuDialog.MenuEditing =
        { AskEntry = askMenuEntry app
          AskRemoval = askMenuRemoval app
          Change = request.Change }

    let createUserMenu () =
        UserMenuDialog.create editing request.Menu

    running app HelpPlaces.UserMenuPopup createUserMenu UserMenuDialog.chosen
