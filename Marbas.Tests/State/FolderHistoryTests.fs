module Marbas.Tests.State.FolderHistoryTests

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
open Marbas.Tests.State.ArchiveFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs/inner")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddDirectory(at "backup")
    fs

let private loadedBoth (fs: IFileSystem) =
    create (fp (at "start"))
    |> loadPanel fs Left (fp (at "start"))
    |> Expect.ok
    |> loadPanel fs Right (fp (at "backup"))
    |> Expect.ok

let private visited path =
    ChangeHistory(Remember(Visited(Place.OnDisk(fp (at path)))))

let private historyChanges effects =
    effects
    |> List.choose (fun effect ->
        match effect with
        | ChangeHistory change -> Some change
        | _ -> None)

let private hitIn where name : FindHit =
    { Location = OnDisk(fp (at where))
      Entry =
        { Name = EntryName.create name
          Kind = File
          Size = None
          Measured = None
          Modified = DateTimeOffset.MinValue
          Visibility = Plain
          Executability = NotExecutable
          LinkTarget = None
          Verification = Verified } }

let private results hits =
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
      Outcome = FileSearch.Finished { Files = List.length hits; Directories = 0; Skipped = 0 }
      Cursor = 0 }

let private moment = DateTimeOffset(2026, 9, 28, 14, 0, 0, TimeSpan.Zero)

let private someHistory =
    History.apply Windows moment (Remember(Visited(Place.OnDisk(fp (at "backup"))))) History.empty

[<Fact>]
let ``walking into a directory remembers the directory walked into`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 1

    let result = update fs WalkIntoRequested state

    Assert.Contains(visited "start/docs", result.Effects)

[<Fact>]
let ``Enter on a directory remembers it`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 1

    let result = update fs EntryOpened state

    Assert.Contains(visited "start/docs", result.Effects)

[<Fact>]
let ``walking out remembers the directory walked out to`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> loadPanel fs Left (fp (at "start/docs")) |> Expect.ok |> setCursor Left 0

    let result = update fs WalkIntoRequested state

    Assert.Contains(visited "start", result.Effects)

[<Fact>]
let ``walking into an archive remembers the level inside it`` () =
    let fs = ArchiveFixture.packed ()
    let state = ArchiveFixture.loaded fs |> activate Left |> setCursor Left 1

    let result = update fs WalkIntoRequested state

    let level = Place.InArchive(ArchiveFixture.archive, ArchivePath.root)
    Assert.Contains(ChangeHistory(Remember(Visited level)), result.Effects)

[<Fact>]
let ``a path opened in the other panel is remembered`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let result = update fs (PathOpened(Right, fp (at "start/docs"))) state

    Assert.Contains(visited "start/docs", result.Effects)

[<Fact>]
let ``the root of the volume is remembered when the panel is sent there`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> loadPanel fs Left (fp (at "start/docs")) |> Expect.ok

    let result = update fs VolumeRootRequested state

    Assert.Contains(visited "", result.Effects)

[<Fact>]
let ``the directory a chosen drive lands in is remembered`` () =
    let fs = Fakes.MountedFileSystem [ Fakes.Mount.ext4 (at ""); Fakes.Mount.ext4 (at "media/stick") ]
    fs.AddDirectory(at "start")
    fs.AddDirectory(at "backup")
    fs.AddDirectory(at "media/stick/photos")
    let state = loadedBoth fs

    let result = update fs (DriveChosen(Left, MountedDrive(Fakes.driveAt (at "media/stick")))) state

    Assert.Contains(visited "media/stick", result.Effects)

[<Fact>]
let ``the directory a found hit lives in is remembered`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> loadPanel fs Left (fp (at "backup")) |> Expect.ok

    let result =
        update fs (SearchAnswered { Landing = Some(AtHit(hitIn "start/docs" "notes.txt")); Results = results [] }) state

    Assert.Contains(visited "start/docs", result.Effects)

[<Fact>]
let ``a panel showing search results remembers nothing`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let result =
        update fs (SearchAnswered { Landing = Some InPanel; Results = results [ hitIn "start" "notes.txt" ] }) state

    Assert.Empty(historyChanges result.Effects)

[<Fact>]
let ``a re-read of the directory the panel stands in remembers nothing`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let result = update fs RefreshRequested state

    Assert.Empty(historyChanges result.Effects)

[<Fact>]
let ``a sort remembers nothing`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let result = update fs (SortRequested BySize) state

    Assert.Empty(historyChanges result.Effects)

[<Fact>]
let ``showing the hidden entries remembers nothing`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let result = update fs HiddenToggled state

    Assert.Empty(historyChanges result.Effects)

[<Fact>]
let ``switching to a tab standing elsewhere remembers nothing`` () =
    let fs = mockFs ()
    let opened = (update fs TabOpened (loadedBoth fs)).State
    let walked = (update fs WalkIntoRequested (setCursor Left 1 opened)).State

    let result = update fs (TabSwitched 0) walked

    Assert.Empty(historyChanges result.Effects)

[<Fact>]
let ``closing a tab onto one standing elsewhere remembers nothing`` () =
    let fs = mockFs ()
    let opened = (update fs TabOpened (loadedBoth fs)).State
    let walked = (update fs WalkIntoRequested (setCursor Left 1 opened)).State

    let result = update fs TabClosed walked

    Assert.Empty(historyChanges result.Effects)

[<Fact>]
let ``cycling to a tab standing elsewhere remembers nothing`` () =
    let fs = mockFs ()
    let opened = (update fs TabOpened (loadedBoth fs)).State
    let walked = (update fs WalkIntoRequested (setCursor Left 1 opened)).State

    let result = update fs (TabCycled Backward) walked

    Assert.Empty(historyChanges result.Effects)

[<Fact>]
let ``a panel moved up because its directory vanished remembers nothing`` () =
    let fs = mockFs ()
    let state = loadedBoth fs
    fs.Directory.Delete(at "start", true)

    let result = update fs ExternalChangeDetected state

    Assert.Empty(historyChanges result.Effects)

[<Fact>]
let ``a directory that cannot be listed is not remembered`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let result = update fs (PathOpened(Left, fp (at "gone"))) state

    Assert.Empty(historyChanges result.Effects)

[<Fact>]
let ``the history written is what the state keeps`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let result = update fs (HistoryChanged someHistory) state

    Assert.Equal(someHistory, result.State.History)
    Assert.Empty result.Effects

[<Fact>]
let ``a history that could not be written is reported the first time`` () =
    let fs = mockFs ()
    let state = loadedBoth fs
    let error = HistoryAccessDenied(fp (at "config/history.conf"))

    let result = update fs (HistoryUnwritten error) state

    Assert.Equal<Effect list>([ ReportHistoryError error ], result.Effects)

[<Fact>]
let ``a history that could not be written is not reported again in the same session`` () =
    let fs = mockFs ()
    let error = HistoryAccessDenied(fp (at "config/history.conf"))
    let state = (update fs (HistoryUnwritten error) (loadedBoth fs)).State

    let result = update fs (HistoryUnwritten error) state

    Assert.Empty result.Effects
