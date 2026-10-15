module Marbas.Tests.Layers.SystemCallTests

open System
open Xunit
open Marbas.Tests.Layers

[<Fact>]
let ``nothing outside Adapters and Program reaches the system directly`` () =
    let calls = SystemCalls.outsideAdapters ()

    Assert.True(List.isEmpty calls, String.Join(Environment.NewLine, calls))

[<Fact>]
let ``the scan sees the calls Adapters makes to the system`` () =
    let calls = SystemCalls.inAdapters ()

    Assert.NotEmpty calls
