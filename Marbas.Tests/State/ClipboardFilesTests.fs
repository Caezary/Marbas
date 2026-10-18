module Marbas.Tests.State.ClipboardFilesTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Locations
open Marbas.Messages
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.ClipboardFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private handing intent paths =
    [ PutFilesOnClipboard(ClipboardFiles.tryCreate intent (paths |> List.map (at >> fp)) |> Expect.some) ]

let private insideArchiveRoot (fs: IFileSystem) =
    ArchiveFixture.loaded fs
    |> standInside fs Left (InArchive(ArchiveFixture.archive, ArchivePath.root))
    |> Expect.ok

let private entry raw =
    ArchivePath.tryCreate raw |> Expect.some

let private mixedResults (fs: MockFileSystem) =
    fs.AddFile(at "start/a/one.txt", MockFileData "1")

    let insideArchive =
        { Location = InArchive(ArchiveFixture.archive, docs)
          Entry = file "inner.txt" }

    ArchiveFixture.loaded fs
    |> update fs (sentToPanel [ hitIn "start/a" "one.txt"; insideArchive ])
    |> _.State
    |> update fs (PatternEntered(Select, everything))
    |> _.State

let private extracted (archive: FileSystemPath) raw : Operations.ItemResult =
    { Source = Operations.ItemInArchive(archive, entry raw)
      Outcome = Ok() }

[<Fact>]
let ``the file under the cursor is put on the clipboard to be copied`` () =
    let fs = mockFs ()
    let state = started fs |> standingOn "notes.txt"

    let result = update fs (FilesOnClipboardRequested ForCopying) state

    Assert.Equal<Effect list>(handing ForCopying [ "start/notes.txt" ], result.Effects)

[<Fact>]
let ``the marked entries are put on the clipboard to be moved, in the order of the listing`` () =
    let fs = mockFs ()

    let state =
        started fs |> markedOn [ "readme.txt"; "docs" ] |> standingOn "notes.txt"

    let result = update fs (FilesOnClipboardRequested ForMoving) state

    Assert.Equal<Effect list>(handing ForMoving [ "start/docs"; "start/readme.txt" ], result.Effects)

[<Fact>]
let ``no files are put on the clipboard with the parent entry under the cursor and nothing marked`` () =
    let fs = mockFs ()
    let state = started fs |> standingOn ".."

    let result = update fs (FilesOnClipboardRequested ForCopying) state

    Assert.Empty result.Effects

[<Fact>]
let ``each hit in a panel of search results puts its own file on the clipboard`` () =
    let fs = mockFs ()

    let state =
        started fs
        |> update fs (sentToPanel [ hitIn "start/a" "one.txt"; hitIn "start/b" "two.txt" ])
        |> _.State
        |> update fs (PatternEntered(Select, everything))
        |> _.State

    let result = update fs (FilesOnClipboardRequested ForMoving) state

    Assert.Equal<Effect list>(handing ForMoving [ "start/a/one.txt"; "start/b/two.txt" ], result.Effects)

[<Fact>]
let ``entries inside an archive are extracted before they are put on the clipboard`` () =
    let fs = ArchiveFixture.packed ()

    let state =
        insideArchiveRoot fs
        |> update fs (PatternEntered(Select, everything))
        |> _.State

    let result = update fs (FilesOnClipboardRequested ForCopying) state

    Assert.Equal<Effect list>(
        [ ExtractForClipboard [ FromArchive(ArchiveFixture.archive, [ entry "docs"; entry "notes.txt" ]) ] ],
        result.Effects
    )

[<Fact>]
let ``cutting from inside an archive is refused by the name of the entry`` () =
    let fs = ArchiveFixture.packed ()
    let state = insideArchiveRoot fs |> standingOn "notes.txt"

    let result = update fs (FilesOnClipboardRequested ForMoving) state

    Assert.Equal<Effect list>(
        [ Report(Problem.Operation(Operations.ArchiveEntryNotCut(fp (at "start/backup.zip/notes.txt")))) ],
        result.Effects
    )

[<Fact>]
let ``search results holding a hit inside an archive are extracted beside the files on disk`` () =
    let fs = ArchiveFixture.packed ()
    let state = mixedResults fs

    let result = update fs (FilesOnClipboardRequested ForCopying) state

    Assert.Equal<Effect list>(
        [ ExtractForClipboard
              [ FromDisk [ fp (at "start/a/one.txt") ]
                FromArchive(ArchiveFixture.archive, [ entry "docs/inner.txt" ]) ] ],
        result.Effects
    )

[<Fact>]
let ``cutting search results that hold a hit inside an archive is refused by its name`` () =
    let fs = ArchiveFixture.packed ()
    let state = mixedResults fs

    let result = update fs (FilesOnClipboardRequested ForMoving) state

    Assert.Equal<Effect list>(
        [ Report(Problem.Operation(Operations.ArchiveEntryNotCut(fp (at "start/backup.zip/docs/inner.txt")))) ],
        result.Effects
    )

