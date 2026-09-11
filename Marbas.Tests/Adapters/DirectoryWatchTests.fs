module Marbas.Tests.Adapters.DirectoryWatchTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Adapters
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private en = EntryName.create

let private quiet = TimeSpan.FromMilliseconds 200.0

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/backup.zip", MockFileData "zip")
    fs

type private Watch() =
    let armed = ResizeArray<unit -> bool>()
    let reports = ResizeArray<int>()
    let mutable now = DateTimeOffset(2026, 9, 11, 12, 0, 0, TimeSpan.Zero)
    let mutable inFront = false

    member _.Armed = armed

    member _.Reports = reports

    member _.Pass(span: TimeSpan) = now <- now + span

    member _.Cover() = inFront <- true

    member _.Uncover() = inFront <- false

    member _.Collapsing() =
        DirectoryWatch.collapsing (fun () -> now) quiet armed.Add (fun () -> not inFront) (fun () -> reports.Add 1)

[<Fact>]
let ``a directory is watched whole`` () =
    let fs = mockFs ()

    let target = DirectoryWatch.target fs (fp (at "start/docs"))

    Assert.Equal(DirectoryWatch.EveryEntryIn(fp (at "start/docs")), target)

[<Fact>]
let ``a file is watched by its name in the directory holding it`` () =
    let fs = mockFs ()

    let target = DirectoryWatch.target fs (fp (at "start/backup.zip"))

    Assert.Equal(DirectoryWatch.OneEntryIn(fp (at "start"), en "backup.zip"), target)

[<Fact>]
let ``a path that is not there is watched by its name as well`` () =
    let fs = mockFs ()

    let target = DirectoryWatch.target fs (fp (at "start/gone"))

    Assert.Equal(DirectoryWatch.OneEntryIn(fp (at "start"), en "gone"), target)

[<Fact>]
let ``a burst of changes arms the watch once`` () =
    let watch = Watch()
    let touched = watch.Collapsing()

    touched ()
    touched ()
    touched ()

    Assert.Single watch.Armed |> ignore
    Assert.Empty watch.Reports

[<Fact>]
let ``a burst that has gone quiet is reported once`` () =
    let watch = Watch()
    let touched = watch.Collapsing()
    touched ()
    touched ()
    watch.Pass quiet

    let again = watch.Armed[0] ()

    Assert.Single watch.Reports |> ignore
    Assert.False again

[<Fact>]
let ``a burst still running is left to run`` () =
    let watch = Watch()
    let touched = watch.Collapsing()
    touched ()
    watch.Pass(TimeSpan.FromMilliseconds 100.0)

    let again = watch.Armed[0] ()

    Assert.Empty watch.Reports
    Assert.True again

[<Fact>]
let ``a change after a report arms the watch again`` () =
    let watch = Watch()
    let touched = watch.Collapsing()
    touched ()
    watch.Pass quiet
    watch.Armed[0] () |> ignore

    touched ()

    Assert.Equal(2, watch.Armed.Count)

[<Fact>]
let ``the paths that are there are the ones standing`` () =
    let fs = mockFs ()

    let standing =
        DirectoryWatch.standing fs [ fp (at "start/docs"); fp (at "start/backup.zip") ]

    Assert.Equal<Set<FileSystemPath>>(
        Set.ofList [ fp (at "start/docs"); fp (at "start/backup.zip") ],
        standing
    )

[<Fact>]
let ``a path that is gone is not standing`` () =
    let fs = mockFs ()

    let standing = DirectoryWatch.standing fs [ fp (at "start/docs"); fp (at "start/gone") ]

    Assert.Equal<Set<FileSystemPath>>(Set.singleton (fp (at "start/docs")), standing)

[<Fact>]
let ``a change while something is in front is held back`` () =
    let watch = Watch()
    let touched = watch.Collapsing()
    watch.Cover()
    touched ()
    watch.Pass quiet

    let again = watch.Armed[0] ()

    Assert.Empty watch.Reports
    Assert.True again

[<Fact>]
let ``what was held becomes one re-read once the screen is the panels' own`` () =
    let watch = Watch()
    let touched = watch.Collapsing()
    watch.Cover()
    touched ()
    touched ()
    watch.Pass quiet
    watch.Armed[0] () |> ignore
    watch.Uncover()

    let again = watch.Armed[0] ()

    Assert.Single watch.Reports |> ignore
    Assert.False again
