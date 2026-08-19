module Marbas.Tests.State.TransferTests

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
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "start/readme.txt", MockFileData "read me")
    fs.AddDirectory(at "backup")
    fs

let private load (fs: IFileSystem) side path state =
    loadPanel fs side path state |> Expect.ok

let private started () =
    let fs = mockFs ()

    let state =
        create (fp (at "start"))
        |> load fs Left (fp (at "start"))
        |> load fs Right (fp (at "backup"))

    fs, state

let private markedNotes state =
    state |> setCursor Left 2 |> toggleSelectionMoving Downward Left

let private finished fs results state =
    finishTransfer fs results state |> Expect.ok |> fst

[<Fact>]
let ``a finished copy lists the new entries in the other panel`` () =
    let fs, state = started ()
    let results = copyAll fs unattended [ fp (at "start/notes.txt") ] (fp (at "backup"))

    let updated = finished fs results (markedNotes state)

    Assert.Contains("notes.txt", updated.Right.Entries |> List.map (_.Name >> EntryName.value))

[<Fact>]
let ``marks are dropped from the entries that were copied`` () =
    let fs, state = started ()
    let results = copyAll fs unattended [ fp (at "start/notes.txt") ] (fp (at "backup"))

    let updated = finished fs results (markedNotes state)

    Assert.Empty(selectedEntries updated.Left)

[<Fact>]
let ``a mark stays on an entry whose copy failed`` () =
    let fs, state = started ()

    let results =
        [ { Name = EntryName.create "notes.txt"
            Outcome = Error(AccessDenied(fp (at "backup/notes.txt"))) } ]

    let updated = finished fs results (markedNotes state)

    Assert.Equal<string list>([ "notes.txt" ], selectedEntries updated.Left |> List.map (_.Name >> EntryName.value))

[<Fact>]
let ``a transfer whose other panel vanished answers with an operation error`` () =
    let fs, state = started ()
    let results = copyAll fs unattended [ fp (at "start/notes.txt") ] (fp (at "backup"))
    fs.Directory.Delete(at "backup", true)

    let result = finishTransfer fs results state

    Assert.Equal(Error(SourceNotFound(fp (at "backup"))), result)
