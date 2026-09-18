module Marbas.Tests.State.ResultsOperationsTests

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
    fs.AddFile(at "start/a/one.txt", MockFileData "1")
    fs.AddFile(at "start/b/two.txt", MockFileData "2")
    fs.AddFile(at "start/c/three.txt", MockFileData "3")
    fs.AddFile(at "start/backup.zip", MockFileData "zip")
    fs.AddFile(at "start/old.tar", MockFileData "tar")
    fs.AddDirectory(at "target")
    fs

let private query =
    { What = FindWhat.create (NameMasks.read "*.txt") None FilesOnly
      Scope = FromDirectory
      Hidden = Concealed
      Links = StopAtLinks
      Archives = SkipArchives }

let private file name =
    { Name = EntryName.create name
      Kind = File
      Size = Some 1L
      Measured = None
      Modified = DateTimeOffset.MinValue
      Visibility = Plain
      Executability = NotExecutable
      LinkTarget = None }

let private hitIn where name =
    { Location = OnDisk(fp (at where))
      Entry = file name }

let private insideArchive =
    { Location = InArchive(fp (at "start/backup.zip"), ArchivePath.root)
      Entry = file "inner.txt" }

let private threeDirectories =
    [ hitIn "start/a" "one.txt"; hitIn "start/b" "two.txt"; hitIn "start/c" "three.txt" ]

let private sources =
    [ fp (at "start/a/one.txt"); fp (at "start/b/two.txt"); fp (at "start/c/three.txt") ]

