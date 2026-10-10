module Marbas.Tests.Adapters.DebugLogTests

open Xunit
open Marbas.FileSystem
open Marbas.State
open Marbas.State.Update
open Marbas.Adapters
open Marbas.Tests

let private recorder () =
    let recorded = ResizeArray<LogEntry>()

    let record entry =
        recorded.Add entry
        Ok()

    recorded, record

[<Fact>]
let ``--debug turns the transition log on`` () =
    let logging = Logging.ofArguments [| "--debug" |]

    Assert.Equal(EveryTransition, logging)

[<Fact>]
let ``without arguments only failures are logged`` () =
    let logging = Logging.ofArguments [||]

    Assert.Equal(FailuresOnly, logging)

[<Theory>]
[<InlineData "--verbose">]
[<InlineData "debug">]
[<InlineData "-debug">]
let ``another argument leaves the transition log off`` (argument: string) =
    let logging = Logging.ofArguments [| argument |]

    Assert.Equal(FailuresOnly, logging)

[<Fact>]
let ``with the transition log off no transition is written`` () =
    let recorded, record = recorder ()
    let log = DebugLog.writer FailuresOnly record

    log (EffectInterpreted Quit)

    Assert.Empty recorded

[<Fact>]
let ``with the transition log off a failure is still written`` () =
    let recorded, record = recorder ()
    let log = DebugLog.writer FailuresOnly record

    let kept =
        LeftoverKept(FileSystemPath.create (Root.at "temp/Marbas/earlier"), "held open")

    log kept

    Assert.Equal<LogEntry>([ kept ], recorded)

[<Fact>]
let ``with the transition log on every entry is written`` () =
    let recorded, record = recorder ()
    let log = DebugLog.writer EveryTransition record

    log (EffectInterpreted Quit)

    Assert.Equal<LogEntry>([ EffectInterpreted Quit ], recorded)
