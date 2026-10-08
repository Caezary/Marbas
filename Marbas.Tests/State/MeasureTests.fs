module Marbas.Tests.State.MeasureTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.State
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.ArchiveFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private en = EntryName.create

let private inside raw =
    ArchivePath.tryCreate raw |> Expect.some

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/docs/inner.txt", MockFileData "12345")
    fs.AddDirectory(at "start/empty")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddDirectory(at "backup")
    fs

let private loaded (fs: IFileSystem) =
    create (fp (at "start"))
    |> loadPanel fs Left (fp (at "start"))
    |> Expect.ok
    |> loadPanel fs Right (fp (at "backup"))
    |> Expect.ok

let private measurementOf name (panel: PanelState) =
    panel.Entries |> Array.find (fun entry -> entry.Name = en name) |> _.Measured

[<Fact>]
let ``the view key on a directory asks for it to be measured`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 1

    let result = update fs ViewRequested state

    Assert.Equal<Effect list>(
        [ MeasureTargets
              [ { Name = en "docs"
                  Location = OnDisk(fp (at "start/docs")) } ] ],
        result.Effects
    )

[<Fact>]
let ``the view key on the parent entry measures the directory it leads to`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 0

    let result = update fs ViewRequested state

    Assert.Equal<Effect list>(
        [ MeasureTargets
              [ { Name = DirectoryListing.ParentName
                  Location = OnDisk(fp (at "")) } ] ],
        result.Effects
    )

[<Fact>]
let ``the view key on a file still opens the viewer`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 3

    let result = update fs ViewRequested state

    Assert.Equal<Effect list>(
        [ ChangeHistory(
              Remember(
                  Opened
                      { Where = Place.OnDisk(fp (at "start/notes.txt"))
                        How = Viewed }
              )
          )
          ViewFile(fp (at "start/notes.txt")) ],
        result.Effects
    )

[<Fact>]
let ``every marked directory is measured, the one under the cursor left alone`` () =
    let fs = mockFs ()

    let state =
        loaded fs
        |> setCursor Left 1
        |> toggleSelection Left
        |> setCursor Left 2
        |> toggleSelection Left
        |> setCursor Left 0

    let result = update fs ViewRequested state

    Assert.Equal<Effect list>(
        [ MeasureTargets
              [ { Name = en "docs"
                  Location = OnDisk(fp (at "start/docs")) }
                { Name = en "empty"
                  Location = OnDisk(fp (at "start/empty")) } ] ],
        result.Effects
    )

[<Fact>]
let ``a marked file is not measured`` () =
    let fs = mockFs ()

    let state =
        loaded fs
        |> setCursor Left 1
        |> toggleSelection Left
        |> setCursor Left 3
        |> toggleSelection Left
        |> setCursor Left 0

    let result = update fs ViewRequested state

    Assert.Equal<Effect list>(
        [ MeasureTargets
              [ { Name = en "docs"
                  Location = OnDisk(fp (at "start/docs")) } ] ],
        result.Effects
    )

[<Fact>]
let ``a directory inside an archive is measured at its level`` () =
    let fs = packed ()
    let state = standingInside fs Left |> setCursor Left 1

    let result = update fs ViewRequested state

    Assert.Equal<Effect list>(
        [ MeasureTargets
              [ { Name = en "docs"
                  Location = InArchive(archive, inside "docs") } ] ],
        result.Effects
    )

[<Fact>]
let ``the parent entry of an archive root measures the directory holding it`` () =
    let fs = packed ()
    let state = standingInside fs Left |> setCursor Left 0

    let result = update fs ViewRequested state

    Assert.Equal<Effect list>(
        [ MeasureTargets
              [ { Name = DirectoryListing.ParentName
                  Location = OnDisk(fp (at "start")) } ] ],
        result.Effects
    )

[<Fact>]
let ``a measurement lands on the entry it was taken for`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result =
        update fs (MeasureFinished(Ok [ { Name = en "docs"; Size = Exact 5L } ])) state

    Assert.Equal(Some(Exact 5L), measurementOf "docs" result.State.Left)
    Assert.Equal(None, measurementOf "empty" result.State.Left)
    Assert.Empty result.Effects

[<Fact>]
let ``a measurement stopped part way is kept as the lower bound it is`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result =
        update fs (MeasureFinished(Ok [ { Name = en "docs"; Size = AtLeast 5L } ])) state

    Assert.Equal(Some(AtLeast 5L), measurementOf "docs" result.State.Left)

[<Fact>]
let ``a measurement taken while the panel sorts by size moves the row into place`` () =
    let fs = mockFs ()

    let state = loaded fs |> sortBy SortKey.BySize Left |> setCursor Left 1

    let result =
        update fs (MeasureFinished(Ok [ { Name = en "docs"; Size = Exact 5L } ])) state

    let order =
        result.State.Left.Entries |> Seq.map (_.Name >> EntryName.value) |> List.ofSeq

    Assert.Equal<string list>([ ".."; "empty"; "docs"; "notes.txt" ], order)
    Assert.Equal(2, result.State.Left.Cursor)

[<Fact>]
let ``a measurement changes no order in a panel sorted by name`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 1

    let result =
        update fs (MeasureFinished(Ok [ { Name = en "docs"; Size = Exact 5L } ])) state

    let order =
        result.State.Left.Entries |> Seq.map (_.Name >> EntryName.value) |> List.ofSeq

    Assert.Equal<string list>([ ".."; "docs"; "empty"; "notes.txt" ], order)
    Assert.Equal(1, result.State.Left.Cursor)

[<Fact>]
let ``a re-read of the panel forgets what was measured`` () =
    let fs = mockFs ()

    let measured =
        loaded fs
        |> update fs (MeasureFinished(Ok [ { Name = en "docs"; Size = Exact 5L } ]))
        |> _.State

    let result = update fs RefreshRequested measured

    Assert.Equal(None, measurementOf "docs" result.State.Left)

[<Fact>]
let ``a measurement that fell over is reported instead of emptying the sizes`` () =
    let fs = mockFs ()
    let state = loaded fs
    let failure = Operations.BatchFailed "the walk fell over"

    let result = update fs (MeasureFinished(Error failure)) state

    Assert.Equal<Effect list>([ ReportOperationError failure ], result.Effects)
    Assert.Equal(None, measurementOf "docs" result.State.Left)
