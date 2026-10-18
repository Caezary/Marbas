module Marbas.Tests.State.ClipboardPathTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Locations
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.ClipboardFixture

let private at = Root.at

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
