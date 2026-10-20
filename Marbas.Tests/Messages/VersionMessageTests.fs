module Marbas.Tests.Messages.VersionMessageTests

open Xunit
open Marbas.Domain
open Marbas.Messages

[<Theory>]
[<InlineData("0.1.0")>]
[<InlineData("0.2.0-alpha.0.3")>]
let ``the product is named with its version`` (version: string) =
    let line = VersionMessage.product (ProductVersion version)

    Assert.Equal($"Marbas {version}", line)
