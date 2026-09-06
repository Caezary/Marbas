module Marbas.Tests.State.DeleteTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.State.AppState
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/docs/inner.txt", MockFileData "x")
    fs.AddDirectory(at "start/empty")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "start/readme.txt", MockFileData "hi")
    fs

let private loaded (fs: IFileSystem) =
    create (fp (at "start")) |> loadPanel fs Left (fp (at "start")) |> Expect.ok

let private started () =
    let fs = mockFs ()
    fs, loaded fs

let private targetNames (state: AppState) = operationTargets state.Left |> List.map (_.Name >> EntryName.value)

let private deleted (fs: IFileSystem) state =
    let results = operationPaths fs (panel Left state) |> deleteAll fs
    finishDelete fs results state |> Expect.ok

[<Fact>]
let ``with nothing marked the cursor entry is the only target`` () =
    let _, state = started ()

    let targets = state |> setCursor Left 3 |> targetNames

    Assert.Equal<string list>([ "notes.txt" ], targets)

[<Fact>]
let ``the parent entry is never a target`` () =
    let _, state = started ()

    let targets = state |> setCursor Left 0 |> targetNames

    Assert.Empty targets

[<Fact>]
let ``marked entries are the targets in listing order`` () =
    let _, state = started ()

    let marked =
        state
        |> setCursor Left 4
        |> toggleSelectionMoving Downward Left
        |> setCursor Left 1
        |> toggleSelectionMoving Downward Left

    Assert.Equal<string list>([ "docs"; "readme.txt" ], targetNames marked)

[<Fact>]
let ``deleting removes the cursor entry and refreshes the panel`` () =
    let fs, state = started ()

    let updated, _ = state |> setCursor Left 3 |> deleted fs

    Assert.False(fs.File.Exists(at "start/notes.txt"))
    Assert.DoesNotContain("notes.txt", updated.Left.Entries |> List.map (_.Name >> EntryName.value))

[<Fact>]
let ``deleting a directory takes its contents with it`` () =
    let fs, state = started ()

    let updated, _ = state |> setCursor Left 1 |> deleted fs

    Assert.False(fs.Directory.Exists(at "start/docs"))
    Assert.DoesNotContain("docs", updated.Left.Entries |> List.map (_.Name >> EntryName.value))

[<Fact>]
let ``every marked entry is deleted and reported`` () =
    let fs, state = started ()

    let marked =
        state
        |> setCursor Left 3
        |> toggleSelectionMoving Downward Left
        |> toggleSelectionMoving Downward Left

    let _, results = deleted fs marked

    Assert.Equal<string list>([ "notes.txt"; "readme.txt" ], results |> List.map (_.Name >> EntryName.value))
    Assert.All(results, fun item -> Assert.Equal(Ok(), item.Outcome))
    Assert.False(fs.File.Exists(at "start/notes.txt"))
    Assert.False(fs.File.Exists(at "start/readme.txt"))

[<Fact>]
let ``a vanished entry is reported while the rest still go`` () =
    let fs, state = started ()

    let marked =
        state
        |> setCursor Left 3
        |> toggleSelectionMoving Downward Left
        |> toggleSelectionMoving Downward Left

    fs.File.Delete(at "start/notes.txt")

    let _, results = deleted fs marked

    Assert.Equal(Error(SourceNotFound(fp (at "start/notes.txt"))), results[0].Outcome)
    Assert.Equal(Ok(), results[1].Outcome)
    Assert.False(fs.File.Exists(at "start/readme.txt"))

[<Fact>]
let ``marks are dropped along with the entries they pointed at`` () =
    let fs, state = started ()

    let marked =
        state
        |> setCursor Left 3
        |> toggleSelectionMoving Downward Left
        |> setCursor Left 1
        |> toggleSelectionMoving Downward Left

    let updated, _ = deleted fs marked

    Assert.Empty(selectedEntries updated.Left)
    Assert.False(fs.Directory.Exists(at "start/docs"))
    Assert.False(fs.File.Exists(at "start/notes.txt"))

[<Fact>]
let ``a non-empty directory among the targets widens the scope`` () =
    let fs, state = started ()

    let scope = deleteScope fs (state |> setCursor Left 1).Left

    Assert.Equal(WithDirectoryContents, scope)

[<Fact>]
let ``an empty directory keeps the scope to the named entries`` () =
    let fs, state = started ()

    let scope = deleteScope fs (state |> setCursor Left 2).Left

    Assert.Equal(NamedEntriesOnly, scope)
