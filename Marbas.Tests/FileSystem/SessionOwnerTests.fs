module Marbas.Tests.FileSystem.SessionOwnerTests

open System
open Xunit
open Marbas.FileSystem

let private started =
    DateTimeOffset(2026, 10, 9, 8, 15, 0, 123, TimeSpan.FromHours 2.0)

[<Literal>]
let private StartedText = "2026-10-09T08:15:00.1230000+02:00"

[<Fact>]
let ``an owner is written as its process id and the moment it started`` () =
    let owner = { ProcessId = 4120; Started = started }

    let lines = SessionOwner.lines owner

    Assert.Equal<string list>([ "4120"; StartedText ], lines)

[<Fact>]
let ``an owner read back is the one that was written, to the tick`` () =
    let owner =
        { ProcessId = 4120
          Started = started.AddTicks 7L }

    let read = SessionOwner.lines owner |> SessionOwner.tryParse

    Assert.Equal(Some owner, read)

[<Theory>]
[<InlineData("four", StartedText)>]
[<InlineData("-4120", StartedText)>]
[<InlineData("", StartedText)>]
[<InlineData("4120", "yesterday")>]
[<InlineData("4120", "2026-10-09")>]
[<InlineData("4120", "")>]
let ``lines that do not hold a process id and a moment name no owner`` (processId: string) (moment: string) =
    let read = SessionOwner.tryParse [ processId; moment ]

    Assert.Equal(None, read)

[<Fact>]
let ``no lines name no owner`` () =
    let read = SessionOwner.tryParse []

    Assert.Equal(None, read)

[<Fact>]
let ``a process id without the moment names no owner`` () =
    let read = SessionOwner.tryParse [ "4120" ]

    Assert.Equal(None, read)

[<Fact>]
let ``a line after the moment names no owner`` () =
    let read = SessionOwner.tryParse [ "4120"; StartedText; "more" ]

    Assert.Equal(None, read)
