module Marbas.Adapters.Prompts

open System
open System.IO.Abstractions
open Terminal.Gui.App
open Terminal.Gui.Views
open Marbas.Collections
open Marbas.Domain
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.Ui.Messages
open Marbas.Ui.Viewer
open Marbas.Ui.Dialogs
open Marbas.Ui.App.Ports

let private running<'view, 'answer when 'view :> IRunnable and 'view :> IDisposable>
    (app: IApplication)
    (create: unit -> 'view)
    (read: 'view -> 'answer)
    =
    use dialog = create ()
    app.Run dialog |> ignore
    read dialog

let reportError (app: IApplication) (report: ErrorReport) =
    MessageBox.ErrorQuery(app, report.Title, report.Message, "Ok") |> ignore

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
    let createDriveDialog = DriveDialog.create request.Side screenWidth

    match request.Drives with
    | [] -> None
    | rows -> running app (fun () -> createDriveDialog rows) DriveDialog.choice

let askFavoriteSlot (app: IApplication) (fs: IFileSystem) (place: Place) =
    let createAskFavourite () =
        KeepFavoriteDialog.create fs app.Screen.Width place

    running app createAskFavourite KeepFavoriteDialog.slot

let chooseFavorite (app: IApplication) (fs: IFileSystem) (request: FavoritesPrompt) =
    let createFavourites () =
        FavoritesDialog.create fs request.Change request.Current request.Favorites

    running app createFavourites FavoritesDialog.chosen

let private texts (remembered: EnteredText list) =
    remembered |> List.map EnteredText.value

let askPattern (app: IApplication) (request: PatternPrompt) =
    running app (fun () -> PatternDialog.create (texts request.Remembered) request.Title) PromptDialog.text

let askName (app: IApplication) (request: NamePrompt) =
    let offered =
        request.Suggestion |> Option.map EnteredText.value |> Option.defaultValue ""

    let asking () =
        PromptDialog.offering (texts request.Remembered) request.Title "Name:" offered
        |> PromptDialog.selecting

    running app asking PromptDialog.text

let askTarget (app: IApplication) (request: TargetPrompt) =
    let offered = FileSystemPath.value request.Suggestion

    running
        app
        (fun () -> PromptDialog.offering (texts request.Remembered) request.Title "To:" offered)
        PromptDialog.text

let askPassword (app: IApplication) (request: PasswordPrompt) =
    let label = PasswordQuestion.describe request.Archive

    running
        app
        (fun () -> PromptDialog.create PasswordQuestion.Title label "" |> PromptDialog.secret)
        PromptDialog.password

let private chooseCodePage (app: IApplication) (offer: CodePageOffer) =
    running app (fun () -> CodePageDialog.create offer) CodePageDialog.choice

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

let private askSearch (app: IApplication) (offer: SearchOffer) =
    let createSearch () =
        ViewerSearchDialog.create (Refused >> reportSearch app) offer

    running app createSearch ViewerSearchDialog.answer

let showFile (app: IApplication) (request: ViewRequest) =
    let progress = ViewerSearch.progress request.Source.Length

    let ports: FileViewer.Ports =
        { Close = _.RequestStop()
          ChooseCodePage = chooseCodePage app
          AskSearch = askSearch app
          Searches = request.Searches
          ByteSearches = request.ByteSearches
          RunSearch = WindowSearchRunner.run app ViewerSearch.Title progress
          RunByteSearch = WindowSearchRunner.run app ViewerSearch.Title progress
          Report = reportSearch app }

    let createFileViewer () = FileViewer.create ports request.Source

    running app createFileViewer ignore

let editProperties (app: IApplication) properties =
    running app (fun () -> PropertiesDialog.create properties) (PropertiesDialog.properties properties)

let askArchive (app: IApplication) retarget (request: PackPrompt) =
    let offered = FileSystemPath.value request.Suggestion

    running app (fun () -> PackDialog.create retarget (texts request.Remembered) offered) PackDialog.choice

let private askedOf (dialog: FindDialog.FindView) =
    let masks, text = FindDialog.typed dialog

    FindDialog.choice dialog
    |> Option.map (fun choice ->
        { Choice = choice
          Masks = masks
          Text = text })

let askFind (app: IApplication) (request: FindPrompt) =
    let asking () =
        FindDialog.create request.Ground request.Hidden request.Found (texts request.Masks) (texts request.Texts)

    running app asking askedOf

let askLink (app: IApplication) (request: LinkPrompt) =
    let offered = FileSystemPath.value request.Suggestion

    running
        app
        (fun () -> LinkDialog.create request.Kinds request.Refusal (texts request.Remembered) offered)
        LinkDialog.choice

let askHandler (app: IApplication) (request: HandlerPrompt) =
    running
        app
        (fun () -> OpenWithDialog.create request.Name request.Handlers request.Chosen (texts request.Remembered))
        OpenWithDialog.choice

let editPrograms (app: IApplication) (request: ProgramsPrompt) =
    running app (fun () -> SettingsDialog.create request.Programs request.Candidate) SettingsDialog.programs

let chooseHistory (app: IApplication) (fs: IFileSystem) (request: HistoryPrompt) =
    let asking title question =
        confirm app { Title = title; Question = question }

    running
        app
        (fun () -> HistoryDialog.create fs asking request.Change request.History request.Tab request.Now)
        HistoryDialog.answer

let chooseTab (app: IApplication) (fs: IFileSystem) (tabs: Zipper<Tab>) =
    let rows =
        tabs |> Zipper.items |> List.mapi (fun index -> TabRow.describe fs (index + 1))

    running app (fun () -> TabDialog.create rows (Zipper.position tabs)) TabDialog.choice

let private askMenuEntry (app: IApplication) (prompt: MenuEntryForm.EntryPrompt) =
    let createDialog () = MenuEntryDialog.create prompt
    running app createDialog MenuEntryDialog.answer

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

    let createDialog () =
        UserMenuDialog.create editing request.Menu

    running app createDialog UserMenuDialog.chosen
