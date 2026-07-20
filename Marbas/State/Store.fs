module Marbas.State.Store

/// Holds the single mutable cell of the application: the current AppState.
/// Handlers dispatch pure transitions and the store re-renders after each one.
/// Terminal.Gui runs a single-threaded event loop, so no synchronisation is needed;
/// background work must marshal its dispatch back onto that thread.
type Store<'state>(initial: 'state, onChanged: 'state -> unit) =
    let mutable state = initial

    member _.State = state

    /// Applies a pure transition and re-renders.
    member _.Dispatch(transition: 'state -> 'state) =
        state <- transition state
        onChanged state

    /// Applies a transition that may fail; on error the state is left untouched
    /// and the error is handed to `onError`.
    member this.DispatchResult (onError: 'error -> unit) (transition: 'state -> Result<'state, 'error>) =
        match transition state with
        | Ok updated ->
            state <- updated
            onChanged state
        | Error err -> onError err

    /// Re-renders without changing the state.
    member _.Refresh() = onChanged state
