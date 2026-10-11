module Marbas.Tests.Ui.Find.ResultsStateTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.FileSystem.FileSearch
open Marbas.Ui.Find
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private moment = DateTimeOffset(2026, 9, 18, 14, 30, 0, TimeSpan.Zero)

let private file name =
    { Name = EntryName.create name
      Kind = File
      Size = Some 1024L
      Measured = None
      Modified = moment
      Visibility = Plain
      Executability = NotExecutable
      LinkTarget = None
      Verification = Verified }

let private onDisk where = OnDisk(fp (at where))

let private hitIn where name =
    { Location = onDisk where
      Entry = file name }

let private directoryIn where name =
    { Location = onDisk where
      Entry =
        { file name with
            Kind = Directory
            Size = None } }

let private counts files =
    { Files = files
      Directories = 0
      Skipped = 0 }

let private arriving hits =
    hits
    |> List.mapi (fun index hit ->
        { Hit = hit
          Counts = counts (index + 1) })

let private query =
    { What = FindWhat.create (NameMasks.read "*.txt") None FilesOnly
      Scope = FromDirectory
      Hidden = Concealed
      Links = StopAtLinks
      Archives = SkipArchives }

let private ground =
    { Roots = [ onDisk "start" ]
      OtherRoots = [] }

let private walk = ResultsState.start (Walk(query, ground))

let private having hits =
    walk |> ResultsState.arrived (arriving hits) |> snd

let private before =
    { Query = query
      Ground = ground
      Hits =
        [ hitIn "start" "a.txt"
          directoryIn "start" "docs"
          hitIn "start/docs" "b.txt" ]
      Outcome =
        Finished
            { Files = 2
              Directories = 1
              Skipped = 4 }
      Cursor = 2 }

[<Fact>]
let ``a walk starts with nothing found`` () =
    let state = walk

    Assert.Empty(ResultsState.hits state)
    Assert.Empty(ResultsState.rows state)

[<Fact>]
let ``a walk that has not ended is still running`` () =
    let running = ResultsState.running walk

    Assert.True running

[<Fact>]
let ``a tick answers the rows it adds`` () =
    let added, _ =
        ResultsState.arrived (arriving [ hitIn "start" "a.txt"; hitIn "start" "b.txt" ]) walk

    Assert.Equal<ResultRow.ResultRow list>(
        [ ResultRow.GroupHeader(onDisk "start")
          ResultRow.Hit(hitIn "start" "a.txt")
          ResultRow.Hit(hitIn "start" "b.txt") ],
        added
    )

[<Fact>]
let ``a tick in the directory the one before ended in opens no second header`` () =
    let state = having [ hitIn "start" "a.txt" ]

    let added, _ = ResultsState.arrived (arriving [ hitIn "start" "b.txt" ]) state

    Assert.Equal<ResultRow.ResultRow list>([ ResultRow.Hit(hitIn "start" "b.txt") ], added)

[<Fact>]
let ``a tick in another directory opens a header of its own`` () =
    let state = having [ hitIn "start" "a.txt" ]

    let added, _ = ResultsState.arrived (arriving [ hitIn "start/docs" "b.txt" ]) state

    Assert.Equal<ResultRow.ResultRow list>(
        [ ResultRow.GroupHeader(onDisk "start/docs")
          ResultRow.Hit(hitIn "start/docs" "b.txt") ],
        added
    )

[<Fact>]
let ``the hits stand in the order they arrived over every tick`` () =
    let state = having [ hitIn "start" "a.txt"; hitIn "start" "b.txt" ]

    let _, later = ResultsState.arrived (arriving [ hitIn "start/docs" "c.txt" ]) state

    Assert.Equal<FindHit list>(
        [ hitIn "start" "a.txt"; hitIn "start" "b.txt"; hitIn "start/docs" "c.txt" ],
        ResultsState.hits later
    )

[<Fact>]
let ``the rows stand in the order they arrived over every tick`` () =
    let state = having [ hitIn "start" "a.txt" ]

    let _, later = ResultsState.arrived (arriving [ hitIn "start/docs" "b.txt" ]) state

    Assert.Equal<ResultRow.ResultRow list>(
        [ ResultRow.GroupHeader(onDisk "start")
          ResultRow.Hit(hitIn "start" "a.txt")
          ResultRow.GroupHeader(onDisk "start/docs")
          ResultRow.Hit(hitIn "start/docs" "b.txt") ],
        ResultsState.rows later
    )

