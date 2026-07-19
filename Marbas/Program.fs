module Marbas.Program

open Terminal.Gui.App
open Marbas.Ui

[<EntryPoint>]
let main _ =
    MainWindow.configureQuitKey ()
    use app = Application.Create().Init()
    use window = MainWindow.create ()
    app.Run window |> ignore
    0
