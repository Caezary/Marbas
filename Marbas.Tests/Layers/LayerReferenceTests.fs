module Marbas.Tests.Layers.LayerReferenceTests

open System
open Xunit
open Marbas.Tests.Layers

[<Fact>]
let ``every type of Marbas is in a layer of the table`` () =
    let strays = LayerReferences.outsideEveryLayer ()

    Assert.True(List.isEmpty strays, String.Join(Environment.NewLine, strays))

[<Fact>]
let ``every layer uses only itself and the layers its row allows`` () =
    let uses = LayerReferences.forbidden ()

    Assert.True(List.isEmpty uses, String.Join(Environment.NewLine, uses))

[<Fact>]
let ``every layer of the table holds a type`` () =
    let layers = LayerReferences.withoutTypes ()

    Assert.True(List.isEmpty layers, String.Join(Environment.NewLine, layers))

[<Fact>]
let ``the scan sees the layers use the ones below them`` () =
    let uses = LayerReferences.acrossLayers ()

    Assert.NotEmpty uses