[<Fact>]
let ``the entries extracted for the clipboard are put on it after the files on disk`` () =
    let fs = ArchiveFixture.packed ()
    let archive = ArchiveFixture.archive

    let finished =
        ClipboardExtractFinished(
            [ FromDisk [ fp (at "start/a/one.txt") ]
              FromArchive(archive, [ entry "docs/inner.txt"; entry "docs/deep" ]) ],
            fp (at "temp/1"),
            [ extracted archive "docs/inner.txt"; extracted archive "docs/deep" ]
        )

    let result = update fs finished (ArchiveFixture.loaded fs)

    Assert.Equal<Effect list>(
        handing ForCopying [ "start/a/one.txt"; "temp/1/inner.txt"; "temp/1/deep" ],
        result.Effects
    )

[<Fact>]
let ``what could not be extracted for the clipboard is reported and the rest is put on it`` () =
    let fs = ArchiveFixture.packed ()
    let archive = ArchiveFixture.archive

    let failed: Operations.ItemResult =
        { Source = Operations.ItemInArchive(archive, entry "docs")
          Outcome = Error(Operations.SourceNotFound(fp (at "start/backup.zip/docs"))) }

    let finished =
        ClipboardExtractFinished(
            [ FromArchive(archive, [ entry "notes.txt"; entry "docs" ]) ],
            fp (at "temp/1"),
            [ extracted archive "notes.txt"; failed ]
        )

    let result = update fs finished (ArchiveFixture.loaded fs)

    Assert.Equal<Effect list>(
        handing ForCopying [ "temp/1/notes.txt" ]
        @ [ Report(Problem.ItemFailures [ failed ]) ],
        result.Effects
    )

[<Fact>]
let ``nothing extracted and nothing on disk puts nothing on the clipboard`` () =
    let fs = ArchiveFixture.packed ()

    let finished =
        ClipboardExtractFinished([ FromArchive(ArchiveFixture.archive, [ entry "notes.txt" ]) ], fp (at "temp/1"), [])

    let result = update fs finished (ArchiveFixture.loaded fs)

    Assert.Empty result.Effects

let private onClipboard intent paths =
    ClipboardFiles.tryCreate intent (paths |> List.map (at >> fp)) |> Expect.some

[<Fact>]
let ``pasting asks the clipboard for files to land in the directory of the active panel`` () =
    let fs = mockFs ()
    let state = started fs |> loadPanel fs Left (fp (at "start/docs")) |> Expect.ok

    let result = update fs FilesPasteRequested state

    Assert.Equal<Effect list>([ ReadClipboardFiles(ToDirectory(fp (at "start/docs"))) ], result.Effects)

[<Fact>]
let ``pasting inside an archive asks for files to land at its level`` () =
    let fs = ArchiveFixture.packed ()

    let state =
        ArchiveFixture.loaded fs
        |> standInside fs Left (InArchive(ArchiveFixture.archive, docs))
        |> Expect.ok

    let result = update fs FilesPasteRequested state

    Assert.Equal<Effect list>([ ReadClipboardFiles(ToArchive(ArchiveFixture.archive, docs)) ], result.Effects)

[<Fact>]
let ``pasting inside an archive of a kind that cannot be changed is refused by name`` () =
    let fs = ArchiveFixture.tarredAs (at "start/backup.tar")
    let tarball = fp (at "start/backup.tar")

    let state =
        ArchiveFixture.loaded fs
        |> standInside fs Left (InArchive(tarball, ArchivePath.root))
        |> Expect.ok

    let result = update fs FilesPasteRequested state

    Assert.Equal<Effect list>(
        [ Report(Problem.Operation(Operations.ArchiveFailed(KindCannotBeChanged tarball))) ],
        result.Effects
    )

[<Fact>]
let ``pasting into a panel of search results is refused as a copy into one is`` () =
    let fs = mockFs ()

    let state =
        started fs |> update fs (sentToPanel [ hitIn "start/a" "one.txt" ]) |> _.State

    let result = update fs FilesPasteRequested state

    Assert.Equal<Effect list>([ Report(Problem.Operation(Operations.NotInSearchResults Receiving)) ], result.Effects)

[<Fact>]
let ``files on the clipboard to be copied are copied through the batch F5 runs`` () =
    let fs = mockFs ()
    let files = onClipboard ForCopying [ "work/a.txt"; "work/b" ]
    let target = ToDirectory(fp (at "start"))

    let result = update fs (ClipboardFilesRead(files, target)) (started fs)

    Assert.Equal<Effect list>(
        [ CopyPlaces([ FromDisk [ fp (at "work/a.txt"); fp (at "work/b") ] ], target) ],
        result.Effects
    )

[<Fact>]
let ``files on the clipboard to be moved are moved as pasted files`` () =
    let fs = mockFs ()
    let files = onClipboard ForMoving [ "work/a.txt" ]
    let target = ToArchive(ArchiveFixture.archive, ArchivePath.root)

    let result = update fs (ClipboardFilesRead(files, target)) (started fs)

    Assert.Equal<Effect list>([ MovePastedFiles(files, target) ], result.Effects)
