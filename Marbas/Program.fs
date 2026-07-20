module Marbas.Program

open System.IO.Abstractions
open Terminal.Gui.App
open Terminal.Gui.Views
open Marbas.Ui

[<EntryPoint>]
let main _ =
    MainWindow.configureQuitKey ()
    let fs = FileSystem()
    use app = Application.Create().Init()

    let reportError message =
        MessageBox.ErrorQuery(app, ListingErrorMessage.Title, message, "Ok") |> ignore

    let chooseDrive drives =
        match drives with
        | [] -> None
        | _ ->
            let buttons = drives @ [ "Cancel" ] |> Array.ofList

            match MessageBox.Query(app, "Select drive", "", buttons) with
            | index when index.HasValue && index.Value < drives.Length -> Some drives[index.Value]
            | _ -> None

    use window =
        MainWindow.create
            { FileSystem = fs
              StartPath = fs.Directory.GetCurrentDirectory()
              OnQuit = fun () -> app.RequestStop()
              ReportError = reportError
              ChooseDrive = chooseDrive }

    app.Run window |> ignore
    0
