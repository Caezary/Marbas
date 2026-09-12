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

[<Literal>]
let private PopupTitle = "Popup — the two keys below, then Esc or Close"

let private literal (view: #View) =
    view.HotKeySpecifier <- Rune NoHotKey
    view

let private quitKey = Key.Q.WithCtrl

let private popupKey = Key.F1

let private wanted =
    [ "Alt+Del wipe", Key.Delete.WithAlt
      "Ctrl+Backslash root", (Key '\\').WithCtrl ]

let private candidates =
    [ "Alt+Backslash root", (Key '\\').WithAlt
      "Alt+Shift+Del wipe", Key.Delete.WithAlt.WithShift ]

let private inPopup =
    [ "Shift+Enter reveal", Key.Enter.WithShift
      "Alt+R reveal", Key.R.WithAlt ]

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

let private record expected (key: Key) =
    log <- describe key :: log |> List.truncate LogLength

    for name, wantedKey in expected do
        if key = wantedKey then
            seen <- Map.add name (describe key) seen

let private rows (heading: string) keys =
    heading
    :: (keys
        |> List.map (fun (name, _) ->
            match Map.tryFind name seen with
            | Some arrival -> $"  [x] %-20s{name} %s{arrival}"
            | None -> $"  [ ] %-20s{name}"))

let private checklist () =
    rows "Wanted:" wanted
    @ [ "" ]
    @ rows "Stand-ins:" candidates
    @ [ "" ]
    @ rows "Inside a popup (F1 opens one):" inPopup

let private screen () =
    checklist ()
    @ [ ""; "Last keys, newest first:" ]
    @ (log |> List.map (fun line -> "  " + line))

let private popupScreen () =
    rows "Inside a popup:" inPopup
    @ [ ""; "Anything else arriving here:" ]
    @ (log |> List.truncate 3 |> List.map (fun line -> "  " + line))

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

let private runPopup (app: IApplication) =
    use dialog = new Dialog(Title = PopupTitle, Width = 70, Height = 12)

    let inside =
        new Label(X = 1, Y = 0, Width = Dim.Fill 1, Height = Dim.Fill 2, Text = String.concat "\n" (popupScreen ()))
        |> literal

    dialog.Add inside |> ignore
    dialog.AddButton(new Button(Title = "_Close"))

    dialog.KeyDown.Add(fun key ->
        record inPopup key
        inside.Text <- String.concat "\n" (popupScreen ())

        if inPopup |> List.exists (fun (_, wantedKey) -> key = wantedKey) then
            key.Handled <- true)

    app.Run dialog |> ignore

[<EntryPoint>]
let main _ =
    Application.SetDefaultKeyBinding(Command.Quit, Bind.All quitKey)
    let app = Application.Create().Init()
    let driver = app.Driver.GetType().Name

    let window =
        new Window(
            Title = "Marbas key probe — press the keys below, F1 opens a popup, Ctrl+Q quits",
            Width = Dim.Fill(),
            Height = Dim.Fill()
        )

    let board =
        new Label(X = 1, Y = 0, Width = Dim.Fill 1, Height = Dim.Fill(), Text = String.concat "\n" (screen ()))
        |> literal

    window.Add board |> ignore

    let mutable popupIsUp = false

    app.Keyboard.KeyDown.Add(fun key ->
        if popupIsUp || key = quitKey then
            ()
        elif key = popupKey then
            key.Handled <- true
            popupIsUp <- true
            runPopup app
            popupIsUp <- false
            board.Text <- String.concat "\n" (screen ())
        else
            record (wanted @ candidates) key
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
