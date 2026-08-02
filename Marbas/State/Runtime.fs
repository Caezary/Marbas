module Marbas.State.Runtime

type Runtime<'state, 'msg, 'effect>
    (
        initial: 'state,
        update: 'msg -> 'state -> 'state * 'effect list,
        interpret: ('msg -> unit) -> 'effect -> unit,
        render: 'state -> unit
    ) =
    let mutable state = initial

    member _.State = state

    member this.Dispatch(msg: 'msg) =
        let next, effects = update msg state
        state <- next
        render state
        effects |> List.iter (interpret this.Dispatch)

    member _.Refresh() = render state
