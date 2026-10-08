module Marbas.Tests.State.ArchiveNavigationTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.Domain
open Marbas.State
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.ArchiveFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private en = EntryName.create

let private inside raw =
    ArchivePath.tryCreate raw |> Expect.some

let private names panel =
    panel.Entries |> Seq.map (_.Name >> EntryName.value) |> List.ofSeq

let private cursorName panel =
    PanelState.cursorEntry panel |> Option.map (_.Name >> EntryName.value)

let private opened (fs: IFileSystem) state = enter fs state |> Expect.ok

let private damagedArchive effect =
    match effect with
    | ReportListingError(ArchiveUnreadable(ArchiveDamaged(path, _))) -> Some path
    | _ -> None

[<Fact>]
let ``entering an archive opens it as the place the panel stands in`` () =
    let fs = packed ()
    let state = loaded fs |> setCursor Left 1

    let entered = opened fs state

    Assert.Equal(InArchive(archive, ArchivePath.root), entered.Left.Path)
    Assert.Equal<string list>([ ".."; "docs"; "notes.txt" ], names entered.Left)

[<Fact>]
let ``a directory inside an archive is walked into`` () =
    let fs = packed ()

    let entered = standingInside fs Left |> setCursor Left 1 |> opened fs

    Assert.Equal(InArchive(archive, inside "docs"), entered.Left.Path)
    Assert.Equal<string list>([ ".."; "deep"; "inner.txt" ], names entered.Left)

[<Fact>]
let ``a level deeper still is walked into`` () =
    let fs = packed ()

    let entered =
        standingInside fs Left
        |> setCursor Left 1
        |> opened fs
        |> setCursor Left 1
        |> opened fs

    Assert.Equal(InArchive(archive, inside "docs/deep"), entered.Left.Path)
    Assert.Equal<string list>([ ".."; "leaf.txt" ], names entered.Left)

[<Fact>]
let ``the way out of a level leads to the one above it with the cursor on the level left`` () =
    let fs = packed ()

    let left =
        standingInside fs Left
        |> setCursor Left 1
        |> opened fs
        |> setCursor Left 0
        |> opened fs

    Assert.Equal(InArchive(archive, ArchivePath.root), left.Left.Path)
    Assert.Equal(Some "docs", cursorName left.Left)

[<Fact>]
let ``the way out of the top level leaves the archive with the cursor on it`` () =
    let fs = packed ()

    let left = standingInside fs Left |> setCursor Left 0 |> opened fs

    Assert.Equal(OnDisk(fp (at "start")), left.Left.Path)
    Assert.Equal(Some "backup.zip", cursorName left.Left)

[<Fact>]
let ``a file inside an archive is not walked into`` () =
    let fs = packed ()
    let state = standingInside fs Left |> setCursor Left 2

    let entered = opened fs state

    Assert.Equal(state, entered)

[<Fact>]
let ``a file that is no archive Marbas can read is not walked into`` () =
    let fs = packed ()
    fs.AddFile(at "start/plain.txt", MockFileData "text")
    let state = loaded fs |> setCursor Left 2

    let entered = opened fs state

    Assert.Equal(Some "plain.txt", cursorName state.Left)
    Assert.Equal(state, entered)

[<Fact>]
let ``a tar gz browses like a directory as well`` () =
    let fs = packedAs TarGz (at "start/backup.tar.gz")
    let state = loaded fs |> setCursor Left 1

    let entered = opened fs state

    Assert.Equal(InArchive(fp (at "start/backup.tar.gz"), ArchivePath.root), entered.Left.Path)
    Assert.Equal<string list>([ ".."; "docs"; "notes.txt" ], names entered.Left)

[<Fact>]
let ``an archive that cannot be read is reported instead of opened`` () =
    let fs = packed ()
    fs.AddFile(at "start/broken.zip", MockFileData "not an archive at all")
    let state = loaded fs |> setCursor Left 2

    let result = update fs EntryOpened state

    Assert.Equal(OnDisk(fp (at "start")), result.State.Left.Path)
    Assert.Equal(Some(fp (at "start/broken.zip")), damagedArchive (Assert.Single result.Effects))

[<Fact>]
let ``a re-read of an archive panel picks up what the archive now holds`` () =
    let fs = packed ()
    let state = standingInside fs Left
    fs.AddFile(at "work/later.txt", MockFileData "later")

    Archive.write fs Operations.unattended (request Zip (at "start/backup.zip")) (fp (at "work/later.txt") :: sources)
    |> ignore

    let result = update fs RefreshRequested state

    Assert.Equal<string list>([ ".."; "docs"; "later.txt"; "notes.txt" ], names result.State.Left)
    Assert.Empty result.Effects

[<Fact>]
let ``the file under the cursor inside an archive is viewed out of it`` () =
    let fs = packed ()
    let state = standingInside fs Left |> setCursor Left 2

    let result = update fs ViewRequested state

    Assert.Equal<Effect list>(
        [ ChangeHistory(
              Remember(
                  Opened
                      { Where = Place.InArchive(archive, inside "notes.txt")
                        How = Viewed }
              )
          )
          ViewArchiveEntry(archive, inside "notes.txt") ],
        result.Effects
    )

[<Fact>]
let ``a directory inside an archive is measured rather than viewed`` () =
    let fs = packed ()
    let state = standingInside fs Left |> setCursor Left 1

    let result = update fs ViewRequested state

    Assert.Equal<Effect list>(
        [ MeasureTargets
              [ { Name = en "docs"
                  Location = InArchive(archive, inside "docs") } ] ],
        result.Effects
    )

[<Fact>]
let ``a marked entry inside an archive keeps its mark through a re-read`` () =
    let fs = packed ()
    let marked = standingInside fs Left |> setCursor Left 2 |> toggleSelection Left

    let result = update fs RefreshRequested marked

    Assert.Equal<Set<EntryName>>(Set.singleton (en "notes.txt"), result.State.Left.Selected)

[<Fact>]
let ``up one level inside an archive goes to the level above with the cursor on the level left`` () =
    let fs = packed ()

    let state =
        standingInside fs Left |> setCursor Left 1 |> opened fs |> setCursor Left 2

    let up = upOneLevel fs state |> Expect.ok

    Assert.Equal(InArchive(archive, ArchivePath.root), up.Left.Path)
    Assert.Equal(Some "docs", cursorName up.Left)

[<Fact>]
let ``up one level at the top of an archive leaves it with the cursor on it`` () =
    let fs = packed ()
    let state = standingInside fs Left |> setCursor Left 1

    let up = upOneLevel fs state |> Expect.ok

    Assert.Equal(OnDisk(fp (at "start")), up.Left.Path)
    Assert.Equal(Some "backup.zip", cursorName up.Left)
