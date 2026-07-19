module Marbas.Program

open System.IO.Abstractions
open Terminal.Gui.App
open Marbas.Ui

[<EntryPoint>]
let main _ =
    MainWindow.configureQuitKey ()
    let fs = FileSystem()
    use app = Application.Create().Init()

    use window =
        MainWindow.create fs (fs.Directory.GetCurrentDirectory()) (fun () -> app.RequestStop())

    app.Run window |> ignore
    0
