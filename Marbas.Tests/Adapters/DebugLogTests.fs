module Marbas.Tests.Adapters.DebugLogTests

open Xunit
open Marbas.State
open Marbas.State.Update
open Marbas.Adapters

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
let ``without arguments only crashes are logged`` () =
    let logging = Logging.ofArguments [||]

    Assert.Equal(CrashesOnly, logging)

[<Theory>]
[<InlineData "--verbose">]
[<InlineData "debug">]
[<InlineData "-debug">]
let ``another argument leaves the transition log off`` (argument: string) =
    let logging = Logging.ofArguments [| argument |]

    Assert.Equal(CrashesOnly, logging)

[<Fact>]
let ``with the transition log off no entry is written`` () =
    let recorded, record = recorder ()
    let log = DebugLog.writer CrashesOnly record

    log (EffectInterpreted Quit)

    Assert.Empty recorded

[<Fact>]
let ``with the transition log on every entry is written`` () =
    let recorded, record = recorder ()
    let log = DebugLog.writer EveryTransition record

    log (EffectInterpreted Quit)

    Assert.Equal<LogEntry>([ EffectInterpreted Quit ], recorded)
