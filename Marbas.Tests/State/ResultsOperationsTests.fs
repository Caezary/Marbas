module Marbas.Tests.State.ResultsOperationsTests

open System
open System.Formats.Tar
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Profile
open Marbas.Locations
open Marbas.Find
open Marbas.FileSystem.DirectoryListing
open Marbas.Messages
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests

let private at = Root.at

let private relative = Root.relative

let private fp = FileSystemPath.create

let private zipped (fs: MockFileSystem) path sources =
    Archive.write
        fs
        Operations.unattended
        { Path = fp path
          Format = ArchiveFormat.tryCreate Zip |> Expect.some
          Compression = Normal
          Password = None }
        sources
    |> ignore

let private tarred (fs: MockFileSystem) path entry =
    use file = fs.File.Create path
    use tar = new TarWriter(file, TarEntryFormat.Pax, true)
    use content = fs.File.OpenRead(FileSystemPath.value entry)
    tar.WriteEntry(PaxTarEntry(TarEntryType.RegularFile, "kept.txt", DataStream = content))

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/a/one.txt", MockFileData "1")
    fs.AddFile(at "start/b/two.txt", MockFileData "2")
    fs.AddFile(at "start/c/three.txt", MockFileData "3")
    fs.AddFile(at "work/inner.txt", MockFileData "inner")
    fs.AddFile(at "work/same.txt", MockFileData "same")
    fs.AddFile(at "work/kept.txt", MockFileData "kept")
    fs.AddDirectory(at "target")

    zipped fs (at "start/backup.zip") [ fp (at "work/inner.txt"); fp (at "work/same.txt") ]
    tarred fs (at "start/old.tar") (fp (at "work/kept.txt"))
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
      LinkTarget = None
      Verification = Verified }

let private hitIn where name =
    { Location = OnDisk(fp (at where))
      Entry = file name }

let private insideArchive =
    { Location = InArchive(fp (at "start/backup.zip"), ArchivePath.root)
      Entry = file "inner.txt" }

let private threeDirectories =
    [ hitIn "start/a" "one.txt"
      hitIn "start/b" "two.txt"
      hitIn "start/c" "three.txt" ]

let private sources =
    [ fp (at "start/a/one.txt")
      fp (at "start/b/two.txt")
      fp (at "start/c/three.txt") ]

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
    (activePanel state).Entries |> Seq.map (_.Name >> EntryName.value) |> List.ofSeq

let private markedNames state =
    (activePanel state).Selected |> Set.map EntryName.value |> Set.toList

let private item where name outcome : Operations.ItemResult =
    { Source = Operations.ItemOnDisk(fp (at $"{where}/{name}"))
      Outcome = outcome }

let private entryItem archive name outcome : Operations.ItemResult =
    { Source = Operations.ItemInArchive(fp (at archive), ArchivePath.child ArchivePath.root (EntryName.create name))
      Outcome = outcome }

let private refused refusal =
    [ Report(Problem.Operation(Operations.NotInSearchResults refusal)) ]

[<Fact>]
let ``marked hits from three directories are copied in one batch`` () =
    let fs = mockFs ()
    let state = marked fs threeDirectories

    let result = update fs CopyRequested state

    Assert.Equal<Effect list>([ CopyPlaces([ FromDisk sources ], ToDirectory(fp (at "target"))) ], result.Effects)

[<Fact>]
let ``marked hits from three directories are moved where the prompt says`` () =
    let fs = mockFs ()
    let state = marked fs threeDirectories

    let asked = update fs MoveRequested state
    let result = update fs (TargetEntered(at "target")) state

    Assert.Equal<Effect list>([ PromptForTarget(fp (at "target")) ], asked.Effects)
    Assert.Equal<Effect list>([ MovePlaces([ FromDisk sources ], ToDirectory(fp (at "target"))) ], result.Effects)

let private deletedAs mode =
    let fs = mockFs ()
    let state = marked fs threeDirectories

    let result = update fs (DeleteConfirmed mode) state

    Assert.Equal<Effect list>([ DeletePlaces([ FromDisk sources ], mode) ], result.Effects)

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

    let result = update fs (DeleteScopeLoaded(NamedEntriesOnly, ByWiping)) state

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
        [ item "start/a" "one.txt" (Ok())
          item "start/b" "two.txt" (Ok())
          item "start/c" "three.txt" (Error(Operations.AccessDenied(fp (at "start/c/three.txt")))) ]

    let finished = update fs (DeleteFinished results) state |> _.State

    Assert.Equal<string list>([ ".."; relative "c/three.txt" ], named finished)
    Assert.Equal<string list>([ relative "c/three.txt" ], markedNames finished)

