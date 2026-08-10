module Marbas.Tests.RuntimeTests

open Xunit
open Marbas.State.Runtime

type private Msg =
    | Bump
    | Cascade

type private Effect = FollowUp

let private step msg state =
    match msg with
    | Bump -> state + 1, []
    | Cascade -> state + 10, [ FollowUp ]

[<Fact>]
let ``dispatch applies the update and renders the new state`` () =
    let renders = ResizeArray<int>()
    let runtime = Runtime(0, step, (fun _ _ -> ()), renders.Add)

    runtime.Dispatch Bump

    Assert.Equal(1, runtime.State)
    Assert.Equal<int list>([ 1 ], List.ofSeq renders)

[<Fact>]
let ``effects are interpreted after the render and can dispatch a follow-up`` () =
    let renders = ResizeArray<int>()
    let interpret dispatch FollowUp = dispatch Bump
    let runtime = Runtime(0, step, interpret, renders.Add)

    runtime.Dispatch Cascade

    Assert.Equal(11, runtime.State)
    Assert.Equal<int list>([ 10; 11 ], List.ofSeq renders)

[<Fact>]
let ``a runtime is not rendering before anything is dispatched`` () =
    let runtime = Runtime(0, step, (fun _ _ -> ()), ignore)

    let rendering = runtime.IsRendering

    Assert.False rendering

[<Fact>]
let ``rendering is observable only while the render callback runs`` () =
    let observed = ResizeArray<bool>()
    let runtimeRef = ref Unchecked.defaultof<Runtime<int, Msg, Effect>>
    let render _ = observed.Add runtimeRef.Value.IsRendering
    let runtime = Runtime(0, step, (fun _ _ -> ()), render)
    runtimeRef.Value <- runtime

    runtime.Dispatch Bump

    Assert.Equal<bool list>([ true ], List.ofSeq observed)
    Assert.False runtime.IsRendering

[<Fact>]
let ``refresh re-renders the current state without changing it`` () =
    let renders = ResizeArray<int>()
    let runtime = Runtime(7, step, (fun _ _ -> ()), renders.Add)

    runtime.Refresh()

    Assert.Equal(7, runtime.State)
    Assert.Equal<int list>([ 7 ], List.ofSeq renders)
