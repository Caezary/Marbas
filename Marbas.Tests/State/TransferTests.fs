module Marbas.Tests.State.TransferTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.State.AppState
open Marbas.Tests

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\start\docs\inner.txt", MockFileData "x")
    fs.AddFile(@"C:\start\notes.txt", MockFileData "hi")
    fs.AddFile(@"C:\start\readme.txt", MockFileData "read me")
    fs.AddDirectory @"C:\backup"
    fs

let private load (fs: IFileSystem) side path state =
    loadPanel fs side path state |> Expect.ok

let private started () =
    let fs = mockFs ()

    let state =
        create (fp @"C:\start")
        |> load fs Left (fp @"C:\start")
        |> load fs Right (fp @"C:\backup")

    fs, state

let private markedNotes state =
    state |> setCursor Left 2 |> toggleSelectionAndAdvance Left

let private finished fs results state =
    finishTransfer fs results state |> Expect.ok |> fst

[<Fact>]
let ``a finished copy lists the new entries in the other panel`` () =
    let fs, state = started ()
    let results = copyAll fs unattended [ fp @"C:\start\notes.txt" ] (fp @"C:\backup")

    let updated = finished fs results (markedNotes state)

    Assert.Contains("notes.txt", updated.Right.Entries |> List.map (_.Name >> EntryName.value))

[<Fact>]
let ``marks are dropped from the entries that were copied`` () =
    let fs, state = started ()
    let results = copyAll fs unattended [ fp @"C:\start\notes.txt" ] (fp @"C:\backup")

    let updated = finished fs results (markedNotes state)

    Assert.Empty(selectedEntries updated.Left)

[<Fact>]
let ``a mark stays on an entry whose copy failed`` () =
    let fs, state = started ()

    let results =
        [ { Name = EntryName.create "notes.txt"
            Outcome = Error(AccessDenied(fp @"C:\backup\notes.txt")) } ]

    let updated = finished fs results (markedNotes state)

    Assert.Equal<string list>([ "notes.txt" ], selectedEntries updated.Left |> List.map (_.Name >> EntryName.value))

[<Fact>]
let ``a transfer whose other panel vanished answers with an operation error`` () =
    let fs, state = started ()
    let results = copyAll fs unattended [ fp @"C:\start\notes.txt" ] (fp @"C:\backup")
    fs.Directory.Delete(@"C:\backup", true)

    let result = finishTransfer fs results state

    Assert.Equal(Error(SourceNotFound(fp @"C:\backup")), result)
