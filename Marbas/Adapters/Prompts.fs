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

let chooseDrive (app: IApplication) (rows: DriveRow list) =
    match rows with
    | [] -> None
    | _ -> running app (fun () -> DriveDialog.create rows) DriveDialog.choice

let private texts (remembered: EnteredText list) = remembered |> List.map EnteredText.value

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

let showFile (app: IApplication) (source: FileWindow.Source) =
    running app (fun () -> FileViewer.create _.RequestStop() source) ignore

let editProperties (app: IApplication) properties =
    running app (fun () -> PropertiesDialog.create properties) (PropertiesDialog.properties properties)

let askArchive (app: IApplication) retarget (request: PackPrompt) =
    let offered = FileSystemPath.value request.Suggestion

    running app (fun () -> PackDialog.create retarget offered) PackDialog.choice

let askFind (app: IApplication) (request: FindPrompt) =
    running app (fun () -> FindDialog.create request.Ground request.Hidden request.Found) FindDialog.choice

let askLink (app: IApplication) (request: LinkPrompt) =
    let offered = FileSystemPath.value request.Suggestion

    running app (fun () -> LinkDialog.create request.Kinds request.Refusal offered) LinkDialog.choice

let askHandler (app: IApplication) (request: HandlerPrompt) =
    running app (fun () -> OpenWithDialog.create request.Name request.Handlers request.Chosen) OpenWithDialog.choice

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
