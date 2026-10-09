module Marbas.Tests.Adapters.BatchRunnerTests

open System
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.State
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

let private later = FileSystemPath.create (Root.at "start/todo.txt")

[<Fact>]
let ``a report queues nothing, it overwrites the one path waiting to be shown`` () =
    let latest = ref None
    let control = BatchRunner.control latest (fun () -> false)

    control.Report path
    control.Report later

    Assert.Equal(Some later, latest.Value)

[<Fact>]
let ``a tick shows the latest path reported since the one before`` () =
    let latest = ref None
    let shown = ResizeArray<FileSystemPath>()
    let control = BatchRunner.control latest (fun () -> false)
    control.Report path
    control.Report later

    BatchRunner.showLatest latest shown.Add

    Assert.Equal<FileSystemPath list>([ later ], List.ofSeq shown)

[<Fact>]
let ``a tick with nothing reported since the one before shows nothing`` () =
    let latest = ref None
    let shown = ResizeArray<FileSystemPath>()
    let control = BatchRunner.control latest (fun () -> false)
    control.Report path
    BatchRunner.showLatest latest shown.Add

    BatchRunner.showLatest latest shown.Add

    Assert.Equal<FileSystemPath list>([ path ], List.ofSeq shown)

[<Fact>]
let ``the control answers whether the batch was cancelled`` () =
    let control = BatchRunner.control (ref None) (fun () -> true)

    let cancelled = control.IsCancelled()

    Assert.True cancelled

[<Fact>]
let ``every report of a batch is counted`` () =
    let counted = ref 0

    let control =
        BatchRunner.counting (fun () -> counted.Value <- counted.Value + 1) Operations.unattended

    control.Report path
    control.Report path

    Assert.Equal(2, counted.Value)

[<Fact>]
let ``a batch that ran to its end concludes as finished`` () =
    let entry = BatchRunner.concluded "Copying" (fun () -> false) 12

    Assert.Equal(BatchFinished("Copying", 12), entry)

[<Fact>]
let ``a cancelled batch concludes as cancelled, with what it reported before it stopped`` () =
    let entry = BatchRunner.concluded "Copying" (fun () -> true) 3

    Assert.Equal(BatchCancelled("Copying", 3), entry)
