module Marbas.Tests.State.ClipboardTests

open System
open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.State
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "start/readme.txt", MockFileData "hi")
    fs.AddFile(at "start/a/one.txt", MockFileData "1")
    fs.AddFile(at "start/b/two.txt", MockFileData "2")
    fs

let private started (fs: IFileSystem) =
    create (fp (at "start"))
    |> loadPanel fs Left (fp (at "start"))
    |> Expect.ok
    |> loadPanel fs Right (fp (at "start"))
    |> Expect.ok

let private indexOf name state =
    (activePanel state).Entries
    |> Array.findIndex (fun entry -> EntryName.value entry.Name = name)

let private standingOn name state =
    setCursor Left (indexOf name state) state

let private markedOn names state =
    names
    |> List.fold (fun marked name -> marked |> standingOn name |> toggleSelection Left) state

let private copying lines =
    [ PutOnClipboard(ClipboardText.tryCreate lines |> Expect.some) ]

let private file name =
    { Name = EntryName.create name
      Kind = File
      Size = Some 1L
      Measured = None
      Modified = DateTimeOffset.MinValue
      Visibility = Plain
      Executability = NotExecutable
      LinkTarget = None
      Verification = Verified }

let private hitIn where name =
    { Location = OnDisk(fp (at where))
      Entry = file name }

let private sentToPanel hits =
    SearchAnswered
        { Landing = Some InPanel
          Results =
            { Query =
                { What = FindWhat.create (NameMasks.read "*.txt") None FilesOnly
                  Scope = FromDirectory
                  Hidden = Concealed
                  Links = StopAtLinks
                  Archives = SkipArchives }
              Ground =
                { Roots = [ OnDisk(fp (at "start")) ]
                  OtherRoots = [] }
              Hits = hits
              Outcome =
                FileSearch.Finished
                    { Files = List.length hits
                      Directories = 0
                      Skipped = 0 }
              Cursor = 0 } }

let private everything = NamePattern.tryCreate "*" |> Expect.some

[<Fact>]
let ``the name of the entry under the cursor is put on the clipboard`` () =
    let fs = mockFs ()
    let state = started fs |> standingOn "notes.txt"

    let result = update fs NamesCopyRequested state

    Assert.Equal<Effect list>(copying [ "notes.txt" ], result.Effects)

[<Fact>]
let ``a directory is named without a separator after it`` () =
    let fs = mockFs ()
    let state = started fs |> standingOn "docs"

    let result = update fs NamesCopyRequested state

    Assert.Equal<Effect list>(copying [ "docs" ], result.Effects)

[<Fact>]
let ``the marked entries are named in the order of the listing, whatever the cursor stands on`` () =
    let fs = mockFs ()

    let state =
        started fs |> markedOn [ "readme.txt"; "docs" ] |> standingOn "notes.txt"

    let result = update fs NamesCopyRequested state

    Assert.Equal<Effect list>(copying [ "docs"; "readme.txt" ], result.Effects)

[<Fact>]
let ``nothing is copied with the parent entry under the cursor and nothing marked`` () =
    let fs = mockFs ()
    let state = started fs |> standingOn ".."

    let result = update fs NamesCopyRequested state

    Assert.Empty result.Effects

[<Fact>]
let ``the marks stay where they were after the names are copied`` () =
    let fs = mockFs ()
    let state = started fs |> markedOn [ "readme.txt"; "docs" ]

    let result = update fs NamesCopyRequested state

    Assert.Equal<Set<EntryName>>(state.Left.Selected, result.State.Left.Selected)

[<Fact>]
let ``each hit in a panel of search results gives its own name`` () =
    let fs = mockFs ()

    let state =
        started fs
        |> update fs (sentToPanel [ hitIn "start/a" "one.txt"; hitIn "start/b" "two.txt" ])
        |> _.State
        |> update fs (PatternEntered(Select, everything))
        |> _.State

    let result = update fs NamesCopyRequested state

    Assert.Equal<Effect list>(copying [ "one.txt"; "two.txt" ], result.Effects)

[<Fact>]
let ``the entries inside an archive are named as the archive holds them`` () =
    let fs = ArchiveFixture.packed ()

    let state =
        ArchiveFixture.loaded fs
        |> standInside fs Left (InArchive(ArchiveFixture.archive, ArchivePath.root))
        |> Expect.ok
        |> update fs (PatternEntered(Select, everything))
        |> _.State

    let result = update fs NamesCopyRequested state

    Assert.Equal<Effect list>(copying [ "docs"; "notes.txt" ], result.Effects)

