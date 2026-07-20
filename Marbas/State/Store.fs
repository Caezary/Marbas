module Marbas.State.Store

type Store<'state>(initial: 'state, onChanged: 'state -> unit) =
    let mutable state = initial

    member _.State = state

    member _.Dispatch(transition: 'state -> 'state) =
        state <- transition state
        onChanged state

    member this.DispatchResult (onError: 'error -> unit) (transition: 'state -> Result<'state, 'error>) =
        match transition state with
        | Ok updated ->
            state <- updated
            onChanged state
        | Error err -> onError err

    member _.Refresh() = onChanged state
