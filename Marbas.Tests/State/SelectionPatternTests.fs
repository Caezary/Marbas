module Marbas.Tests.State.SelectionPatternTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.Tests

let private fp = FileSystemPath.create

let private pattern raw = NamePattern.tryCreate raw |> Expect.some

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start\docs"
    fs.AddFile(@"C:\start\notes.txt", MockFileData "hi")
    fs.AddFile(@"C:\start\readme.TXT", MockFileData "hi")
    fs.AddFile(@"C:\start\setup.fs", MockFileData "hi")
    fs

let private loaded (fs: IFileSystem) =
    create (fp @"C:\start") |> loadPanel fs Left (fp @"C:\start") |> Expect.ok

let private selectedNames panel =
    selectedEntries panel |> List.map (_.Name >> EntryName.value)

[<Fact>]
let ``a pattern selects every matching entry in one step`` () =
    let state = loaded (mockFs ())

    let selected = applyPattern Select Left (pattern "*.fs") state

    Assert.Equal<string list>([ "setup.fs" ], selectedNames selected.Left)

[<Fact>]
let ``pattern matching ignores case`` () =
    let state = loaded (mockFs ())

    let selected = applyPattern Select Left (pattern "*.txt") state

    Assert.Equal<string list>([ "notes.txt"; "readme.TXT" ], selectedNames selected.Left)

[<Fact>]
let ``a pattern never selects the parent entry`` () =
    let state = loaded (mockFs ())

    let selected = applyPattern Select Left (pattern "*") state

    Assert.Equal<string list>([ "docs"; "notes.txt"; "readme.TXT"; "setup.fs" ], selectedNames selected.Left)

[<Fact>]
let ``a pattern adds to what is already selected`` () =
    let state = loaded (mockFs ())

    let selected =
        state
        |> applyPattern Select Left (pattern "*.fs")
        |> applyPattern Select Left (pattern "*.txt")

    Assert.Equal<string list>([ "notes.txt"; "readme.TXT"; "setup.fs" ], selectedNames selected.Left)

[<Fact>]
let ``a pattern deselects only the entries it matches`` () =
    let state = loaded (mockFs ())

    let selected =
        state
        |> applyPattern Select Left (pattern "*")
        |> applyPattern Deselect Left (pattern "*.txt")

    Assert.Equal<string list>([ "docs"; "setup.fs" ], selectedNames selected.Left)

[<Fact>]
let ``a pattern that matches nothing leaves the selection alone`` () =
    let state = loaded (mockFs ())

    let selected =
        state
        |> applyPattern Select Left (pattern "*.fs")
        |> applyPattern Select Left (pattern "*.md")
        |> applyPattern Deselect Left (pattern "*.md")

    Assert.Equal<string list>([ "setup.fs" ], selectedNames selected.Left)
