module Marbas.Tests.Adapters.NativeLinksTests

open Xunit
open Marbas
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Adapters

[<Fact>]
let ``Windows offers every kind of link`` () =
    let kinds = Links.available (NativeLinks.forPlatform Windows)

    Assert.Equal<LinkKind list>([ Symbolic; Hard; Junction ], kinds)

[<Fact>]
let ``Unix has no junction to offer`` () =
    let kinds = Links.available (NativeLinks.forPlatform Unix)

    Assert.Equal<LinkKind list>([ Symbolic; Hard ], kinds)
