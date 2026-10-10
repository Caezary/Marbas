module Marbas.Tests.State.FindTests

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
    fs.AddDirectory(at "backup")
    fs

let private loaded (fs: IFileSystem) =
    create (fp (at "start")) |> loadPanel fs Left (fp (at "start")) |> Expect.ok

let private marking state =
    state |> toggleSelectionMoving Downward Left

let private query scope =
    { What = FindWhat.create (NameMasks.read "*.txt") None FilesOnly
      Scope = scope
      Hidden = Concealed
      Links = StopAtLinks
      Archives = SkipArchives }

[<Fact>]
let ``the popup is asked about the panel the key was pressed in`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = update fs FindRequested state

    Assert.Equal<Effect list>([ PromptForFind(NoDirectoryMarked, Concealed, None) ], result.Effects)

[<Fact>]
let ``a panel showing its hidden entries offers to search them`` () =
    let fs = mockFs ()
    let state = loaded fs |> toggleHidden fs Left |> Expect.ok

    let result = update fs FindRequested state

    Assert.Equal<Effect list>([ PromptForFind(NoDirectoryMarked, Shown, None) ], result.Effects)

[<Fact>]
let ``a marked directory opens the scope that walks the marks`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 1 |> marking

    let result = update fs FindRequested state

    Assert.Equal<Effect list>([ PromptForFind(DirectoriesMarked, Concealed, None) ], result.Effects)

[<Fact>]
let ``a marked file leaves that scope closed`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2 |> marking

    let result = update fs FindRequested state

    Assert.Equal<Effect list>([ PromptForFind(NoDirectoryMarked, Concealed, None) ], result.Effects)

[<Fact>]
let ``the entry under the cursor is no mark of its own`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 1

    let result = update fs FindRequested state

    Assert.Equal<Effect list>([ PromptForFind(NoDirectoryMarked, Concealed, None) ], result.Effects)

[<Fact>]
let ``the query the popup answered runs from where the panel stands`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = update fs (FindEntered(query FromDirectory)) state

    Assert.Equal<Effect list>([ RunSearch(query FromDirectory, OnDisk(fp (at "start")), []) ], result.Effects)

[<Fact>]
let ``the marked directories travel with the query that walks them`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 1 |> marking

    let result = update fs (FindEntered(query MarkedDirectories)) state

    Assert.Equal<Effect list>(
        [ RunSearch(query MarkedDirectories, OnDisk(fp (at "start")), [ OnDisk(fp (at "start/docs")) ]) ],
        result.Effects
    )

