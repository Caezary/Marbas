module Marbas.Tests.Integration.App.DebugLogTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.State
open Marbas.State.Update
open Marbas.Ui.Keys
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

[<Fact>]
let ``a key pressed in a panel leaves the message it dispatched`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start")
    let logged = ResizeArray<LogEntry>()

    use window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                Log = logged.Add }

    press (leftList window) KeyMap.hiddenKey

    Assert.Contains(MessageHandled(HiddenToggled, Left), logged)
