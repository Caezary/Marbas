module Marbas.Tests.Adapters.WindowSearchRunnerTests

open Xunit
open Marbas.Adapters

let private describe (offset: int64) = $"{offset / 100L}"

let private queuing (pending: ResizeArray<unit -> unit>) action = pending.Add action

[<Fact>]
let ``a place reached while the search runs reaches the dialog`` () =
    let pending = ResizeArray<unit -> unit>()
    let shown = ResizeArray<string>()

    let control =
        WindowSearchRunner.control (queuing pending) shown.Add describe (fun () -> false)

    control.Reached 250L

    pending |> Seq.iter (fun action -> action ())

    Assert.Equal<string list>([ "2" ], List.ofSeq shown)

[<Fact>]
let ``a place that reads as the last one is not queued again`` () =
    let pending = ResizeArray<unit -> unit>()

    let control =
        WindowSearchRunner.control (queuing pending) ignore describe (fun () -> false)

    control.Reached 210L

    control.Reached 290L

    Assert.Single pending |> ignore

[<Fact>]
let ``a place still queued when the search is stopped never reaches the closed dialog`` () =
    let pending = ResizeArray<unit -> unit>()
    let cancelled = ref false
    let shown = ResizeArray<string>()

    let control =
        WindowSearchRunner.control (queuing pending) shown.Add describe (fun () -> cancelled.Value)

    control.Reached 250L
    cancelled.Value <- true

    pending |> Seq.iter (fun action -> action ())

    Assert.Empty shown

[<Fact>]
let ``a place reached after the search is stopped is not queued`` () =
    let pending = ResizeArray<unit -> unit>()

    let control =
        WindowSearchRunner.control (queuing pending) ignore describe (fun () -> true)

    control.Reached 250L

    Assert.Empty pending
