module Marbas.Tests.State.ClipboardLandingTests

open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Locations
open Marbas.State
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.ClipboardFixture

let private at = Root.at

let private fp = FileSystemPath.create

[<Fact>]
let ``going to the path on the clipboard reads it against the directory of the active panel`` () =
    let fs = mockFs ()
    let state = started fs |> loadPanel fs Left (fp (at "start/docs")) |> Expect.ok

    let result = update fs ClipboardPathRequested state

    Assert.Equal<Effect list>([ ReadClipboardPath(fp (at "start/docs")) ], result.Effects)

[<Fact>]
let ``inside an archive the path on the clipboard is read against the directory holding it`` () =
    let fs = ArchiveFixture.packed ()

    let state =
        ArchiveFixture.loaded fs
        |> standInside fs Left (InArchive(ArchiveFixture.archive, ArchivePath.root))
        |> Expect.ok

    let result = update fs ClipboardPathRequested state

    Assert.Equal<Effect list>([ ReadClipboardPath(fp (at "start")) ], result.Effects)

[<Fact>]
let ``a directory read from the clipboard is where the active panel goes`` () =
    let fs = mockFs ()

    let result = update fs (ClipboardPathRead(fp (at "start/a"))) (started fs)

    Assert.Equal(OnDisk(fp (at "start/a")), result.State.Left.Path)

[<Fact>]
let ``a file read from the clipboard puts the active panel beside it with the cursor on it`` () =
    let fs = mockFs ()

    let result = update fs (ClipboardPathRead(fp (at "start/b/two.txt"))) (started fs)

    let standing =
        PanelState.cursorEntry result.State.Left
        |> Option.map (_.Name >> EntryName.value)

    Assert.Equal(OnDisk(fp (at "start/b")), result.State.Left.Path)
    Assert.Equal(Some "two.txt", standing)

[<Fact>]
let ``the landing from the clipboard is remembered in the folder history`` () =
    let fs = mockFs ()

    let result = update fs (ClipboardPathRead(fp (at "start/a"))) (started fs)

    Assert.Contains(ChangeHistory(Remember(Visited(Place.OnDisk(fp (at "start/a"))))), result.Effects)

[<Fact>]
let ``a path read from the clipboard that does not exist changes nothing and says nothing`` () =
    let fs = mockFs ()
    let state = started fs

    let result = update fs (ClipboardPathRead(fp (at "start/gone"))) state

    Assert.Equal(state.Left.Path, result.State.Left.Path)
    Assert.Empty result.Effects

[<Fact>]
let ``a path to a level inside an archive read from the clipboard opens the archive there`` () =
    let fs = ArchiveFixture.packed ()

    let result =
        update fs (ClipboardPathRead(fp (at "start/backup.zip/docs"))) (ArchiveFixture.loaded fs)

    Assert.Equal(InArchive(ArchiveFixture.archive, docs), result.State.Left.Path)

[<Fact>]
let ``a path to an entry inside an archive read from the clipboard puts the cursor on it`` () =
    let fs = ArchiveFixture.packed ()

    let result =
        update fs (ClipboardPathRead(fp (at "start/backup.zip/docs/inner.txt"))) (ArchiveFixture.loaded fs)

    let standing =
        PanelState.cursorEntry result.State.Left
        |> Option.map (_.Name >> EntryName.value)

    Assert.Equal(InArchive(ArchiveFixture.archive, docs), result.State.Left.Path)
    Assert.Equal(Some "inner.txt", standing)

[<Fact>]
let ``a level inside an archive reached from the clipboard is remembered in the folder history`` () =
    let fs = ArchiveFixture.packed ()

    let result =
        update fs (ClipboardPathRead(fp (at "start/backup.zip/docs"))) (ArchiveFixture.loaded fs)

    Assert.Contains(ChangeHistory(Remember(Visited(Place.InArchive(ArchiveFixture.archive, docs)))), result.Effects)

[<Fact>]
let ``a path into an archive that does not hold it changes nothing and says nothing`` () =
    let fs = ArchiveFixture.packed ()
    let state = ArchiveFixture.loaded fs

    let result =
        update fs (ClipboardPathRead(fp (at "start/backup.zip/docs/gone.txt"))) state

    Assert.Equal(state.Left.Path, result.State.Left.Path)
    Assert.Empty result.Effects
