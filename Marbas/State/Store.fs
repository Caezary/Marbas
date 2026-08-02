module Marbas.State.Store

type Store<'state>(initial: 'state, onChanged: 'state -> unit) =
    let mutable state = initial

    member _.State = state

    member _.Put(next: 'state) =
        state <- next
        onChanged state

    member _.Refresh() = onChanged state
