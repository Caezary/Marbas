module Marbas.Tests.Adapters.TerminalHandOverTests

open Xunit
open Terminal.Gui.App
open Marbas.Adapters
open Marbas.Tests

[<Fact>]
let ``the Terminal.Gui in use lets the driver be stopped and started again`` () =
    let implementation =
        typeof<IApplication>.Assembly.GetType "Terminal.Gui.App.ApplicationImpl"
        |> Expect.notNull

    let restart = TerminalHandOver.restartOf implementation

    Assert.True restart.IsSome

[<Fact>]
let ``an application without those members gets no restart, so the editor shares the terminal as before`` () =
    let restart = TerminalHandOver.restartOf typeof<string>

    Assert.True restart.IsNone
