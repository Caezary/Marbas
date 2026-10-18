module Marbas.Tests.State.Shell.ShellUserMenuTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Profile
open Marbas.Domain
open Marbas.Messages
open Marbas.State
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.Shell.ShellFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private menuEntry hotkey label line =
    { Hotkey = Expect.some (MenuHotkey.tryCreate hotkey)
      Label = Expect.some (MenuLabel.tryCreate label)
      Line = Expect.some (CommandLine.tryCreate line)
      Run = Directly
      Elevation = AsUser }

let private ranCommand: CommandRun =
    { Label = Expect.some (MenuLabel.tryCreate "Show")
      Command =
        { Program = { Text = "show"; Quoting = Bare }
          Arguments = []
          ListFile = None }
      Run = Directly
      Elevation = AsUser
      WorkingDirectory = fp (at "start") }

[<Fact>]
let ``the user menu popup is handed the menu its file holds`` () =
    let fs = MockFileSystem()

    let written =
        UserMenu.apply (AddItem(CommandItem(menuEntry "S" "Show" "show %p"), None)) UserMenu.empty
        |> Expect.ok

    fs.AddFile(at "user-menu.conf", MockFileData(String.concat "\n" (UserMenu.format written)))
    let asked = ResizeArray<Ports.UserMenuPrompt>()

    let deps =
        { Fakes.dependencies fs (at "start") with
            Prompts.ChooseFromUserMenu =
                fun prompt ->
                    asked.Add prompt
                    None }

    Shell.interpret deps ignore ShowUserMenu

    Assert.Equal(written, (Assert.Single asked).Menu)

[<Fact>]
let ``with no file the user menu popup is handed an empty menu`` () =
    let asked = ResizeArray<Ports.UserMenuPrompt>()

    let deps =
        { noDeps () with
            Prompts.ChooseFromUserMenu =
                fun prompt ->
                    asked.Add prompt
                    None }

    Shell.interpret deps ignore ShowUserMenu

    Assert.Equal(UserMenu.empty, (Assert.Single asked).Menu)

[<Fact>]
let ``an entry chosen in the user menu is dispatched with a list file in the session's working directory`` () =
    let sent, dispatch = capture ()
    let chosen = menuEntry "S" "Show" "show %f"

    let deps =
        { noDeps () with
            Prompts.ChooseFromUserMenu = fun _ -> Some chosen }

    Shell.interpret deps dispatch ShowUserMenu

    Assert.Equal<Message list>(
        [ MenuCommandChosen(chosen, fp (at "temp/Marbas/session/marked-1.txt")) ],
        List.ofSeq sent
    )

[<Fact>]
let ``a user menu that cannot be read is reported and the popup is not shown`` () =
    let fs = Fakes.UnreadableFile(at "user-menu.conf")
    fs.AddFile(at "user-menu.conf", MockFileData "entry.1=separator")
    let mutable reported = ""
    let mutable shown = false

    let deps =
        { Fakes.dependencies fs (at "start") with
            Prompts.ReportError = fun report -> reported <- report.Title
            Prompts.ChooseFromUserMenu =
                fun _ ->
                    shown <- true
                    None }

    Shell.interpret deps ignore ShowUserMenu

    Assert.Equal(UserMenuErrorMessage.Title, reported)
    Assert.False shown

[<Fact>]
let ``a command run is handed to the process port and its outcome dispatched back`` () =
    let sent, dispatch = capture ()
    let handed = ResizeArray<CommandRun>()

    let deps =
        { noDeps () with
            Processes.RunCommand =
                fun run ->
                    handed.Add run
                    Ok() }

    Shell.interpret deps dispatch (RunCommand ranCommand)

    Assert.Equal(ranCommand, Assert.Single handed)
    Assert.Equal<Message list>([ CommandFinished(ranCommand, Ok()) ], List.ofSeq sent)

[<Fact>]
let ``a refused expansion is reported under the command title`` () =
    let mutable reported = ""

    let deps =
        { noDeps () with
            Prompts.ReportError = fun report -> reported <- report.Title }

    Shell.interpret deps ignore (Report(Problem.Expansion NoProgram))

    Assert.Equal(ExpansionRefusalMessage.Title, reported)

[<Fact>]
let ``a command that could not start is reported under the command title`` () =
    let mutable reported = ""

    let deps =
        { noDeps () with
            Prompts.ReportError = fun report -> reported <- report.Title }

    Shell.interpret deps ignore (Report(Problem.Command(LaunchRefused(fp "show", "denied"))))

    Assert.Equal(LaunchErrorMessage.CommandTitle, reported)

[<Fact>]
let ``a change made in the user menu popup is written at once and handed back`` () =
    let fs = MockFileSystem()
    let mutable answered = None
    let added = CommandItem(menuEntry "S" "Show" "show %p")

    let deps =
        { Fakes.dependencies fs (at "start") with
            Prompts.ChooseFromUserMenu =
                fun prompt ->
                    answered <- prompt.Change(AddItem(added, None))
                    None }

    Shell.interpret deps ignore ShowUserMenu

    let written = UserMenu.load fs (fp (at "user-menu.conf")) |> Expect.ok
    Assert.Equal(Some written, answered)
    Assert.Equal<MenuItem list>([ added ], UserMenu.items written)

[<Fact>]
let ``a change the user menu refuses is reported and nothing is handed back`` () =
    let fs = MockFileSystem()
    let held = CommandItem(menuEntry "S" "Show" "show %p")
    let written = UserMenu.apply (AddItem(held, None)) UserMenu.empty |> Expect.ok
    fs.AddFile(at "user-menu.conf", MockFileData(String.concat "\n" (UserMenu.format written)))
    let mutable answered = Some UserMenu.empty
    let mutable reported = ""

    let deps =
        { Fakes.dependencies fs (at "start") with
            Prompts.ReportError = fun report -> reported <- report.Message
            Prompts.ChooseFromUserMenu =
                fun prompt ->
                    answered <- prompt.Change(AddItem(CommandItem(menuEntry "s" "Search" "grep %s"), None))
                    None }

    Shell.interpret deps ignore ShowUserMenu

    Assert.Equal(None, answered)
    Assert.Contains("'S'", reported)
