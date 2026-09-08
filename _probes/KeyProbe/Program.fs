module KeyProbe.Program

open System
open System.Text
open Terminal.Gui.App
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views

[<Literal>]
let private NoHotKey = 0xffff

[<Literal>]
let private LogLength = 12

let private literal (view: #View) =
    view.HotKeySpecifier <- Rune NoHotKey
    view

let private quitKey = Key.Q.WithCtrl

let private wanted =
    [ "F11 tabs", Key.F11
      "Alt+T tabs", Key.T.WithAlt
      "Alt+F11 new", Key.F11.WithAlt
      "Alt+Shift+F11 shut", Key.F11.WithAlt.WithShift
      "Shift+F9 save", Key.F9.WithShift
      "Alt+Left back", Key.CursorLeft.WithAlt
      "Alt+Right on", Key.CursorRight.WithAlt ]

let private candidates =
    [ "Ctrl+F11", Key.F11.WithCtrl
      "Ctrl+Shift+F11", Key.F11.WithCtrl.WithShift
      "Alt+Shift+T", Key.T.WithAlt.WithShift
      "Ctrl+T", Key.T.WithCtrl ]

let private modifiersOf (key: Key) =
    [ if key.IsCtrl then "ctrl"
      if key.IsAlt then "alt"
      if key.IsShift then "shift" ]
    |> function
        | [] -> "none"
        | held -> String.concat "+" held

let private describe (key: Key) =
    $"%O{key}  code=0x%X{int key.KeyCode}  held=%s{modifiersOf key}"

let mutable private seen: Map<string, string> = Map.empty

let mutable private log: string list = []

let private record (key: Key) =
    log <- describe key :: log |> List.truncate LogLength

    for name, expected in wanted @ candidates do
        if key = expected then
            seen <- Map.add name (describe key) seen

let private checklist () =
    let rows (heading: string) keys =
        heading
        :: (keys
            |> List.map (fun (name, _) ->
                match Map.tryFind name seen with
                | Some arrival -> $"  [x] %-20s{name} %s{arrival}"
                | None -> $"  [ ] %-20s{name}"))

    rows "Wanted:" wanted @ [ "" ] @ rows "Candidates:" candidates

let private screen () =
    checklist () @ [ ""; "Last keys, newest first:" ] @ (log |> List.map (fun line -> "  " + line))

let private environmentOf (name: string) =
    match Environment.GetEnvironmentVariable name with
    | null -> "(unset)"
    | value -> value

let private report (driver: string) =
    let term = environmentOf "TERM"
    let session = environmentOf "WT_SESSION"

    [ $"Terminal.Gui driver: %s{driver}"
      $"TERM=%s{term}  WT_SESSION=%s{session}"
      "" ]
    @ screen ()

[<EntryPoint>]
let main _ =
    Application.SetDefaultKeyBinding(Command.Quit, Bind.All quitKey)
    let app = Application.Create().Init()
    let driver = app.Driver.GetType().Name

    let window =
        new Window(Title = "Marbas key probe — press the keys below, Ctrl+Q quits", Width = Dim.Fill(), Height = Dim.Fill())

    let board =
        new Label(X = 1, Y = 0, Width = Dim.Fill 1, Height = Dim.Fill(), Text = String.concat "\n" (screen ()))
        |> literal

    window.Add board |> ignore

    app.Keyboard.KeyDown.Add(fun key ->
        if key <> quitKey then
            record key
            board.Text <- String.concat "\n" (screen ())
            key.Handled <- true)

    app.Run window |> ignore
    window.Dispose()
    app.Dispose()

    let lines = report driver
    let stamp = DateTimeOffset.Now.ToString "yyyyMMdd-HHmmss"
    let file = IO.Path.GetFullPath $"keyprobe-%s{stamp}.txt"
    IO.File.WriteAllLines(file, lines)

    for line in lines do
        printfn "%s" line

    printfn ""
    printfn "Written to %s" file

    0