[<Fact>]
let ``a clipboard the system does not offer is reported the first time`` () =
    let fs = mockFs ()

    let result = update fs (ClipboardRefused ClipboardUnavailable) (started fs)

    Assert.Equal<Effect list>([ ReportClipboardError ClipboardUnavailable ], result.Effects)

[<Fact>]
let ``a clipboard the system does not offer is not reported again in the same session`` () =
    let fs = mockFs ()
    let reported = update fs (ClipboardRefused ClipboardUnavailable) (started fs)

    let result = update fs (ClipboardRefused ClipboardUnavailable) reported.State

    Assert.Empty result.Effects

[<Fact>]
let ``a clipboard that failed is reported every time`` () =
    let fs = mockFs ()
    let failure = ClipboardFailed "The clipboard is held by another program."
    let reported = update fs (ClipboardRefused failure) (started fs)

    let result = update fs (ClipboardRefused failure) reported.State

    Assert.Equal<Effect list>([ ReportClipboardError failure ], result.Effects)

[<Fact>]
let ``the full path of the entry under the cursor is put on the clipboard`` () =
    let fs = mockFs ()
    let state = started fs |> standingOn "notes.txt"

    let result = update fs PathsCopyRequested state

    Assert.Equal<Effect list>(copying [ at "start/notes.txt" ], result.Effects)

[<Fact>]
let ``the full path of a directory carries no separator after it`` () =
    let fs = mockFs ()
    let state = started fs |> standingOn "docs"

    let result = update fs PathsCopyRequested state

    Assert.Equal<Effect list>(copying [ at "start/docs" ], result.Effects)

[<Fact>]
let ``the full paths of the marked entries come in the order of the listing`` () =
    let fs = mockFs ()

    let state =
        started fs |> markedOn [ "readme.txt"; "docs" ] |> standingOn "notes.txt"

    let result = update fs PathsCopyRequested state

    Assert.Equal<Effect list>(copying [ at "start/docs"; at "start/readme.txt" ], result.Effects)

[<Fact>]
let ``the parent entry under the cursor with nothing marked copies the directory the panel shows`` () =
    let fs = mockFs ()
    let state = started fs |> standingOn ".."

    let result = update fs PathsCopyRequested state

    Assert.Equal<Effect list>(copying [ at "start" ], result.Effects)

[<Fact>]
let ``the parent entry under the cursor leaves the marked entries alone to be copied`` () =
    let fs = mockFs ()
    let state = started fs |> markedOn [ "readme.txt"; "docs" ] |> standingOn ".."

    let result = update fs PathsCopyRequested state

    Assert.Equal<Effect list>(copying [ at "start/docs"; at "start/readme.txt" ], result.Effects)

[<Fact>]
let ``the parent entry inside an archive copies the level the way the panel title spells it`` () =
    let fs = ArchiveFixture.packed ()
    let docs = ArchivePath.tryCreate "docs" |> Expect.some

    let state =
        ArchiveFixture.loaded fs
        |> standInside fs Left (InArchive(ArchiveFixture.archive, docs))
        |> Expect.ok
        |> standingOn ".."

    let result = update fs PathsCopyRequested state

    Assert.Equal<Effect list>(copying [ at "start/backup.zip/docs" ], result.Effects)

[<Fact>]
let ``the parent entry in a panel of search results copies no path`` () =
    let fs = mockFs ()

    let state =
        started fs
        |> update fs (sentToPanel [ hitIn "start/a" "one.txt" ])
        |> _.State
        |> standingOn ".."

    let result = update fs PathsCopyRequested state

    Assert.Empty result.Effects

[<Fact>]
let ``the full path of an entry inside an archive is spelled the way the panel title spells it`` () =
    let fs = ArchiveFixture.packed ()
    let docs = ArchivePath.tryCreate "docs" |> Expect.some

    let state =
        ArchiveFixture.loaded fs
        |> standInside fs Left (InArchive(ArchiveFixture.archive, docs))
        |> Expect.ok
        |> update fs (PatternEntered(Select, everything))
        |> _.State

    let result = update fs PathsCopyRequested state

    Assert.Equal<Effect list>(
        copying [ at "start/backup.zip/docs/deep"; at "start/backup.zip/docs/inner.txt" ],
        result.Effects
    )

[<Fact>]
let ``each hit in a panel of search results gives its own full path, inside an archive too`` () =
    let fs = ArchiveFixture.packed ()
    fs.AddFile(at "start/a/one.txt", MockFileData "1")
    let docs = ArchivePath.tryCreate "docs" |> Expect.some

    let insideArchive =
        { Location = InArchive(ArchiveFixture.archive, docs)
          Entry = file "inner.txt" }

    let state =
        ArchiveFixture.loaded fs
        |> update fs (sentToPanel [ hitIn "start/a" "one.txt"; insideArchive ])
        |> _.State
        |> update fs (PatternEntered(Select, everything))
        |> _.State

    let result = update fs PathsCopyRequested state

    Assert.Equal<Effect list>(copying [ at "start/a/one.txt"; at "start/backup.zip/docs/inner.txt" ], result.Effects)

