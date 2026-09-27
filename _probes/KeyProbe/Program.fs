module KeyProbe.Program

open System
open System.Collections.ObjectModel
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
let private PopupTitle = "Popup, buttons without hot keys — the keys below, then Esc"

[<Literal>]
let private FieldTitle = "Popup with a drop-down field — the keys below, then Esc"

let private literal (view: #View) =
    view.HotKeySpecifier <- Rune NoHotKey
    view

let private quitKey = Key.Q.WithCtrl

let private popupKey = Key.F1

let private fieldKey = Key.F2

let private wanted = [ "Alt+F12 history", Key.F12.WithAlt ]

let private candidates = [ "Alt+Shift+H history", Key.H.WithAlt.WithShift ]

let private inPopup =
    [ "Ctrl+PgUp tab", Key.PageUp.WithCtrl
      "Ctrl+PgDn tab", Key.PageDown.WithCtrl
      "Del forget", Key.Delete
      "Shift+Del clear", Key.Delete.WithShift
      "Alt+G go to", Key.G.WithAlt ]

let private inField =
    [ "Alt+Down list", Key.CursorDown.WithAlt
      "F4 list", Key.F4 ]

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
    @ [ "" ]
    @ rows "In a drop-down field inside a popup (F2 opens one):" inField

let private screen () =
    checklist ()
    @ [ ""; "Last keys, newest first:" ]
    @ (log |> List.map (fun line -> "  " + line))

let private insideScreen heading keys =
    rows heading keys
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

let mutable private listening: ((Key -> unit) option) = None

let private runPopup (app: IApplication) =
    use dialog = new Dialog(Title = PopupTitle, Width = 76, Height = 14)

    let inside =
        new Label(X = 1, Y = 0, Width = Dim.Fill 1, Height = Dim.Fill 2, Text = String.concat "\n" (insideScreen "Inside a popup:" inPopup))
        |> literal

    dialog.Add inside |> ignore
    dialog.AddButton(new Button(Title = "Close") |> literal)
    dialog.AddButton(new Button(Title = "Go to") |> literal)

    listening <-
        Some(fun key ->
            record inPopup key
            inside.Text <- String.concat "\n" (insideScreen "Inside a popup:" inPopup)

            if inPopup |> List.exists (fun (_, wantedKey) -> key = wantedKey) then
                key.Handled <- true)

    app.Run dialog |> ignore
    listening <- None

let private runField (app: IApplication) =
    use dialog = new Dialog(Title = FieldTitle, Width = 76, Height = 14)

    let field = new DropDownList(X = 1, Y = 1, Width = Dim.Fill 1, ReadOnly = false)
    field.Source <- new ListWrapper<string>(ObservableCollection [ "first remembered"; "second remembered" ])

    let inside =
        new Label(X = 1, Y = 3, Width = Dim.Fill 1, Height = Dim.Fill 2, Text = String.concat "\n" (insideScreen "In the field:" inField))
        |> literal

    dialog.Add(new Label(X = 1, Y = 0, Text = "Field:") |> literal, field, inside) |> ignore
    dialog.AddButton(new Button(Title = "Close") |> literal)

    listening <-
        Some(fun key ->
            record inField key
            inside.Text <- String.concat "\n" (insideScreen "In the field:" inField))

    app.Run dialog |> ignore
    listening <- None

[<EntryPoint>]
let main _ =
    Application.SetDefaultKeyBinding(Command.Quit, Bind.All(quitKey, Key.Esc))
    let app = Application.Create().Init()
    let driver = app.Driver.GetType().Name

    let window =
        new Window(
            Title = "Marbas key probe — press the keys below, F1 a popup, F2 a drop-down field, Ctrl+Q quits",
            Width = Dim.Fill(),
            Height = Dim.Fill()
        )

    let board =
        new Label(X = 1, Y = 0, Width = Dim.Fill 1, Height = Dim.Fill(), Text = String.concat "\n" (screen ()))
        |> literal

    window.Add board |> ignore

    app.Keyboard.KeyDown.Add(fun key ->
        match listening with
        | Some listen -> listen key
        | None when key = quitKey -> ()
        | None when key = popupKey || key = fieldKey ->
            key.Handled <- true
            (if key = popupKey then runPopup app else runField app)
            board.Text <- String.concat "\n" (screen ())
        | None ->
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
