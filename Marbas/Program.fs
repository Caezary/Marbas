module Marbas.Program

open System.IO.Abstractions
open Terminal.Gui.App
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Ui

[<EntryPoint>]
let main _ =
    MainWindow.configureQuitKey ()
    let fs = FileSystem()
    use app = Application.Create().Init()

    let reportError title message =
        MessageBox.ErrorQuery(app, title, message, "Ok") |> ignore

    let chooseOption title options =
        match options with
        | [] -> None
        | _ ->
            let buttons = options @ [ "Cancel" ] |> Array.ofList

            match MessageBox.Query(app, title, "", buttons) with
            | index when index.HasValue && index.Value < options.Length -> Some options[index.Value]
            | _ -> None

    let confirm (title: string) (message: string) =
        let answer = MessageBox.Query(app, title, message, "_Yes", "_No")
        answer.HasValue && answer.Value = 0

    let askPattern title =
        use dialog = PatternDialog.create title
        app.Run dialog |> ignore
        PatternDialog.pattern dialog

    let askName title =
        use dialog = PromptDialog.create title "Name:" ""
        app.Run dialog |> ignore
        PromptDialog.text dialog

    use window =
        MainWindow.create
            { FileSystem = fs
              StartPath = fs.Directory.GetCurrentDirectory()
              OnQuit = fun () -> app.RequestStop()
              ReportError = reportError
              ChooseOption = chooseOption
              AskPattern = askPattern
              AskName = askName
              Confirm = confirm }

    app.Run window |> ignore
    0