[<Fact>]
let ``a tick that brought nothing adds no rows and keeps the counts`` () =
    let fs = MockFileSystem()
    let state = having [ hitIn "start" "a.txt" ]

    let added, later = ResultsState.arrived [] state

    Assert.Empty added
    Assert.Equal(FindSummary.Walking("", counts 1), ResultsState.progress fs later)

[<Fact>]
let ``the counts shown are those of the last hit reported`` () =
    let fs = MockFileSystem()

    let state =
        having [ hitIn "start" "a.txt"; hitIn "start" "b.txt"; hitIn "start" "c.txt" ]

    let progress = ResultsState.progress fs state

    Assert.Equal(FindSummary.Walking("", counts 3), progress)

[<Fact>]
let ``a step of the walk names the directory it is in`` () =
    let fs = MockFileSystem()

    let state = ResultsState.walking (at "start/docs") walk

    Assert.Equal(FindSummary.Walking(at "start/docs", counts 0), ResultsState.progress fs state)

[<Fact>]
let ``a step of the walk leaves the rows alone`` () =
    let state = having [ hitIn "start" "a.txt" ]

    let stepped = ResultsState.walking (at "start/docs") state

    Assert.Equal<ResultRow.ResultRow list>(ResultsState.rows state, ResultsState.rows stepped)

[<Fact>]
let ``an ended walk shows the outcome it answered`` () =
    let fs = MockFileSystem()
    let outcome = Finished(counts 7)

    let state = ResultsState.ended outcome walk

    Assert.Equal(FindSummary.Done outcome, ResultsState.progress fs state)

[<Fact>]
let ``an ended walk runs no more`` () =
    let state = ResultsState.ended (Stopped(counts 0)) walk

    let running = ResultsState.running state

    Assert.False running

[<Fact>]
let ``leaving a walk that still runs stops it and keeps the window`` () =
    let leave, _ = ResultsState.leaving walk

    Assert.Equal(StopsWalk, leave)

[<Fact>]
let ``leaving again closes the window over what was found`` () =
    let _, asked = ResultsState.leaving walk

    let leave, _ = ResultsState.leaving asked

    Assert.Equal(ClosesWindow, leave)

[<Fact>]
let ``leaving a walk that ended on its own closes at once`` () =
    let state = ResultsState.ended (Finished(counts 0)) walk

    let leave, _ = ResultsState.leaving state

    Assert.Equal(ClosesWindow, leave)

[<Fact>]
let ``a file under the cursor can be viewed`` () =
    let state = having [ hitIn "start" "a.txt" ]

    let viewed = ResultsState.viewableAt 1 state

    Assert.Equal(Some(hitIn "start" "a.txt"), viewed)

[<Fact>]
let ``a directory under the cursor is nothing to view`` () =
    let state =
        walk |> ResultsState.arrived (arriving [ directoryIn "start" "docs" ]) |> snd

    let viewed = ResultsState.viewableAt 1 state

    Assert.Equal(None, viewed)

[<Fact>]
let ``the header of a group is nothing to view`` () =
    let state = having [ hitIn "start" "a.txt" ]

    let viewed = ResultsState.viewableAt 0 state

    Assert.Equal(None, viewed)

[<Theory>]
[<InlineData(-1)>]
[<InlineData(2)>]
[<InlineData(40)>]
let ``there is no row outside the list`` (index: int) =
    let state = having [ hitIn "start" "a.txt" ]

    let row = ResultsState.rowAt index state

    Assert.Equal(None, row)

[<Fact>]
let ``a hit under the cursor is where the panel would stand`` () =
    let state = having [ hitIn "start" "a.txt" ]

    let landing = ResultsState.landingAt 1 state

    Assert.Equal(Some(AtHit(hitIn "start" "a.txt")), landing)

[<Fact>]
let ``the header of a group lands in the directory it names`` () =
    let state = having [ hitIn "start/docs" "a.txt" ]

    let landing = ResultsState.landingAt 0 state

    Assert.Equal(Some(At(Into(onDisk "start/docs"))), landing)

