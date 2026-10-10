module Marbas.Tests.Integration.App.UserMenuKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Marbas.Domain
open Marbas.FileSystem
open Marbas.State
open Marbas.Ui.Keys
open Marbas.State.Messages
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs

let private entry line =
    { Hotkey = Expect.some (MenuHotkey.tryCreate "S")
      Label = Expect.some (MenuLabel.tryCreate "Show")
      Line = Expect.some (CommandLine.tryCreate line)
      Run = InShellKeptOpen
      Elevation = AsUser }

let private windowChoosing (fs: MockFileSystem) chosen (runs: ResizeArray<CommandRun>) reported =
    focusedWindow
        { Fakes.dependencies fs (at "start") with
            Prompts.ChooseFromUserMenu = fun _ -> Some chosen
            Prompts.ReportError = fun (report: Ports.ErrorReport) -> reported report.Title
            Processes.RunCommand =
                fun run ->
                    runs.Add run
                    Ok() }

[<Fact>]
let ``F2 runs the entry chosen in the menu against the file under the cursor`` () =
    let runs = ResizeArray<CommandRun>()
    use window = windowChoosing (mockFs ()) (entry "show %p") runs ignore
    (leftList window).SelectedItem <- 2

    pressKey window KeyMap.userMenuKey

    let run = Assert.Single runs

    Assert.Equal<CommandWord list>(
        [ { Text = at "start/notes.txt"
            Quoting = Quoted } ],
        run.Command.Arguments
    )

    Assert.Equal(InShellKeptOpen, run.Run)

[<Fact>]
let ``a command run from the menu is remembered on the history`` () =
    let fs = mockFs ()
    let runs = ResizeArray<CommandRun>()
    use window = windowChoosing fs (entry "show %p") runs ignore
    (leftList window).SelectedItem <- 2

    pressKey window KeyMap.userMenuKey

    let history = History.load fs (fp (at "history.conf")) |> Expect.ok
    Assert.Equal<Started list>([ StartedCommand(Assert.Single runs) ], history.Launches |> List.map _.Item)

[<Fact>]
let ``a run the panels refuse is reported and nothing is started`` () =
    let runs = ResizeArray<CommandRun>()
    let reported = ResizeArray<string>()
    use window = windowChoosing (mockFs ()) (entry "show %p") runs reported.Add
    (leftList window).SelectedItem <- 0

    pressKey window KeyMap.userMenuKey

    Assert.Empty runs
    Assert.Equal(ExpansionRefusalMessage.Title, Assert.Single reported)

[<Fact>]
let ``closing the menu without a choice starts nothing`` () =
    let runs = ResizeArray<CommandRun>()

    use window =
        focusedWindow
            { Fakes.dependencies (mockFs ()) (at "start") with
                Processes.RunCommand =
                    fun run ->
                        runs.Add run
                        Ok() }

    pressKey window Key.F2

    Assert.Empty runs

[<Fact>]
let ``a menu edited and closed is the same menu after a restart`` () =
    let fs = mockFs ()
    let added = CommandItem(entry "show %p")
    let below = SeparatorAfter(Some(Expect.some (MenuHotkey.tryCreate "S")), 0)
    let shown = ResizeArray<UserMenu>()

    let editing (prompt: Ports.UserMenuPrompt) =
        [ AddItem(added, None)
          AddItem(SeparatorItem, Some(CommandAt(Expect.some (MenuHotkey.tryCreate "S")))) ]
        |> List.iter (prompt.Change >> ignore)

        None

    use first =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                Prompts.ChooseFromUserMenu = editing }

    use restarted =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                Prompts.ChooseFromUserMenu =
                    fun prompt ->
                        shown.Add prompt.Menu
                        None }

    pressKey first KeyMap.userMenuKey
    pressKey restarted KeyMap.userMenuKey

    let menu = Assert.Single shown
    Assert.Equal<MenuItem list>([ added; SeparatorItem ], UserMenu.items menu)
    Assert.Equal(Some below, UserMenu.refAt 1 menu)
