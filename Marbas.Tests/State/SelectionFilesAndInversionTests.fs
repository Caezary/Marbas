module Marbas.Tests.State.SelectionFilesAndInversionTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Domain
open Marbas.State
open Marbas.State.AppState
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private pattern raw = NamePattern.tryCreate raw |> Expect.some

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddDirectory(at "start/src")
    fs.AddFile(at "start/a.txt", MockFileData "hi")
    fs.AddFile(at "start/b.txt", MockFileData "hi")
    fs.AddFile(at "start/c.fs", MockFileData "hi")
    fs

let private loaded (fs: IFileSystem) =
    create (fp (at "start")) |> loadPanel fs Left (fp (at "start")) |> Expect.ok

let private markedDocsAndA () =
    loaded (mockFs ())
    |> applyPattern Select Left (pattern "docs")
    |> applyPattern Select Left (pattern "a.txt")

let private selectedNames state =
    PanelState.selectedEntries (activePanel state)
    |> List.map (_.Name >> EntryName.value)
    |> Set.ofList

[<Fact>]
let ``marking all files marks every file and leaves the directories as they were`` () =
    let state = markedDocsAndA ()

    let marked = applyAllFiles Select Left state

    Assert.Equal<Set<string>>(set [ "docs"; "a.txt"; "b.txt"; "c.fs" ], selectedNames marked)

[<Fact>]
let ``unmarking all files unmarks every file and leaves the directories as they were`` () =
    let state = markedDocsAndA ()

    let unmarked = applyAllFiles Deselect Left state

    Assert.Equal<Set<string>>(set [ "docs" ], selectedNames unmarked)

[<Fact>]
let ``inverting marks what was unmarked and unmarks what was marked, directories included`` () =
    let state = markedDocsAndA ()

    let inverted = invertSelection Left state

    Assert.Equal<Set<string>>(set [ "src"; "b.txt"; "c.fs" ], selectedNames inverted)

[<Fact>]
let ``inverting nothing marks everything but the parent entry`` () =
    let state = loaded (mockFs ())

    let inverted = invertSelection Left state

    Assert.Equal<Set<string>>(set [ "docs"; "src"; "a.txt"; "b.txt"; "c.fs" ], selectedNames inverted)

[<Fact>]
let ``inverting twice gives the marks back`` () =
    let state = markedDocsAndA ()

    let twice = state |> invertSelection Left |> invertSelection Left

    Assert.Equal<Set<string>>(set [ "docs"; "a.txt" ], selectedNames twice)

[<Fact>]
let ``marking all files adds to a pattern applied before it`` () =
    let state = loaded (mockFs ()) |> applyPattern Select Left (pattern "s*")

    let marked = applyAllFiles Select Left state

    Assert.Equal<Set<string>>(set [ "src"; "a.txt"; "b.txt"; "c.fs" ], selectedNames marked)

[<Fact>]
let ``unmarking all files keeps the directories a pattern marked before it`` () =
    let state = loaded (mockFs ()) |> applyPattern Select Left (pattern "*")

    let unmarked = applyAllFiles Deselect Left state

    Assert.Equal<Set<string>>(set [ "docs"; "src" ], selectedNames unmarked)

[<Fact>]
let ``inverting turns over what a pattern marked before it`` () =
    let state = loaded (mockFs ()) |> applyPattern Select Left (pattern "*.txt")

    let inverted = invertSelection Left state

    Assert.Equal<Set<string>>(set [ "docs"; "src"; "c.fs" ], selectedNames inverted)
