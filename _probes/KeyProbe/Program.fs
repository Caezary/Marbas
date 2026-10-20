module KeyProbe.Program

open System
open System.Collections.Generic
open System.Collections.ObjectModel
open System.Text
open Terminal.Gui.App
open Terminal.Gui.Drivers
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views

[<Literal>]
let private NoHotKey = 0xffff

[<Literal>]
let private EarlierShown = 8

[<Literal>]
let private PasteShown = 40

type private Prompt = { Name: string; Hint: string }

type private Place =
    | Panel
    | Viewer
    | ListDialog
    | FieldDialog

type private Stage =
    { Title: string
      Place: Place
      Receiver: string
      Prompts: Prompt list }

type private Arrivals =
    { Application: ResizeArray<string>
      View: ResizeArray<string>
      Driver: ResizeArray<string> }

let private prompt name hint = { Name = name; Hint = hint }

let private inPanel =
    [ prompt "grey +" "the + of the numeric keypad, alone"
      prompt "main +" "Shift with the = key of the main keyboard"
      prompt "grey -" "the - of the numeric keypad, alone"
      prompt "main -" "the - key of the main keyboard, alone"
      prompt "Ctrl+grey +" "Ctrl with the + of the numeric keypad"
      prompt "Ctrl+main +" "Ctrl+Shift with the = key of the main keyboard"
      prompt "Ctrl+main =" "Ctrl with the = key of the main keyboard, no Shift"
      prompt "Ctrl+grey -" "Ctrl with the - of the numeric keypad"
      prompt "Ctrl+main -" "Ctrl with the - key of the main keyboard"
      prompt "Alt+grey +" "left Alt with the + of the numeric keypad"
      prompt "Alt+main +" "left Alt+Shift with the = key of the main keyboard"
      prompt "Alt+main =" "left Alt with the = key of the main keyboard, no Shift"
      prompt "Alt+grey -" "left Alt with the - of the numeric keypad"
      prompt "Alt+main -" "left Alt with the - key of the main keyboard"
      prompt "Shift+grey +" "Shift with the + of the numeric keypad"
      prompt "Shift+grey -" "Shift with the - of the numeric keypad"
      prompt "Shift+main -" "Shift with the - key of the main keyboard (an underscore on most layouts)"
      prompt "grey *" "the * of the numeric keypad, alone"
      prompt "Ctrl+grey *" "Ctrl with the * of the numeric keypad"
      prompt "Ctrl+main *" "Ctrl+Shift with the 8 key of the main keyboard"
      prompt "Ctrl+M" "Ctrl with the letter M"
      prompt "Ctrl+PgUp" "Ctrl with Page Up"
      prompt "Alt+0 … Alt+9" "left Alt with each digit of the top row, 0 to 9 in order"
      prompt "Shift+Alt+0 … Shift+Alt+9" "left Alt+Shift with each digit of the top row, 0 to 9 in order"
      prompt "Alt+F8" "left Alt with F8"
      prompt "F2" "F2 alone"
      prompt "Ctrl+Ins" "Ctrl with Insert"
      prompt "Alt+Shift+Ins" "left Alt+Shift with Insert"
      prompt "Ctrl+Alt+Ins" "Ctrl with left Alt and Insert"
      prompt "Ctrl+Shift+Ins" "Ctrl+Shift with Insert"
      prompt "Ctrl+Shift+C" "Ctrl+Shift with the letter C"
      prompt "Ctrl+Shift+X" "Ctrl+Shift with the letter X"
      prompt "Ctrl+Shift+V" "Ctrl+Shift with the letter V" ]

let private inViewer =
    [ prompt "F7" "F7 alone"
      prompt "Shift+F7" "Shift with F7"
      prompt "F8" "F8 alone" ]

let private inListDialog =
    [ prompt "a bare letter" "the letter e, alone"
      prompt "Ins" "Insert alone"
      prompt "Del" "Delete alone"
      prompt "F4" "F4 alone"
      prompt "Ctrl+Up" "Ctrl with the up arrow"
      prompt "Ctrl+Down" "Ctrl with the down arrow" ]

