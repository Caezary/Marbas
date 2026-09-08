module Marbas.Adapters.Prompts

open System.IO.Abstractions
open Terminal.Gui.App
open Terminal.Gui.Views
open Marbas.Collections
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.Ui.Messages
open Marbas.Ui.Viewer
open Marbas.Ui.Dialogs

let reportError (app: IApplication) title message =
    MessageBox.ErrorQuery(app, title, message, "Ok") |> ignore

let chooseOption (app: IApplication) title options =
    match options with
    | [] -> None
    | _ ->
        let buttons = options @ [ "Cancel" ] |> Array.ofList

        match MessageBox.Query(app, title, "", buttons) with
        | index when index.HasValue && index.Value < options.Length -> Some options[index.Value]
        | _ -> None

let confirm (app: IApplication) (title: string) (message: string) =
    let answer = MessageBox.Query(app, title, message, "_Yes", "_No")
    answer.HasValue && answer.Value = 0

let askOverwrite (app: IApplication) (title: string) (message: string) =
    let answer = MessageBox.Query(app, title, message, Array.ofList OverwriteQuestion.Answers)
    OverwriteQuestion.answerAt (if answer.HasValue then answer.Value else -1)

let askPattern (app: IApplication) title =
    use dialog = PatternDialog.create title
    app.Run dialog |> ignore
    PatternDialog.pattern dialog

let askName (app: IApplication) title suggestion =
    use dialog = PromptDialog.create title "Name:" suggestion |> PromptDialog.selecting
    app.Run dialog |> ignore
    PromptDialog.text dialog

let askTarget (app: IApplication) title suggestion =
    use dialog = PromptDialog.create title "To:" suggestion
    app.Run dialog |> ignore
    PromptDialog.text dialog

let askPassword (app: IApplication) title label =
    use dialog = PromptDialog.create title label "" |> PromptDialog.secret
    app.Run dialog |> ignore
    PromptDialog.text dialog

let showFile (app: IApplication) (source: FileWindow.Source) =
    use viewer = FileViewer.create _.RequestStop() source
    app.Run viewer |> ignore

let editProperties (app: IApplication) properties =
    use dialog = PropertiesDialog.create properties
    app.Run dialog |> ignore
    PropertiesDialog.properties properties dialog

let askArchive (app: IApplication) retarget suggestion =
    use dialog = PackDialog.create retarget suggestion
    app.Run dialog |> ignore
    PackDialog.choice dialog

let askHandler (app: IApplication) name handlers chosen =
    use dialog = OpenWithDialog.create name handlers chosen
    app.Run dialog |> ignore
    OpenWithDialog.choice dialog

let editPrograms (app: IApplication) programs candidate =
    use dialog = SettingsDialog.create programs candidate
    app.Run dialog |> ignore
    SettingsDialog.programs dialog

let chooseTab (app: IApplication) (fs: IFileSystem) (tabs: Zipper<Tab>) =
    let rows =
        tabs |> Zipper.items |> List.mapi (fun index -> TabRow.describe fs (index + 1))

    use dialog = TabDialog.create rows (Zipper.position tabs)
    app.Run dialog |> ignore
    TabDialog.choice dialog
