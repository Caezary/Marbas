module Marbas.Tests.State.TransferIntoItselfTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Domain
open Marbas.State
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private docs = fp (at "start/docs")

let private packInDocs = fp (at "start/docs/pack.zip")

let private tree () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/docs/sub/inner.txt", MockFileData "inner")
    fs.AddFile(at "start/docs-old/old.txt", MockFileData "old")
    fs

let private withArchiveInDocs () =
    let fs = tree ()

    Archive.write fs unattended (ArchiveFixture.request Zip (at "start/docs/pack.zip")) [ fp (at "start/docs-old") ]
    |> ignore

    fs

let private facing (fs: IFileSystem) (other: PanelLocation) =
    create (fp (at "start"))
    |> loadPanel fs Left (fp (at "start"))
    |> Expect.ok
    |> standInside fs Right other
    |> Expect.ok
    |> setCursor Left 1

let private onClipboard intent paths =
    ClipboardFiles.tryCreate intent paths |> Expect.some

let private refusedAt path =
    [ ReportOperationError(TargetWithinSource path) ]

[<Fact>]
let ``a copy into a directory under its source is refused by the target's name`` () =
    let fs = tree ()
    let state = facing fs (OnDisk(fp (at "start/docs/sub")))

    let result = update fs CopyRequested state

    Assert.Equal<Effect list>(refusedAt (fp (at "start/docs/sub")), result.Effects)

[<Fact>]
let ``a move into the other panel under its source is refused by the target's name`` () =
    let fs = tree ()
    let state = facing fs (OnDisk(fp (at "start/docs/sub")))

    let result = update fs (TargetEntered(at "start/docs/sub")) state

    Assert.Equal<Effect list>(refusedAt (fp (at "start/docs/sub")), result.Effects)

[<Fact>]
let ``a typed target under the source is refused by the place it names`` () =
    let fs = tree ()
    let state = facing fs (OnDisk(fp (at "start/docs-old")))

    let result = update fs (TargetEntered(Root.relative "docs/new")) state

    Assert.Equal<Effect list>(refusedAt (fp (at "start/docs/new")), result.Effects)

[<Fact>]
let ``pasting copied files into a directory under one of them is refused by the target's name`` () =
    let fs = tree ()
    let state = facing fs (OnDisk(fp (at "start/docs/sub")))
    let files = onClipboard ForCopying [ docs ]

    let result =
        update fs (ClipboardFilesRead(files, ToDirectory(fp (at "start/docs/sub")))) state

    Assert.Equal<Effect list>(refusedAt (fp (at "start/docs/sub")), result.Effects)

[<Fact>]
let ``pasting cut files into a directory under one of them is refused by the target's name`` () =
    let fs = tree ()
    let state = facing fs (OnDisk(fp (at "start/docs/sub")))
    let files = onClipboard ForMoving [ docs ]

    let result =
        update fs (ClipboardFilesRead(files, ToDirectory(fp (at "start/docs/sub")))) state

    Assert.Equal<Effect list>(refusedAt (fp (at "start/docs/sub")), result.Effects)

[<Fact>]
let ``a copy into an archive lying under its source is refused by the archive's name`` () =
    let fs = withArchiveInDocs ()
    let state = facing fs (InArchive(packInDocs, ArchivePath.root))

    let result = update fs CopyRequested state

    Assert.Equal<Effect list>(refusedAt packInDocs, result.Effects)

[<Fact>]
let ``a move into an archive lying under its source is refused by the archive's name`` () =
    let fs = withArchiveInDocs ()
    let state = facing fs (InArchive(packInDocs, ArchivePath.root))

    let result = update fs MoveRequested state

    Assert.Equal<Effect list>(refusedAt packInDocs, result.Effects)

[<Theory>]
[<InlineData("start")>]
[<InlineData("start/docs-old")>]
let ``a copy beside its source or into a name that merely begins with the source's goes ahead`` (other: string) =
    let fs = tree ()
    let state = facing fs (OnDisk(fp (at other)))

    let result = update fs CopyRequested state

    Assert.Equal<Effect list>([ CopyPlaces([ FromDisk [ docs ] ], ToDirectory(fp (at other))) ], result.Effects)

[<Fact>]
let ``a move into a name that merely begins with the source's goes ahead`` () =
    let fs = tree ()
    let state = facing fs (OnDisk(fp (at "start/docs-old")))

    let result = update fs (TargetEntered(at "start/docs-old")) state

    Assert.Equal<Effect list>(
        [ MovePlaces([ FromDisk [ docs ] ], ToDirectory(fp (at "start/docs-old"))) ],
        result.Effects
    )