let private standInsInPanel =
    [ prompt "Alt+E" "left Alt with the letter E"
      prompt "Alt+Shift+E" "left Alt+Shift with the letter E"
      prompt "Alt+N" "left Alt with the letter N"
      prompt "Alt+Shift+N" "left Alt+Shift with the letter N"
      prompt "Alt+A" "left Alt with the letter A"
      prompt "Alt+Shift+A" "left Alt+Shift with the letter A"
      prompt "Alt+I" "left Alt with the letter I"
      prompt "main *" "Shift with the 8 key of the main keyboard"
      prompt "Alt+M" "left Alt with the letter M"
      prompt "Alt+U" "left Alt with the letter U"
      prompt "Alt+B" "left Alt with the letter B"
      prompt "Alt+K, then a digit" "left Alt with the letter K, let go, then the 5 of the top row"
      prompt "Alt+C" "left Alt with the letter C"
      prompt "Alt+P" "left Alt with the letter P"
      prompt "Alt+L" "left Alt with the letter L"
      prompt "Alt+J" "left Alt with the letter J"
      prompt "Alt+Shift+C" "left Alt+Shift with the letter C"
      prompt "Alt+Shift+X" "left Alt+Shift with the letter X"
      prompt "Alt+Shift+V" "left Alt+Shift with the letter V" ]

let private standInsInListDialog =
    [ prompt "a bare digit" "the 5 of the top row, alone"
      prompt "Enter" "Enter alone" ]

let private tabLettersInPanel =
    [ prompt "Alt+F11" "left Alt with F11"
      prompt "Alt+Shift+F11" "left Alt+Shift with F11"
      prompt "Alt+O" "left Alt with the letter O"
      prompt "Alt+Shift+O" "left Alt+Shift with the letter O"
      prompt "Alt+S" "left Alt with the letter S"
      prompt "Alt+Shift+S" "left Alt+Shift with the letter S"
      prompt "Alt+Y" "left Alt with the letter Y"
      prompt "Alt+Shift+Y" "left Alt+Shift with the letter Y"
      prompt "Alt+Z" "left Alt with the letter Z"
      prompt "Alt+Shift+Z" "left Alt+Shift with the letter Z"
      prompt "Alt+Q" "left Alt with the letter Q"
      prompt "Alt+Shift+Q" "left Alt+Shift with the letter Q" ]

let private helpOpeners =
    [ prompt "F1" "F1 alone"
      prompt "Ctrl+F1" "Ctrl with F1"
      prompt "Alt+S" "left Alt with the letter S"
      prompt "Alt+Y" "left Alt with the letter Y" ]

let private inHelp =
    helpOpeners
    @ [ prompt "Tab" "Tab alone"
        prompt "Shift+Tab" "Shift with Tab"
        prompt "Backspace" "Backspace alone"
        prompt "Alt+Left" "left Alt with the left arrow" ]

let private panelOf prompts =
    { Title = "The panel — a ListView with the focus, as in Marbas"
      Place = Panel
      Receiver = "the list"
      Prompts = prompts }

let private listDialogOf prompts =
    { Title = "A popup holding a list — a modal Dialog, the list focused"
      Place = ListDialog
      Receiver = "the list"
      Prompts = prompts }

let private viewerOf prompts =
    { Title = "The viewer — a full-screen window with no buttons"
      Place = Viewer
      Receiver = "the viewer"
      Prompts = prompts }

let private fieldDialogOf prompts =
    { Title = "A popup holding a text field — a modal Dialog, the field focused"
      Place = FieldDialog
      Receiver = "the field"
      Prompts = prompts }

let private keyRound =
    [ panelOf inPanel; viewerOf inViewer; listDialogOf inListDialog ]