[<Fact>]
let ``what was copied loses its mark and what failed keeps it`` () =
    let fs = mockFs ()
    let state = marked fs threeDirectories

    let results =
        [ item "start/a" "one.txt" (Ok())
          item "start/b" "two.txt" (Error(Operations.TargetExists(fp (at "target/two.txt"))))
          item "start/c" "three.txt" (Ok()) ]

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
        [ InspectDelete(
              { Probed = [ fp (at "start/a/one.txt") ]
                AtLeast = NamedEntriesOnly },
              Permanently
          ) ],
        result.Effects
    )

[<Fact>]
let ``marks in one archive alone take the way an archive panel takes`` () =
    let fs = mockFs ()
    let state = marked fs [ insideArchive ]

    let result = update fs CopyRequested state

    Assert.Equal<Effect list>(
        [ CopyPlaces([ FromArchive(fp (at "start/backup.zip"), [ inner "inner.txt" ]) ], ToDirectory(fp (at "target"))) ],
        result.Effects
    )

[<Fact>]
let ``F3 on a hit inside an archive streams the entry rather than a path on disk`` () =
    let fs = mockFs ()
    let state = showing fs [ insideArchive ] |> setCursor Left 1
    let entry = ArchivePath.child ArchivePath.root (EntryName.create "inner.txt")

    let result = update fs ViewRequested state

    Assert.Equal<Effect list>(
        [ ChangeHistory(
              Remember(
                  Opened
                      { Where = Place.InArchive(fp (at "start/backup.zip"), entry)
                        How = Viewed }
              )
          )
          ViewArchiveEntry(fp (at "start/backup.zip"), entry) ],
        result.Effects
    )

[<Fact>]
let ``F4 edits the file a hit stands for`` () =
    let fs = mockFs ()
    let state = showing fs threeDirectories |> setCursor Left 2

    let result = update fs EditRequested state

    Assert.Equal<Effect list>(
        [ ChangeHistory(
              Remember(
                  Opened
                      { Where = Place.OnDisk(fp (at "start/b/two.txt"))
                        How = Edited }
              )
          )
          EditFile(fp (at "start/b/two.txt"), None) ],
        result.Effects
    )

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
    showing fs threeDirectories |> activate Right |> setCursor Right 1

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

let private bothShowingResults (fs: MockFileSystem) =
    showing fs threeDirectories
    |> activate Right
    |> update fs (sentToPanel threeDirectories)
    |> _.State
    |> activate Left

[<Fact>]
let ``F5 from one results panel into another refuses as the target`` () =
    let fs = mockFs ()
    let state = bothShowingResults fs

    let result = update fs CopyRequested state

    Assert.Equal<Effect list>(refused Receiving, result.Effects)

[<Fact>]
let ``Shift+F1 between two results panels names what the panel it stands in refuses`` () =
    let fs = mockFs ()
    let state = bothShowingResults fs

    let result = update fs PackRequested state

    Assert.Equal<Effect list>(refused Packing, result.Effects)

let private twinsOnDisk = [ hitIn "start/a" "same.txt"; hitIn "start/b" "same.txt" ]

let private twinInArchive =
    { Location = InArchive(fp (at "start/backup.zip"), ArchivePath.root)
      Entry = file "same.txt" }

let private twinsAcrossPlaces = [ hitIn "start/a" "same.txt"; twinInArchive ]

let private withTwins () =
    let fs = mockFs ()
    fs.AddFile(at "start/a/same.txt", MockFileData "a")
    fs.AddFile(at "start/b/same.txt", MockFileData "b")
    fs

[<Fact>]
let ``of two hits of one name only the one that failed keeps its mark`` () =
    let fs = withTwins ()
    let state = marked fs twinsOnDisk

    let results =
        [ item "start/a" "same.txt" (Ok())
          item "start/b" "same.txt" (Error(Operations.AccessDenied(fp (at "start/b/same.txt")))) ]

    let finished = update fs (CopyFinished results) state |> _.State

    Assert.Equal<string list>([ relative "b/same.txt" ], markedNames finished)

[<Fact>]
let ``a twin inside an archive keeps no mark when only the one on disk failed`` () =
    let fs = withTwins ()
    let state = marked fs twinsAcrossPlaces

    let results =
        [ item "start/a" "same.txt" (Error(Operations.AccessDenied(fp (at "start/a/same.txt"))))
          entryItem "start/backup.zip" "same.txt" (Ok()) ]

    let finished = update fs (CopyFinished results) state |> _.State

    Assert.Equal<string list>([ relative "a/same.txt" ], markedNames finished)
