module Marbas.Tests.Messages.LinkRefusalMessageTests

open Xunit
open Marbas.Domain
open Marbas.Messages

[<Fact>]
let ``a hard link to a directory says it wants a file`` () =
    let message = LinkRefusalMessage.describe HardLinkToDirectory

    Assert.Equal("A hard link can only point at a file.", message)

[<Fact>]
let ``a hard link across volumes says where it must stand`` () =
    let message = LinkRefusalMessage.describe HardLinkAcrossVolumes

    Assert.Equal("A hard link must stand on the volume of its file.", message)

[<Fact>]
let ``a junction to a file says it wants a directory`` () =
    let message = LinkRefusalMessage.describe JunctionToFile

    Assert.Equal("A junction can only point at a directory.", message)

[<Fact>]
let ``a kind the system lacks says so`` () =
    let message = LinkRefusalMessage.describe KindUnavailable

    Assert.Equal("This system has no way to make a link of this kind.", message)