let private standInRound =
    [ panelOf standInsInPanel; listDialogOf standInsInListDialog ]

let private tabLetterRound = [ panelOf tabLettersInPanel ]

let private helpRound =
    [ panelOf helpOpeners
      viewerOf inHelp
      listDialogOf helpOpeners
      fieldDialogOf helpOpeners ]

type private Controls =
    { Next: Key
      Back: Key
      NextName: string
      BackName: string }

let private tabControls =
    { Next = Key.Tab
      Back = Key.Tab.WithShift
      NextName = "Tab"
      BackName = "Shift+Tab" }

let private enterControls =
    { Next = Key.Enter
      Back = Key.P.WithCtrl
      NextName = "Enter"
      BackName = "Ctrl+P" }

let private roundNamed (argv: string array) =
    match argv with
    | [| "stand-ins" |] -> "stand-ins", standInRound, tabControls
    | [| "tab-letters" |] -> "tab-letters", tabLetterRound, tabControls
    | [| "help" |] -> "help", helpRound, enterControls
    | _ -> "keys", keyRound, tabControls

let mutable private roundName = "keys"

let mutable private stages = keyRound

let mutable private controls = tabControls

let private stopKey = Key.Q.WithCtrl

let private arrivals = Dictionary<int * string, Arrivals>()

let private arrivalsOf stage name =
    match arrivals.TryGetValue((stage, name)) with
    | true, found -> found
    | false, _ ->
        let fresh =
            { Application = ResizeArray()
              View = ResizeArray()
              Driver = ResizeArray() }

        arrivals[(stage, name)] <- fresh
        fresh

let mutable private stageIndex = 0

let mutable private promptIndex = 0

let mutable private stopped = false

let mutable private refresh: unit -> unit = ignore

let mutable private finishStage: unit -> unit = ignore

let private currentStage () = stages[stageIndex]

let private currentPrompt () = (currentStage ()).Prompts[promptIndex]

