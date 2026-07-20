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

    use window =
        MainWindow.create fs (fs.Directory.GetCurrentDirectory()) (fun () -> app.RequestStop()) reportError

    app.Run window |> ignore
    0