[<Fact>]
let ``an empty list is nowhere to land`` () =
    let landing = ResultsState.landingAt 0 walk

    Assert.Equal(None, landing)

[<Fact>]
let ``a window left without a choice answers no landing`` () =
    let state = having [ hitIn "start" "a.txt" ]

    let answer = ResultsState.answer 0 state

    Assert.Equal(None, answer.Landing)

[<Fact>]
let ``the landing chosen is the one answered`` () =
    let state = having [ hitIn "start" "a.txt" ]

    let answer = state |> ResultsState.landed InPanel |> ResultsState.answer 0

    Assert.Equal(Some InPanel, answer.Landing)

[<Fact>]
let ``the answer carries the walk, what it found and the row it was left on`` () =
    let state =
        having [ hitIn "start" "a.txt"; hitIn "start" "b.txt" ]
        |> ResultsState.ended (Finished(counts 2))

    let answer = ResultsState.answer 2 state

    Assert.Equal(
        { Query = query
          Ground = ground
          Hits = [ hitIn "start" "a.txt"; hitIn "start" "b.txt" ]
          Outcome = Finished(counts 2)
          Cursor = 2 },
        answer.Results
    )

[<Fact>]
let ``a walk left while it still ran answers a stopped walk with the counts so far`` () =
    let state = having [ hitIn "start" "a.txt"; hitIn "start" "b.txt" ]

    let answer = ResultsState.answer 0 state

    Assert.Equal(Stopped(counts 2), answer.Results.Outcome)

[<Fact>]
let ``results opened again hold what was found before`` () =
    let state = ResultsState.start (Reopen(before, 0))

    Assert.Equal<FindHit list>(before.Hits, ResultsState.hits state)

    Assert.Equal<ResultRow.ResultRow list>(
        [ ResultRow.GroupHeader(onDisk "start")
          ResultRow.Hit(hitIn "start" "a.txt")
          ResultRow.Hit(directoryIn "start" "docs")
          ResultRow.GroupHeader(onDisk "start/docs")
          ResultRow.Hit(hitIn "start/docs" "b.txt") ],
        ResultsState.rows state
    )

[<Fact>]
let ``results opened again are no running walk`` () =
    let state = ResultsState.start (Reopen(before, 0))

    let running = ResultsState.running state

    Assert.False running

[<Fact>]
let ``results opened again name the query, where it ran, what is left and what is gone`` () =
    let fs = MockFileSystem()
    let state = ResultsState.start (Reopen(before, 3))

    let progress = ResultsState.progress fs state

    Assert.Equal(
        FindSummary.Reopened(
            "*.txt",
            at "start",
            { Files = 2
              Directories = 1
              Skipped = 0 },
            3
        ),
        progress
    )

[<Fact>]
let ``results opened again answer the outcome of the walk they came from`` () =
    let state = ResultsState.start (Reopen(before, 0))

    let answer = ResultsState.answer 1 state

    Assert.Equal(before.Outcome, answer.Results.Outcome)

[<Fact>]
let ``hits arriving after results opened again follow the last of them`` () =
    let state = ResultsState.start (Reopen(before, 0))

    let added, _ = ResultsState.arrived (arriving [ hitIn "start/docs" "c.txt" ]) state

    Assert.Equal<ResultRow.ResultRow list>([ ResultRow.Hit(hitIn "start/docs" "c.txt") ], added)

[<Fact>]
let ``the lines are laid out for no room until the list is`` () =
    let room = ResultsState.room walk

    Assert.Equal(0, room)

[<Fact>]
let ``a list laid out remembers the room its lines were written for`` () =
    let state = ResultsState.laidOut 60 walk

    let room = ResultsState.room state

    Assert.Equal(60, room)

[<Fact>]
let ``every row is a line as wide as the room`` () =
    let fs = MockFileSystem()
    let state = having [ hitIn "start" "a.txt" ] |> ResultsState.laidOut 60

    let lines = ResultsState.lines fs state

    Assert.Equal<string list>([ at "start"; ResultRow.describe fs 60 (ResultRow.Hit(hitIn "start" "a.txt")) ], lines)
