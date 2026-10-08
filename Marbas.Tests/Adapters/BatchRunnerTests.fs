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

let private path = FileSystemPath.create (Root.at "start/notes.txt")

[<Fact>]
let ``a report made while the batch runs reaches the dialog`` () =
    let pending = ref ignore
    let shown = ResizeArray<FileSystemPath>()

    let control =
        BatchRunner.control (fun action -> pending.Value <- action) shown.Add (fun () -> false)

    control.Report path

    pending.Value()

    Assert.Equal<FileSystemPath list>([ path ], List.ofSeq shown)

[<Fact>]
let ``a report still queued when the batch is cancelled never reaches the closed dialog`` () =
    let pending = ref ignore
    let cancelled = ref false
    let shown = ResizeArray<FileSystemPath>()

    let control =
        BatchRunner.control (fun action -> pending.Value <- action) shown.Add (fun () -> cancelled.Value)

    control.Report path
    cancelled.Value <- true

    pending.Value()

    Assert.Empty shown

[<Fact>]
let ``a report made after the batch is cancelled is not queued`` () =
    let queued = ResizeArray<unit -> unit>()
    let control = BatchRunner.control queued.Add ignore (fun () -> true)

    control.Report path

    Assert.Empty queued
