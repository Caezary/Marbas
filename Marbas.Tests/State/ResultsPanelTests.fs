module Marbas.Tests.State.ResultsPanelTests

open System
open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests

let private at = Root.at

let private relative = Root.relative

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "start/docs/inner.txt", MockFileData "hi")
    fs.AddDirectory(at "start/docs/deep")
    fs.AddDirectory(at "backup")
    fs :> IFileSystem

let private query =
    { What = FindWhat.create (NameMasks.read "*.txt") None FilesOnly
      Scope = FromDirectory
      Hidden = Concealed
      Links = StopAtLinks
      Archives = SkipArchives }

let private entry name kind =
    { Name = EntryName.create name
      Kind = kind
      Size = Some 2L
      Measured = None
      Modified = DateTimeOffset.MinValue
      Visibility = Plain
      Executability = NotExecutable
      LinkTarget = None }

let private hitIn where name kind =
    { Location = OnDisk(fp (at where))
      Entry = entry name kind }

let private results hits =
    { Query = query
      Ground =
        { Roots = [ OnDisk(fp (at "start")) ]
          OtherRoots = [] }
      Hits = hits
      Outcome = FileSearch.Finished
                    { Files = List.length hits
                      Directories = 0
                      Skipped = 0 }
      Cursor = 0 }

let private sentToPanel hits =
    SearchAnswered { Landing = Some InPanel; Results = results hits }

let private loaded (fs: IFileSystem) =
    create (fp (at "start")) |> loadPanel fs Left (fp (at "start")) |> Expect.ok

let private hits = [ hitIn "start/docs" "inner.txt" File; hitIn "start" "notes.txt" File ]

let private showing fs state = update fs (sentToPanel hits) state |> _.State

let private named state =
    (activePanel state).Entries |> List.map (_.Name >> EntryName.value)

let private cursorName state =
    cursorEntry (activePanel state) |> Option.map (_.Name >> EntryName.value)

let private standingAt name state =
    let index =
        (activePanel state).Entries
        |> List.findIndex (fun e -> EntryName.value e.Name = name)

    setCursor state.Active index state

[<Fact>]
let ``the hits sent to the panel are listed under their paths below the start`` () =
    let fs = mockFs ()

    let state = loaded fs |> showing fs

    Assert.Equal<string list>([ ".."; relative "docs/inner.txt"; "notes.txt" ], named state)

[<Fact>]
let ``the panel showing results remembers the location it stood on`` () =
    let fs = mockFs ()

    let state = loaded fs |> setCursor Left 1 |> showing fs

    match (activePanel state).Path with
    | SearchResults found ->
        Assert.Equal(OnDisk(fp (at "start")), found.Origin)
        Assert.Equal(Some(EntryName.create "docs"), found.Returning)
    | other -> failwith $"expected results, got %A{other}"

[<Fact>]
let ``the way back stands the panel where it was, on the entry it left`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 1 |> showing fs

    let back = update fs WalkIntoRequested state |> _.State

    Assert.Equal(OnDisk(fp (at "start")), (activePanel back).Path)
    Assert.Equal(Some "docs", cursorName back)

[<Fact>]
let ``a second search from a results panel keeps the first way back`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 1 |> showing fs |> showing fs

    match (activePanel state).Path with
    | SearchResults found ->
        Assert.Equal(OnDisk(fp (at "start")), found.Origin)
        Assert.Equal(Some(EntryName.create "docs"), found.Returning)
    | other -> failwith $"expected results, got %A{other}"

[<Fact>]
let ``a panel showing results is watched by nothing of its own`` () =
    let fs = mockFs ()
    let state = loaded fs |> loadPanel fs Right (fp (at "backup")) |> Expect.ok

    let result = update fs (sentToPanel hits) state

    Assert.Contains(WatchLocations [ fp (at "backup") ], result.Effects)

[<Fact>]
let ``reading the panel again drops a hit that is gone`` () =
    let fs = mockFs ()
    let state = loaded fs |> showing fs
    fs.File.Delete(at "start/notes.txt")

    let again = update fs RefreshRequested state |> _.State

    Assert.Equal<string list>([ ".."; relative "docs/inner.txt" ], named again)

[<Fact>]
let ``Enter on a directory hit stands the panel inside that directory`` () =
    let fs = mockFs ()

    let state =
        loaded fs
        |> update fs (sentToPanel [ hitIn "start/docs" "deep" Directory ])
        |> _.State
        |> standingAt (relative "docs/deep")

    let walked = update fs EntryOpened state |> _.State

    Assert.Equal(OnDisk(fp (at "start/docs/deep")), (activePanel walked).Path)

[<Fact>]
let ``Enter on a file hit runs it from the directory holding it`` () =
    let fs = mockFs ()
    let state = loaded fs |> showing fs |> standingAt (relative "docs/inner.txt")

    let result = update fs EntryOpened state

    Assert.Equal<Effect list>(
        [ RunProgram
              { Target = fp (at "start/docs/inner.txt")
                Program = None
                WorkingDirectory = fp (at "start/docs")
                Elevation = AsUser } ],
        result.Effects
    )

[<Fact>]
let ``F3 views the file a hit stands for`` () =
    let fs = mockFs ()
    let state = loaded fs |> showing fs |> standingAt (relative "docs/inner.txt")

    let result = update fs ViewRequested state

    Assert.Equal<Effect list>([ ViewFile(fp (at "start/docs/inner.txt")) ], result.Effects)

[<Fact>]
let ``a search from a results panel starts where that panel returns to`` () =
    let fs = mockFs ()
    let state = loaded fs |> showing fs

    let result = update fs (FindEntered query) state

    Assert.Equal<Effect list>([ RunSearch(query, OnDisk(fp (at "start")), []) ], result.Effects)

[<Fact>]
let ``a marked directory of a results panel is a place of its own to search`` () =
    let fs = mockFs ()

    let state =
        loaded fs
        |> update fs (sentToPanel [ hitIn "start/docs" "deep" Directory ])
        |> _.State
        |> standingAt (relative "docs/deep")
        |> toggleSelection Left

    let result = update fs (FindEntered { query with Scope = MarkedDirectories }) state

    Assert.Equal<Effect list>(
        [ RunSearch(
              { query with Scope = MarkedDirectories },
              OnDisk(fp (at "start")),
              [ OnDisk(fp (at "start/docs/deep")) ]
          ) ],
        result.Effects
    )

[<Fact>]
let ``a tab opened beside a results panel opens on the location it returns to`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 1 |> showing fs

    let opened = update fs TabOpened state |> _.State

    Assert.Equal(OnDisk(fp (at "start")), (activePanel opened).Path)
    Assert.Equal(Some "docs", cursorName opened)

[<Fact>]
let ``the session file keeps the location the results panel returns to`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 1 |> showing fs

    let written = settings fs state

    let left = (Marbas.Collections.Zipper.current written.Tabs).Left
    Assert.Equal(fp (at "start"), left.Path)
    Assert.Equal(Some(EntryName.create "docs"), left.Cursor)