let private linked () =
    let fs = mockFs ()
    fs.AddFile(at "work/report.txt", MockFileData "report")
    fs.File.CreateSymbolicLink(at "start/twin.txt", at "start/notes.txt") |> ignore
    fs.Directory.CreateSymbolicLink(at "start/place", at "work") |> ignore
    fs.Directory.SetCurrentDirectory(at "start")
    fs.File.CreateSymbolicLink(at "start/near.txt", "readme.txt") |> ignore
    fs.AddFile(at "start/report.lnk", MockFileData(ShortcutBytes.toFullPath (at "work/report.txt")))
    fs.AddFile(at "start/broken.lnk", MockFileData "not a shortcut")
    fs

[<Theory>]
[<InlineData("twin.txt", "start/notes.txt")>]
[<InlineData("place", "work")>]
[<InlineData("near.txt", "start/readme.txt")>]
[<InlineData("report.lnk", "work/report.txt")>]
let ``a link or a shortcut under the cursor copies the place it leads to`` (name: string, target: string) =
    let fs = linked ()
    let state = started fs |> standingOn name

    let result = update fs LinkTargetsCopyRequested state

    Assert.Equal<Effect list>(copying [ at target ], result.Effects)

[<Theory>]
[<InlineData("notes.txt", "start/notes.txt")>]
[<InlineData("docs", "start/docs")>]
[<InlineData("broken.lnk", "start/broken.lnk")>]
let ``an entry that leads nowhere else copies its own full path`` (name: string, path: string) =
    let fs = linked ()
    let state = started fs |> standingOn name

    let result = update fs LinkTargetsCopyRequested state

    Assert.Equal<Effect list>(copying [ at path ], result.Effects)

[<Fact>]
let ``each marked entry gives the place it leads to, in the order of the listing`` () =
    let fs = linked ()
    let state = started fs |> markedOn [ "twin.txt"; "notes.txt"; "report.lnk" ]

    let result = update fs LinkTargetsCopyRequested state

    Assert.Equal<Effect list>(
        copying [ at "start/notes.txt"; at "work/report.txt"; at "start/notes.txt" ],
        result.Effects
    )

[<Fact>]
let ``an entry inside an archive copies its own full path`` () =
    let fs = ArchiveFixture.packed ()

    let state =
        ArchiveFixture.loaded fs
        |> standInside fs Left (InArchive(ArchiveFixture.archive, ArchivePath.root))
        |> Expect.ok
        |> standingOn "notes.txt"

    let result = update fs LinkTargetsCopyRequested state

    Assert.Equal<Effect list>(copying [ at "start/backup.zip/notes.txt" ], result.Effects)

[<Fact>]
let ``a shortcut found by a search copies the place it leads to`` () =
    let fs = linked ()

    let state =
        started fs
        |> update fs (sentToPanel [ hitIn "start" "report.lnk" ])
        |> _.State
        |> update fs (PatternEntered(Select, everything))
        |> _.State

    let result = update fs LinkTargetsCopyRequested state

    Assert.Equal<Effect list>(copying [ at "work/report.txt" ], result.Effects)

[<Fact>]
let ``no link target is copied with the parent entry under the cursor and nothing marked`` () =
    let fs = linked ()
    let state = started fs |> standingOn ".."

    let result = update fs LinkTargetsCopyRequested state

    Assert.Empty result.Effects

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

let private docs = ArchivePath.tryCreate "docs" |> Expect.some

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
        [ ReportOperationError(Operations.ArchiveEntryNotCut(fp (at "start/backup.zip/notes.txt"))) ],
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
        [ ReportOperationError(Operations.ArchiveEntryNotCut(fp (at "start/backup.zip/docs/inner.txt"))) ],
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
        handing ForCopying [ "temp/1/notes.txt" ] @ [ ReportItemFailures [ failed ] ],
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
        [ ReportOperationError(Operations.ArchiveFailed(KindCannotBeChanged tarball)) ],
        result.Effects
    )

[<Fact>]
let ``pasting into a panel of search results is refused as a copy into one is`` () =
    let fs = mockFs ()

    let state =
        started fs |> update fs (sentToPanel [ hitIn "start/a" "one.txt" ]) |> _.State

    let result = update fs FilesPasteRequested state

    Assert.Equal<Effect list>([ ReportOperationError(Operations.NotInSearchResults Receiving) ], result.Effects)

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
