module Marbas.Tests.State.Shell.ShellLaunchTests

open Xunit
open Marbas.FileSystem
open Marbas.Programs
open Marbas.Domain
open Marbas.Messages
open Marbas.State
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.Shell.ShellFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private en = EntryName.create

let private launching target =
    { Target = fp target
      Program = None
      WorkingDirectory = fp (at "start")
      Elevation = AsUser }

[<Fact>]
let ``a program the effect names is handed to the launcher and its outcome dispatched back`` () =
    let sent, dispatch = capture ()
    let asked = ResizeArray<LaunchRequest>()

    let deps =
        { noDeps () with
            Processes.Launch =
                fun request ->
                    asked.Add request
                    Ok() }

    Shell.interpret deps dispatch (RunProgram(launching (at "start/notes.txt")))

    Assert.Equal(launching (at "start/notes.txt"), Assert.Single asked)
    Assert.Equal<Message list>([ LaunchFinished(launching (at "start/notes.txt"), Ok()) ], List.ofSeq sent)

[<Fact>]
let ``a launch that fails dispatches its failure back`` () =
    let sent, dispatch = capture ()
    let failure = NothingAssociated(fp (at "start/notes.odd"))

    let deps =
        { noDeps () with
            Processes.Launch = fun _ -> Error failure }

    Shell.interpret deps dispatch (RunProgram(launching (at "start/notes.odd")))

    Assert.Equal<Message list>([ LaunchFinished(launching (at "start/notes.odd"), Error failure) ], List.ofSeq sent)

[<Fact>]
let ``a launch failure is reported under its own title`` () =
    let mutable reported = ("", "")

    let deps =
        { noDeps () with
            Prompts.ReportError = fun report -> reported <- (report.Title, report.Message) }

    Shell.interpret deps ignore (Report(Problem.Launch(NothingAssociated(fp (at "start/notes.odd")))))

    Assert.Equal(LaunchErrorMessage.Title, fst reported)
    Assert.Contains("notes.odd", snd reported)

[<Fact>]
let ``the handlers of the file the effect names are the ones offered`` () =
    let asked = ResizeArray<EntryName * Handler list * ProgramPath option>()

    let firefox =
        Handler.tryCreate "Firefox" (ProgramPath.tryCreate "firefox.exe") |> Expect.some

    let deps =
        { noDeps () with
            Processes.HandlersFor = fun _ -> [ firefox ]
            Prompts.AskHandler =
                fun request ->
                    asked.Add(request.Name, request.Handlers, request.Chosen)
                    None }

    Shell.interpret deps ignore (ShowHandlers(fp (at "start/page.html"), ProgramPath.tryCreate "code.exe"))

    let name, handlers, chosen = Assert.Single asked

    Assert.Equal(en "page.html", name)
    Assert.Equal(firefox, Assert.Single handlers)
    Assert.Equal(ProgramPath.tryCreate "code.exe", chosen)

[<Fact>]
let ``a program picked out of the popup is dispatched back with the file it opens`` () =
    let sent, dispatch = capture ()

    let choice =
        { Program = Expect.some (ProgramPath.tryCreate "firefox.exe")
          Use = AlwaysThisWay }

    let deps =
        { noDeps () with
            Prompts.AskHandler = fun _ -> Some choice }

    Shell.interpret deps dispatch (ShowHandlers(fp (at "start/page.html"), None))

    Assert.Contains(HandlerChosen(fp (at "start/page.html"), choice), sent)

[<Fact>]
let ``a dismissed open-with popup dispatches nothing`` () =
    let sent, dispatch = capture ()

    Shell.interpret (noDeps ()) dispatch (ShowHandlers(fp (at "start/page.html"), None))

    Assert.Empty sent