let private literal (view: #View) =
    view.HotKeySpecifier <- Rune NoHotKey
    view

let private modifiersOf (key: Key) =
    [ if key.IsCtrl then
          "ctrl"
      if key.IsAlt then
          "alt"
      if key.IsShift then
          "shift" ]
    |> function
        | [] -> "none"
        | held -> String.concat "+" held

let private visible (text: string) =
    text.Replace("\u001b", "\\e").Replace("\r", "\\r").Replace("\n", "\\n")

let private describe (key: Key) =
    let extras =
        [ if key.ShiftedKeyCode <> KeyCode.Null then
              $"shifted=0x%X{uint32 key.ShiftedKeyCode}"
          if key.BaseLayoutKeyCode <> KeyCode.Null then
              $"base=0x%X{uint32 key.BaseLayoutKeyCode}"
          if key.AssociatedText <> "" then
              $"text=%s{visible key.AssociatedText}"
          if key.EventType <> KeyEventType.Press then
              $"event=%O{key.EventType}" ]

    $"%O{key}  code=0x%X{uint32 key.KeyCode}  held=%s{modifiersOf key}" :: extras
    |> String.concat "  "

let private add (into: ResizeArray<string>) text = lock arrivals (fun () -> into.Add text)

let private summary (found: ResizeArray<string>) =
    lock arrivals (fun () ->
        match List.ofSeq found with
        | [] -> [ "nothing" ]
        | seen -> seen |> List.distinct)

let private firstOf (found: ResizeArray<string>) =
    lock arrivals (fun () -> if found.Count = 0 then "nothing" else found[0])

let private currentBoard () =
    let stage = currentStage ()
    let now = currentPrompt ()
    let found = arrivalsOf stageIndex now.Name

    let earlier =
        stage.Prompts
        |> List.take promptIndex
        |> List.rev
        |> List.truncate EarlierShown
        |> List.map (fun p -> $"  %-26s{p.Name} %s{firstOf (arrivalsOf stageIndex p.Name).View}")

    [ $"Stage %d{stageIndex + 1} of %d{stages.Length}: %s{stage.Title}"
      $"Press the key named below, as often as you like, then %s{controls.NextName} for the next one."
      $"%s{controls.BackName} goes back, Ctrl+Q stops and writes the report. Use the left Alt;"
      "digits come from the top row, never the keypad, and Num Lock stays on."
      ""
      $">> %d{promptIndex + 1}/%d{stage.Prompts.Length}  %s{now.Name}"
      $"   %s{now.Hint}"
      ""
      "   reached the application:" ]
    @ (summary found.Application |> List.map (fun line -> "     " + line))
    @ [ $"   reached %s{stage.Receiver}:" ]
    @ (summary found.View |> List.map (fun line -> "     " + line))
    @ [ "   swallowed or pasted by the driver:" ]
    @ (summary found.Driver |> List.map (fun line -> "     " + line))
    @ [ ""; "Earlier in this stage, newest first (what reached the receiver first):" ]
    @ earlier
    |> String.concat "\n"

let private advance () =
    if promptIndex + 1 < (currentStage ()).Prompts.Length then
        promptIndex <- promptIndex + 1
        refresh ()
    else
        finishStage ()

let private retreat () =
    if promptIndex > 0 then
        promptIndex <- promptIndex - 1
        refresh ()

let private atApplication (key: Key) =
    if key = controls.Next then
        key.Handled <- true
        advance ()
    elif key = controls.Back then
        key.Handled <- true
        retreat ()
    elif key = stopKey then
        key.Handled <- true
        stopped <- true
        finishStage ()
    else
        add (arrivalsOf stageIndex (currentPrompt ()).Name).Application (describe key)
        refresh ()

let private atReceiver (key: Key) =
    add (arrivalsOf stageIndex (currentPrompt ()).Name).View (describe key)
    key.Handled <- true
    refresh ()

let private fromDriver (app: IApplication) label (text: string) =
    let shown =
        if text.Length > PasteShown then
            text.Substring(0, PasteShown) + "…"
        else
            text

    add (arrivalsOf stageIndex (currentPrompt ()).Name).Driver $"%s{label}: %s{visible shown}"
    app.Invoke(fun () -> refresh ())

let private board () =
    new Label(X = 1, Y = 0, Width = Dim.Fill 1, Height = Dim.Fill 6, Text = currentBoard ())
    |> literal

let private entries () =
    new ListWrapper<string>(ObservableCollection [ ".."; "docs"; "notes.txt"; "readme.md"; "setup.exe" ])

let private receivingList () =
    let list = new ListView(X = 1, Y = Pos.AnchorEnd 6, Width = Dim.Fill 1, Height = 5)
    list.Source <- entries ()
    list.KeystrokeNavigator <- null
    list.KeyDown.Add atReceiver
    list

let private panelStage () =
    let window =
        new Window(Title = "Marbas key probe — the panel", Width = Dim.Fill(), Height = Dim.Fill())

    let shown = board ()
    let list = receivingList ()
    window.Add(shown, list) |> ignore
    window.Initialized.Add(fun _ -> list.SetFocus() |> ignore)
    window :> Runnable, shown

let private viewerStage () =
    let window =
        new Window(Title = "Marbas key probe — the viewer", Width = Dim.Fill(), Height = Dim.Fill())

    let shown = board ()

    let content =
        new View(X = 1, Y = Pos.AnchorEnd 6, Width = Dim.Fill 1, Height = 5, CanFocus = true)

    content.KeyDown.Add atReceiver
    window.Add(shown, content) |> ignore
    window.Initialized.Add(fun _ -> content.SetFocus() |> ignore)
    window :> Runnable, shown

let private listDialogStage () =
    let dialog =
        new Dialog(Title = "Marbas key probe — a popup holding a list", Width = Dim.Percent 90, Height = Dim.Percent 90)

    let shown = board ()
    let list = receivingList ()
    dialog.Add(shown, list) |> ignore
    dialog.AddButton(new Button(Title = "Close") |> literal)
    dialog.Initialized.Add(fun _ -> list.SetFocus() |> ignore)
    dialog :> Runnable, shown

let private fieldDialogStage () =
    let dialog =
        new Dialog(
            Title = "Marbas key probe — a popup holding a text field",
            Width = Dim.Percent 90,
            Height = Dim.Percent 90
        )

    let shown = board ()

    let field =
        new TextField(X = 1, Y = Pos.AnchorEnd 6, Width = Dim.Fill 1, Text = "notes.txt")

    field.KeyDown.Add atReceiver
    dialog.Add(shown, field) |> ignore
    dialog.AddButton(new Button(Title = "Close") |> literal)
    dialog.Initialized.Add(fun _ -> field.SetFocus() |> ignore)
    dialog :> Runnable, shown

let private runStage (app: IApplication) index =
    stageIndex <- index
    promptIndex <- 0

    let runnable, shown =
        match stages[index].Place with
        | Panel -> panelStage ()
        | Viewer -> viewerStage ()
        | ListDialog -> listDialogStage ()
        | FieldDialog -> fieldDialogStage ()

    refresh <- fun () -> shown.Text <- currentBoard ()
    finishStage <- fun () -> app.RequestStop runnable
    refresh ()
    app.Run runnable |> ignore
    runnable.Dispose()

let private environmentOf (name: string) =
    match Environment.GetEnvironmentVariable name with
    | null -> "(unset)"
    | value -> value

let private kittyOf (driver: IDriver) =
    match driver.KittyKeyboardCapabilities with
    | null -> "not reported"
    | capabilities -> $"%A{capabilities}"

let private reportPrompt index stage p =
    let found = arrivalsOf index p.Name

    [ $"-- %s{p.Name}  (%s{p.Hint})" ]
    @ (summary found.Application |> List.map (sprintf "     application: %s"))
    @ (summary found.View |> List.map (sprintf "     %s: %s" stage.Receiver))
    @ (summary found.Driver |> List.map (sprintf "     driver: %s"))

let private reportStage index stage =
    [ $"== %s{stage.Title}" ]
    @ (stage.Prompts |> List.collect (reportPrompt index stage))
    @ [ "" ]

let private report (driver: IDriver) =
    [ $"Round: %s{roundName}"
      $"Terminal.Gui driver: %s{driver.GetName()}"
      $"Kitty keyboard: %s{kittyOf driver}"
      $"""TERM=%s{environmentOf "TERM"}  TERM_PROGRAM=%s{environmentOf "TERM_PROGRAM"}"""
      $"""WT_SESSION=%s{environmentOf "WT_SESSION"}  ConEmuANSI=%s{environmentOf "ConEmuANSI"}"""
      $"Stopped early: %b{stopped}"
      "" ]
    @ (stages |> List.mapi reportStage |> List.concat)

[<EntryPoint>]
let main argv =
    let named, chosen, controlling = roundNamed argv
    roundName <- named
    stages <- chosen
    controls <- controlling
    Application.SetDefaultKeyBinding(Command.Quit, Bind.All stopKey)
    let app = Application.Create().Init()
    let processor = app.Driver.GetInputProcessor()

    app.Keyboard.KeyDown.Add atApplication
    processor.AnsiSequenceSwallowed.Add(fromDriver app "swallowed")
    processor.Paste.Add(fromDriver app "pasted")

    stages
    |> List.iteri (fun index _ ->
        if not stopped then
            runStage app index)

    let lines = report app.Driver
    app.Dispose()

    let stamp = DateTimeOffset.Now.ToString "yyyyMMdd-HHmmss"
    let file = IO.Path.GetFullPath $"keyprobe-%s{stamp}.txt"
    IO.File.WriteAllLines(file, lines)

    for line in lines do
        printfn "%s" line

    printfn ""
    printfn "Written to %s" file

    0
