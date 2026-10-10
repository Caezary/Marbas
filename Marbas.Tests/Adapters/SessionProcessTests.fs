module Marbas.Tests.Adapters.SessionProcessTests

open System
open Xunit
open Marbas.FileSystem
open Marbas.Adapters

let private started = DateTimeOffset(2026, 10, 9, 8, 15, 0, TimeSpan.FromHours 2.0)

[<Literal>]
let private IdNoProcessHas = Int32.MaxValue

[<Fact>]
let ``the process of the session owns it from the moment it started`` () =
    let owner = SessionProcess.current ()

    Assert.Equal(Environment.ProcessId, owner.ProcessId)
    Assert.InRange(owner.Started, DateTimeOffset.Now.AddDays -1.0, DateTimeOffset.Now)

[<Fact>]
let ``the owner of a session that runs is running`` () =
    let owner = SessionProcess.current ()

    let running = SessionProcess.isRunning owner

    Assert.True running

[<Fact>]
let ``a process with the id of the owner but started at another moment is not the owner`` () =
    let owner = SessionProcess.current ()

    let earlier =
        { owner with
            Started = owner.Started.AddMinutes -1.0 }

    let running = SessionProcess.isRunning earlier

    Assert.False running

[<Fact>]
let ``an owner whose process id nothing has is not running`` () =
    let owner =
        { ProcessId = IdNoProcessHas
          Started = started }

    let running = SessionProcess.isRunning owner

    Assert.False running

[<Theory>]
[<InlineData 0.0>]
[<InlineData 0.004>]
[<InlineData -1.5>]
[<InlineData 2.0>]
let ``a start read within the tolerance of the one written is the same start`` (secondsApart: float) =
    let same = SessionProcess.sameStart started (started.AddSeconds secondsApart)

    Assert.True same

[<Theory>]
[<InlineData 2.5>]
[<InlineData -60.0>]
let ``a start read further from the one written is another start`` (secondsApart: float) =
    let same = SessionProcess.sameStart started (started.AddSeconds secondsApart)

    Assert.False same
