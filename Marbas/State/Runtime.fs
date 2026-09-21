module Marbas.State.Runtime

open System.Collections.Generic

type Runtime<'state, 'msg, 'effect>
    (
        initial: 'state,
        update: 'msg -> 'state -> 'state * 'effect list,
        interpret: ('msg -> unit) -> 'effect -> unit,
        render: 'state -> unit
    ) =
    let pending = Queue<'msg>()
    let mutable state = initial
    let mutable rendering = false
    let mutable draining = false

    let paint () =
        rendering <- true
        render state
        rendering <- false

    member _.State = state

    member _.IsRendering = rendering

    member this.Dispatch(msg: 'msg) =
        pending.Enqueue msg

        if not draining then
            draining <- true

            try
                while pending.Count > 0 do
                    let next, effects = update (pending.Dequeue()) state
                    state <- next
                    paint ()
                    effects |> List.iter (interpret this.Dispatch)
            finally
                draining <- false

    member _.Refresh() = paint ()