let private sentToPanel hits =
    SearchAnswered
        { Landing = Some InPanel
          Results =
            { Query = query
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

let private showing (fs: MockFileSystem) hits =
    create (fp (at "start"))
    |> loadPanel fs Left (fp (at "start"))
    |> Expect.ok
    |> loadPanel fs Right (fp (at "target"))
    |> Expect.ok
    |> update fs (sentToPanel hits)
    |> _.State

let private marked (fs: MockFileSystem) hits =
    showing fs hits |> update fs (PatternEntered(Select, everything)) |> _.State

let private named state =
    (activePanel state).Entries |> List.map (_.Name >> EntryName.value)

let private markedNames state =
    (activePanel state).Selected |> Set.map EntryName.value |> Set.toList

let private item name outcome : Operations.ItemResult =
    { Name = EntryName.create name
      Outcome = outcome }

let private refused refusal =
    [ ReportOperationError(Operations.NotInSearchResults refusal) ]

[<Fact>]
let ``marked hits from three directories are copied in one batch`` () =
    let fs = mockFs ()
    let state = marked fs threeDirectories

    let result = update fs CopyRequested state

    Assert.Equal<Effect list>([ CopyTargets(sources, fp (at "target")) ], result.Effects)

[<Fact>]
let ``marked hits from three directories are moved where the prompt says`` () =
    let fs = mockFs ()
    let state = marked fs threeDirectories

    let asked = update fs MoveRequested state
    let result = update fs (TargetEntered(at "target")) state

    Assert.Equal<Effect list>([ PromptForTarget(fp (at "target")) ], asked.Effects)
    Assert.Equal<Effect list>([ MoveTargets(sources, fp (at "target")) ], result.Effects)

let private deletedAs mode =
    let fs = mockFs ()
    let state = marked fs threeDirectories

    let result = update fs (DeleteConfirmed mode) state

    Assert.Equal<Effect list>([ DeleteTargets(sources, mode) ], result.Effects)

[<Fact>]
let ``marked hits from three directories go to the trash in one batch`` () = deletedAs ToTrash

[<Fact>]
let ``marked hits from three directories are deleted for good in one batch`` () = deletedAs Permanently

[<Fact>]
let ``marked hits from three directories are wiped in one batch`` () = deletedAs ByWiping

[<Fact>]
let ``the confirmation names the hits by the paths the panel shows`` () =
    let fs = mockFs ()
    let state = marked fs threeDirectories

    let result = update fs (DeleteRequested ByWiping) state

    Assert.Equal<Effect list>(
        [ ConfirmDelete
              { Targets =
                  [ relative "a/one.txt"; relative "b/two.txt"; relative "c/three.txt" ]
                  |> List.map EntryName.create
                Scope = NamedEntriesOnly
                Mode = ByWiping } ],
        result.Effects
    )

[<Fact>]
let ``what was deleted leaves the listing and what failed keeps its mark`` () =
    let fs = mockFs ()
    let state = marked fs threeDirectories
    fs.File.Delete(at "start/a/one.txt")
    fs.File.Delete(at "start/b/two.txt")

    let results =
        [ item "one.txt" (Ok())
          item "two.txt" (Ok())
          item "three.txt" (Error(Operations.AccessDenied(fp (at "start/c/three.txt")))) ]

    let finished = update fs (DeleteFinished results) state |> _.State

    Assert.Equal<string list>([ ".."; relative "c/three.txt" ], named finished)
    Assert.Equal<string list>([ relative "c/three.txt" ], markedNames finished)

[<Fact>]
let ``what was copied loses its mark and what failed keeps it`` () =
    let fs = mockFs ()
    let state = marked fs threeDirectories

    let results =
        [ item "one.txt" (Ok())
          item "two.txt" (Error(Operations.TargetExists(fp (at "target/two.txt"))))
          item "three.txt" (Ok()) ]

    let finished = update fs (CopyFinished results) state |> _.State

    Assert.Equal<string list>([ relative "b/two.txt" ], markedNames finished)

let private insideTar =
    { Location = InArchive(fp (at "start/old.tar"), ArchivePath.root)
      Entry = file "kept.txt" }

let private threePlaces = [ hitIn "start/a" "one.txt"; insideArchive; insideTar ]

let private inner name =
    ArchivePath.child ArchivePath.root (EntryName.create name)

let private groups =
    [ FromDisk [ fp (at "start/a/one.txt") ]
      FromArchive(fp (at "start/backup.zip"), [ inner "inner.txt" ])
      FromArchive(fp (at "start/old.tar"), [ inner "kept.txt" ]) ]

[<Fact>]
let ``marks on the disk and in two archives are copied as three groups`` () =
    let fs = mockFs ()
    let state = marked fs threePlaces

    let result = update fs CopyRequested state

    Assert.Equal<Effect list>([ CopyPlaces(groups, ToDirectory(fp (at "target"))) ], result.Effects)

[<Fact>]
let ``marks on the disk and in two archives are moved as three groups where the prompt says`` () =
    let fs = mockFs ()
    let state = marked fs threePlaces

    let asked = update fs MoveRequested state
    let result = update fs (TargetEntered(at "target")) state

    Assert.Equal<Effect list>([ PromptForTarget(fp (at "target")) ], asked.Effects)
    Assert.Equal<Effect list>([ MovePlaces(groups, ToDirectory(fp (at "target"))) ], result.Effects)

[<Fact>]
let ``marks on the disk and in two archives are deleted as three groups`` () =
    let fs = mockFs ()
    let state = marked fs threePlaces

    let result = update fs (DeleteConfirmed Permanently) state

    Assert.Equal<Effect list>([ DeletePlaces(groups, Permanently) ], result.Effects)

[<Fact>]
let ``a mark inside an archive turns the trash into deleting for good`` () =
    let fs = mockFs ()
    let state = marked fs threePlaces

    let result = update fs (DeleteRequested ToTrash) state

    Assert.Equal<Effect list>(
        [ ConfirmDelete
              { Targets =
                  [ relative "a/one.txt"; relative "backup.zip/inner.txt"; relative "old.tar/kept.txt" ]
                  |> List.map EntryName.create
                Scope = NamedEntriesOnly
                Mode = Permanently } ],
        result.Effects
    )

[<Fact>]
let ``marks in one archive alone take the way an archive panel takes`` () =
    let fs = mockFs ()
    let state = marked fs [ insideArchive ]

    let result = update fs CopyRequested state

    Assert.Equal<Effect list>(
        [ ExtractEntries(fp (at "start/backup.zip"), [ inner "inner.txt" ], fp (at "target")) ],
        result.Effects
    )

[<Fact>]
let ``F3 on a hit inside an archive streams the entry rather than a path on disk`` () =
    let fs = mockFs ()
    let state = showing fs [ insideArchive ] |> setCursor Left 1

    let result = update fs ViewRequested state

    Assert.Equal<Effect list>(
        [ ViewArchiveEntry(fp (at "start/backup.zip"), ArchivePath.child ArchivePath.root (EntryName.create "inner.txt")) ],
        result.Effects
    )

[<Fact>]
let ``F4 edits the file a hit stands for`` () =
    let fs = mockFs ()
    let state = showing fs threeDirectories |> setCursor Left 2

    let result = update fs EditRequested state

    Assert.Equal<Effect list>([ EditFile(fp (at "start/b/two.txt"), None) ], result.Effects)

[<Fact>]
let ``F7 refuses in a results panel`` () =
    let fs = mockFs ()
    let state = showing fs threeDirectories

    let result = update fs MakeDirectoryRequested state

    Assert.Equal<Effect list>(refused MakingDirectory, result.Effects)

[<Fact>]
let ``Shift+F4 refuses in a results panel`` () =
    let fs = mockFs ()
    let state = showing fs threeDirectories

    let result = update fs NewFileRequested state

    Assert.Equal<Effect list>(refused MakingFile, result.Effects)

[<Fact>]
let ``Shift+F1 refuses in a results panel`` () =
    let fs = mockFs ()
    let state = marked fs threeDirectories

    let result = update fs PackRequested state

    Assert.Equal<Effect list>(refused Packing, result.Effects)

[<Fact>]
let ``Shift+F2 refuses in a results panel`` () =
    let fs = mockFs ()
    let state = marked fs threeDirectories

    let result = update fs UnpackRequested state

    Assert.Equal<Effect list>(refused Unpacking, result.Effects)

[<Fact>]
let ``Alt+F6 refuses in a results panel`` () =
    let fs = mockFs ()
    let state = marked fs threeDirectories

    let result = update fs LinkRequested state

    Assert.Equal<Effect list>(refused Linking, result.Effects)

[<Fact>]
let ``a bare name typed for F6 refuses rather than renaming`` () =
    let fs = mockFs ()
    let state = showing fs threeDirectories |> setCursor Left 1

    let result = update fs (TargetEntered "renamed.txt") state

    Assert.Equal<Effect list>(refused Renaming, result.Effects)

let private facingResults (fs: MockFileSystem) =
    showing fs threeDirectories
    |> activate Right
    |> setCursor Right 1

[<Fact>]
let ``F5 into a results panel refuses`` () =
    let fs = mockFs ()
    fs.AddFile(at "target/four.txt", MockFileData "4")
    let state = facingResults fs |> refresh fs |> Expect.ok |> setCursor Right 1

    let result = update fs CopyRequested state

    Assert.Equal<Effect list>(refused Receiving, result.Effects)

[<Fact>]
let ``F6 into a results panel refuses`` () =
    let fs = mockFs ()
    fs.AddFile(at "target/four.txt", MockFileData "4")
    let state = facingResults fs |> refresh fs |> Expect.ok |> setCursor Right 1

    let result = update fs MoveRequested state

    Assert.Equal<Effect list>(refused Receiving, result.Effects)

[<Fact>]
let ``Shift+F1 into a results panel refuses`` () =
    let fs = mockFs ()
    fs.AddFile(at "target/four.txt", MockFileData "4")
    let state = facingResults fs |> refresh fs |> Expect.ok |> setCursor Right 1

    let result = update fs PackRequested state

    Assert.Equal<Effect list>(refused Receiving, result.Effects)

[<Fact>]
let ``Shift+F2 into a results panel refuses`` () =
    let fs = mockFs ()
    fs.AddFile(at "target/four.zip", MockFileData "4")
    let state = facingResults fs |> refresh fs |> Expect.ok |> setCursor Right 1

    let result = update fs UnpackRequested state

    Assert.Equal<Effect list>(refused Receiving, result.Effects)
