module Marbas.Program

open System
open System.Diagnostics
open System.IO.Abstractions
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks
open Terminal.Gui.App
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.FileSystem
open Marbas.Ui

[<Literal>]
let private VkShift = 0x10

[<Literal>]
let private VkControl = 0x11

[<Literal>]
let private VkAlt = 0x12

[<Literal>]
let private PollInterval = 100.0

[<Literal>]
let private ConfigDirectory = "Marbas"

[<Literal>]
let private ConfigFile = "settings.conf"

[<DllImport "user32.dll">]
extern int16 private GetAsyncKeyState(int vKey)

let private isDown vKey = GetAsyncKeyState vKey &&& 0x8000s <> 0s

let private pollModifiers () =
    [ if isDown VkControl then Ctrl
      if isDown VkAlt then Alt
      if isDown VkShift then Shift ]
    |> Modifiers.held

[<EntryPoint>]
let main _ =
    MainWindow.configureQuitKey ()
    let fs = FileSystem()
    use app = Application.Create().Init()

    let reportError title message =
        MessageBox.ErrorQuery(app, title, message, "Ok") |> ignore

    let chooseOption title options =
        match options with
        | [] -> None
        | _ ->
            let buttons = options @ [ "Cancel" ] |> Array.ofList

            match MessageBox.Query(app, title, "", buttons) with
            | index when index.HasValue && index.Value < options.Length -> Some options[index.Value]
            | _ -> None

    let confirm (title: string) (message: string) =
        let answer = MessageBox.Query(app, title, message, "_Yes", "_No")
        answer.HasValue && answer.Value = 0

    let askOverwrite (title: string) (message: string) =
        let answer = MessageBox.Query(app, title, message, Array.ofList OverwriteQuestion.Answers)
        OverwriteQuestion.answerAt (if answer.HasValue then answer.Value else -1)

    let askPattern title =
        use dialog = PatternDialog.create title
        app.Run dialog |> ignore
        PatternDialog.pattern dialog

    let askName title =
        use dialog = PromptDialog.create title "Name:" ""
        app.Run dialog |> ignore
        PromptDialog.text dialog

    let askTarget title suggestion =
        use dialog = PromptDialog.create title "To:" suggestion
        app.Run dialog |> ignore
        PromptDialog.text dialog

    let showFile title lines =
        use dialog = FileViewer.create (_.RequestStop()) title lines
        app.Run dialog |> ignore

    let editorCommand =
        let platform =
            if OperatingSystem.IsWindows() then
                EditorCommand.Windows
            else
                EditorCommand.Unix

        EditorCommand.choose (Environment.GetEnvironmentVariable "EDITOR" |> Option.ofObj) platform

    let runEditor path =
        try
            let start = ProcessStartInfo(editorCommand, UseShellExecute = false)
            start.ArgumentList.Add(FileSystemPath.value path)

            match Process.Start start with
            | null -> ()
            | editor ->
                use editor = editor
                editor.WaitForExit()

            app.ClearScreenNextIteration <- true
            Ok()
        with ex ->
            Error(Operations.mapException path ex)

    let watchModifiers report =
        if OperatingSystem.IsWindows() then
            let held = ref Modifiers.none

            app.AddTimeout(
                TimeSpan.FromMilliseconds PollInterval,
                fun () ->
                    let current = pollModifiers ()

                    if current <> held.Value then
                        held.Value <- current
                        report current

                    true
            )
            |> ignore

    let runBatch title work finish =
        let cancellation = new CancellationTokenSource()
        let dialog = ProgressDialog.create title

        let control: Operations.BatchControl =
            { Report =
                fun path ->
                    if not cancellation.IsCancellationRequested then
                        app.Invoke(fun () -> ProgressDialog.report dialog (FileSystemPath.value path))
              IsCancelled = fun () -> cancellation.IsCancellationRequested }

        Task.Run(fun () ->
            let results = work control

            app.Invoke(fun () ->
                if not cancellation.IsCancellationRequested then
                    dialog.RequestStop()

                finish results))
        |> ignore

        app.Run dialog |> ignore
        cancellation.Cancel()

    let settingsPath =
        fs.Path.Combine(
            Environment.GetFolderPath Environment.SpecialFolder.ApplicationData,
            ConfigDirectory,
            ConfigFile
        )
        |> FileSystemPath.create

    use window =
        MainWindow.create
            { FileSystem = fs
              StartPath = fs.Directory.GetCurrentDirectory() |> FileSystemPath.create
              SettingsPath = settingsPath
              OnQuit = fun () -> app.RequestStop()
              ReportError = reportError
              ChooseOption = chooseOption
              AskPattern = askPattern
              AskName = askName
              AskTarget = askTarget
              Confirm = confirm
              ShowFile = showFile
              RunEditor = runEditor
              AskOverwrite = askOverwrite
              WatchModifiers = watchModifiers
              RunBatch = runBatch }

    app.Run window |> ignore
    0