[<Fact>]
let ``a marked file is no place to search`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2 |> marking

    let result = update fs (FindEntered(query MarkedDirectories)) state

    Assert.Equal<Effect list>([ RunSearch(query MarkedDirectories, OnDisk(fp (at "start")), []) ], result.Effects)

let private hidden = Root.at "start/.secret.txt"

let private treeWith () =
    let fs = mockFs ()
    fs.AddFile(at "start/docs/inner.txt", MockFileData "hi")
    fs.AddFile(hidden, MockFileData "hi")
    fs

let private entry name kind visibility =
    { Name = EntryName.create name
      Kind = kind
      Size = None
      Measured = None
      Modified = System.DateTimeOffset.MinValue
      Visibility = visibility
      Executability = NotExecutable
      LinkTarget = None
      Verification = Verified }

let private hitIn where name kind visibility : FindHit =
    { Location = OnDisk(fp (at where))
      Entry = entry name kind visibility }

let private results hits cursor =
    { Query = query FromDirectory
      Ground =
        { Roots = [ OnDisk(fp (at "start")) ]
          OtherRoots = [] }
      Hits = hits
      Outcome =
        FileSearch.Finished
            { Files = List.length hits
              Directories = 0
              Skipped = 0 }
      Cursor = cursor }

let private answer landing =
    { Landing = landing
      Results = results [] 0 }

let private landing where = answer (Some where)

let private cursorName state =
    PanelState.cursorEntry (activePanel state)
    |> Option.map (_.Name >> EntryName.value)

[<Fact>]
let ``Enter on a file stands the panel in the directory holding it, on the file`` () =
    let fs = treeWith ()
    let state = loaded fs |> loadPanel fs Left (fp (at "backup")) |> Expect.ok
    let hit = hitIn "start" "notes.txt" File Plain

    let result = update fs (SearchAnswered(landing (AtHit hit))) state

    Assert.Equal(OnDisk(fp (at "start")), (activePanel result.State).Path)
    Assert.Equal(Some "notes.txt", cursorName result.State)

[<Fact>]
let ``Enter on a directory stands the panel beside it, not inside it`` () =
    let fs = treeWith ()
    let state = loaded fs |> loadPanel fs Left (fp (at "backup")) |> Expect.ok
    let hit = hitIn "start" "docs" Directory Plain

    let result = update fs (SearchAnswered(landing (AtHit hit))) state

    Assert.Equal(OnDisk(fp (at "start")), (activePanel result.State).Path)
    Assert.Equal(Some "docs", cursorName result.State)

[<Fact>]
let ``a hit the panel conceals shows the hidden entries first`` () =
    let fs = treeWith ()
    let state = loaded fs
    let hit = hitIn "start" ".secret.txt" File Hidden

    let result = update fs (SearchAnswered(landing (AtHit hit))) state

    Assert.Equal(Shown, (activePanel result.State).Hidden)
    Assert.Equal(Some ".secret.txt", cursorName result.State)

[<Fact>]
let ``Enter on a group header stands the panel inside that directory`` () =
    let fs = treeWith ()
    let state = loaded fs

    let result =
        update fs (SearchAnswered(landing (At(Into(OnDisk(fp (at "start/docs"))))))) state

    Assert.Equal(OnDisk(fp (at "start/docs")), (activePanel result.State).Path)

[<Fact>]
let ``a landing beside an entry stands the panel in its directory, on the entry`` () =
    let fs = treeWith ()
    let state = loaded fs |> loadPanel fs Left (fp (at "backup")) |> Expect.ok
    let beside = Beside(OnDisk(fp (at "start")), EntryName.create "notes.txt")

    let result = update fs (SearchAnswered(landing (At beside))) state

    Assert.Equal(OnDisk(fp (at "start")), (activePanel result.State).Path)
    Assert.Equal(Some "notes.txt", cursorName result.State)

[<Fact>]
let ``a window left without a landing moves nothing`` () =
    let fs = treeWith ()
    let state = loaded fs

    let result = update fs (SearchAnswered(answer None)) state

    Assert.Equal(OnDisk(fp (at "start")), (activePanel result.State).Path)
    Assert.Empty result.Effects

[<Fact>]
let ``a landing whose directory is gone says so instead of moving`` () =
    let fs = treeWith ()
    let state = loaded fs

    let result =
        update fs (SearchAnswered(landing (At(Into(OnDisk(fp (at "start/gone"))))))) state

    Assert.Equal<Effect list>([ ReportListingError(DirectoryNotFound(fp (at "start/gone"))) ], result.Effects)

[<Fact>]
let ``Enter on a hit inside an archive walks the panel into that level`` () =
    let fs = ArchiveFixture.packed ()
    let state = ArchiveFixture.loaded fs

    let level =
        InArchive(ArchiveFixture.archive, ArchivePath.child ArchivePath.root (EntryName.create "docs"))

    let hit: FindHit =
        { Location = level
          Entry = entry "inner.txt" File Plain }

    let result = update fs (SearchAnswered(landing (AtHit hit))) state

    Assert.Equal(level, (activePanel result.State).Path)
    Assert.Equal(Some "inner.txt", cursorName result.State)

let private answering hits cursor =
    { Landing = None
      Results = results hits cursor }

[<Fact>]
let ``the answer of a walk becomes the last results`` () =
    let fs = treeWith ()
    let state = loaded fs

    let result =
        update fs (SearchAnswered(answering [ hitIn "start" "notes.txt" File Plain ] 1)) state

    Assert.Equal(Some(results [ hitIn "start" "notes.txt" File Plain ] 1), result.State.LastSearch)

[<Fact>]
let ``a new search replaces the answer of the one before it`` () =
    let fs = treeWith ()

    let state =
        loaded fs
        |> update fs (SearchAnswered(answering [ hitIn "start" "notes.txt" File Plain ] 1))
        |> _.State

    let result = update fs (SearchAnswered(answering [] 0)) state

    Assert.Equal(Some(results [] 0), result.State.LastSearch)

[<Fact>]
let ``the popup is told how many hits there are to reopen`` () =
    let fs = treeWith ()

    let state =
        loaded fs
        |> update fs (SearchAnswered(answering [ hitIn "start" "notes.txt" File Plain ] 1))
        |> _.State

    let result = update fs FindRequested state

    Assert.Equal<Effect list>([ PromptForFind(NoDirectoryMarked, Concealed, Some 1) ], result.Effects)

[<Fact>]
let ``the last results are shown again with the row the cursor left`` () =
    let fs = treeWith ()

    let state =
        loaded fs
        |> update fs (SearchAnswered(answering [ hitIn "start" "notes.txt" File Plain ] 1))
        |> _.State

    let result = update fs LastResultsRequested state

    Assert.Equal<Effect list>(
        [ ShowLastResults(results [ hitIn "start" "notes.txt" File Plain ] 1, 0) ],
        result.Effects
    )

[<Fact>]
let ``a hit that is gone by now is dropped and counted`` () =
    let fs = treeWith ()
    let kept = hitIn "start" "notes.txt" File Plain
    let found = results [ kept; hitIn "start" "gone.txt" File Plain ] 1
    let surviving = { found with Hits = [ kept ] }

    let state =
        loaded fs
        |> update fs (SearchAnswered { Landing = None; Results = found })
        |> _.State

    let result = update fs LastResultsRequested state

    Assert.Equal<Effect list>([ ShowLastResults(surviving, 1) ], result.Effects)
    Assert.Equal(Some surviving, result.State.LastSearch)

[<Fact>]
let ``there is nothing to reopen until something was searched for`` () =
    let fs = treeWith ()
    let state = loaded fs

    let result = update fs LastResultsRequested state

    Assert.Empty result.Effects
