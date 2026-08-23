module Probe

open System
open System.Drawing
open Terminal.Gui.App
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Marbas.Ui

let private screenSize = Rectangle(0, 0, 100, 24)

let private text (app: IApplication) = app.Driver.ToString()

let private colours (app: IApplication) (rows: int seq) =
    let contents = app.Driver.Contents

    for row in rows do
        [ for col in 0 .. Array2D.length2 contents - 1 ->
              string (Seq.head contents[row, col].Runes), string contents[row, col].Attribute ]
        |> List.fold
            (fun runs (rune, attribute) ->
                match runs with
                | (run, worn) :: rest when worn = attribute -> (run + rune, worn) :: rest
                | _ -> (rune, attribute) :: runs)
            []
        |> List.rev
        |> List.filter (fun (run, _) -> run.Trim() <> "")
        |> List.iter (fun (run, worn) -> eprintfn "  row %d '%s' %s" row run worn)

let private typing (app: IApplication) (keys: Key list) =
    keys
    |> List.iteri (fun index key ->
        app.AddTimeout(
            TimeSpan.FromMilliseconds(200.0 + 100.0 * float index),
            fun () ->
                app.Keyboard.RaiseKeyDownEvent key |> ignore
                false
        )
        |> ignore)

let private shot (app: IApplication) name (after: float) =
    app.AddTimeout(
        TimeSpan.FromMilliseconds after,
        fun () ->
            IO.File.WriteAllText(name + ".txt", text app)
            colours app [ 0 .. screenSize.Height - 1 ]
            app.RequestStop()
            false
    )
    |> ignore

let private scene (app: IApplication) (what: string) =
    match what with
    | "prompt" ->
        let dialog =
            PromptDialog.create "Create directory" "Name:" "work" |> PromptDialog.selecting

        typing app [ Key 'n'; Key 'e'; Key 'w' ]
        shot app what 600.0
        app.Run dialog |> ignore
        eprintfn "typed = %A, cancelled = %b" (PromptDialog.text dialog) dialog.Canceled
    | _ -> eprintfn "no scene named '%s'" what

[<EntryPoint>]
let main argv =
    MainWindow.configureQuitKey ()
    use app = Application.Create().Init()
    DialogStyle.apply ()
    typeof<IApplication>.GetProperty("Screen").SetValue(app, screenSize)

    scene app (if argv.Length = 0 then "prompt" else argv[0])

    printfn "%s" (text app)
    0
