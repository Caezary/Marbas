module Marbas.Program

open System.IO.Abstractions
open Terminal.Gui.App
open Marbas.Adapters
open Marbas.Ui.Theme
open Marbas.Ui.App

[<EntryPoint>]
let main _ =
    MainWindow.configureQuitKey ()
    use app = Application.Create().Init()
    DialogStyle.apply ()

    let dependencies = Dependencies.create app (FileSystem()) (HostEnvironment.current ())
    use window = MainWindow.create dependencies

    app.Run window |> ignore
    0
