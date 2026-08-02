module Marbas.State.Runtime

type Runtime<'state, 'msg, 'effect>
    (
        initial: 'state,
        update: 'msg -> 'state -> 'state * 'effect list,
        interpret: ('msg -> unit) -> 'effect -> unit,
        render: 'state -> unit
    ) =
    let mutable state = initial
    let mutable rendering = false

    let paint () =
        rendering <- true
        render state
        rendering <- false

    member _.State = state

    member _.IsRendering = rendering

    member this.Dispatch(msg: 'msg) =
        let next, effects = update msg state
        state <- next
        paint ()
        effects |> List.iter (interpret this.Dispatch)

    member _.Refresh() = paint ()
