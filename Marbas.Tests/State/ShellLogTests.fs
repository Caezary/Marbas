module Marbas.Tests.State.ShellLogTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.State
open Marbas.State.Update
open Marbas.State.Messages
open Marbas.Tests

let private logging () =
    let logged = ResizeArray<LogEntry>()

    let deps =
        { Fakes.dependencies (MockFileSystem()) (Root.at "start") with
            Log = logged.Add }

    logged, deps

[<Fact>]
let ``an interpreted effect leaves its entry`` () =
    let logged, deps = logging ()

    Shell.interpret deps ignore Quit

    Assert.Contains(EffectInterpreted Quit, logged)

[<Fact>]
let ``an error shown to the user leaves its title and its words`` () =
    let logged, deps = logging ()
    let error = Operations.BatchFailed "the walk fell over"

    Shell.interpret deps ignore (ReportOperationError error)

    Assert.Contains(ErrorReported(OperationErrorMessage.Title, OperationErrorMessage.describe error), logged)
