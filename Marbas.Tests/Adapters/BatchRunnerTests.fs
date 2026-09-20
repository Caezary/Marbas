module Marbas.Tests.Adapters.BatchRunnerTests

open System
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Adapters
open Marbas.Tests

[<Fact>]
let ``a measurement that finishes is answered as it stands`` () =
    let work _ =
        [ { Name = EntryName.create "docs"
            Size = Exact 5L } ]

    let measured = BatchRunner.measured work Operations.unattended

    Assert.Equal<Measurement list>(
        [ { Name = EntryName.create "docs"
            Size = Exact 5L } ],
        Expect.ok measured
    )

[<Fact>]
let ``a measurement that throws answers the failure instead of an empty one`` () =
    let work _ : Measurement list =
        raise (InvalidOperationException "the walk fell over")

    let measured = BatchRunner.measured work Operations.unattended

    Assert.Equal(Operations.BatchFailed "the walk fell over", Expect.error measured)
