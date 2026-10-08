module Marbas.Tests.State.SelectionLikenessTests

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

let private pattern raw =
    NamePattern.tryCreate raw |> Expect.some

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddDirectory(at "start/src")
    fs.AddDirectory(at "start/backup.gz")
    fs.AddFile(at "start/archive.tar.gz", MockFileData "hi")
    fs.AddFile(at "start/archive.tar.bz2", MockFileData "hi")
    fs.AddFile(at "start/data.gz", MockFileData "hi")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "start/readme.TXT", MockFileData "hi")
    fs.AddFile(at "start/readme", MockFileData "hi")
    fs.AddFile(at "start/Makefile", MockFileData "hi")
    fs

let private loaded (fs: IFileSystem) =
    create (fp (at "start")) |> loadPanel fs Left (fp (at "start")) |> Expect.ok

let private standingOn name state =
    let index =
        (activePanel state).Entries
        |> Array.findIndex (fun entry -> EntryName.value entry.Name = name)

    setCursor Left index state

let private selectedNames state =
    PanelState.selectedEntries (activePanel state)
    |> List.map (_.Name >> EntryName.value)
    |> Set.ofList

let private everything =
    set
        [ "docs"
          "src"
          "backup.gz"
          "archive.tar.gz"
          "archive.tar.bz2"
          "data.gz"
          "notes.txt"
          "readme.TXT"
          "readme"
          "Makefile" ]

[<Fact>]
let ``marking by extension marks every entry ending the way the one under the cursor does`` () =
    let state = loaded (mockFs ()) |> standingOn "archive.tar.gz"

    let marked = applyLikeness Select Left SameExtension state

    Assert.Equal<Set<string>>(set [ "archive.tar.gz"; "backup.gz"; "data.gz" ], selectedNames marked)

[<Fact>]
let ``marking by extension ignores case`` () =
    let state = loaded (mockFs ()) |> standingOn "notes.txt"

    let marked = applyLikeness Select Left SameExtension state

    Assert.Equal<Set<string>>(set [ "notes.txt"; "readme.TXT" ], selectedNames marked)

[<Fact>]
let ``a name without a dot marks the other names without one, directories included`` () =
    let state = loaded (mockFs ()) |> standingOn "readme"

    let marked = applyLikeness Select Left SameExtension state

    Assert.Equal<Set<string>>(set [ "Makefile"; "docs"; "readme"; "src" ], selectedNames marked)

[<Fact>]
let ``marking by name marks every entry sharing the name before the last dot`` () =
    let state = loaded (mockFs ()) |> standingOn "archive.tar.gz"

    let marked = applyLikeness Select Left SameStem state

    Assert.Equal<Set<string>>(set [ "archive.tar.bz2"; "archive.tar.gz" ], selectedNames marked)

[<Fact>]
let ``marking by name ignores case and takes in the name without an extension`` () =
    let state = loaded (mockFs ()) |> standingOn "readme.TXT"

    let marked = applyLikeness Select Left SameStem state

    Assert.Equal<Set<string>>(set [ "readme"; "readme.TXT" ], selectedNames marked)

[<Fact>]
let ``unmarking by extension takes off only the entries with that extension`` () =
    let state =
        loaded (mockFs ())
        |> applyPattern Select Left (pattern "*")
        |> standingOn "notes.txt"

    let unmarked = applyLikeness Deselect Left SameExtension state

    Assert.Equal<Set<string>>(everything - set [ "notes.txt"; "readme.TXT" ], selectedNames unmarked)

[<Fact>]
let ``unmarking by name takes off only the entries with that name`` () =
    let state =
        loaded (mockFs ())
        |> applyPattern Select Left (pattern "*")
        |> standingOn "readme"

    let unmarked = applyLikeness Deselect Left SameStem state

    Assert.Equal<Set<string>>(everything - set [ "readme"; "readme.TXT" ], selectedNames unmarked)

[<Fact>]
let ``marking by extension adds to the marks already made`` () =
    let state =
        loaded (mockFs ())
        |> applyPattern Select Left (pattern "*.txt")
        |> standingOn "data.gz"

    let marked = applyLikeness Select Left SameExtension state

    Assert.Equal<Set<string>>(
        set [ "archive.tar.gz"; "backup.gz"; "data.gz"; "notes.txt"; "readme.TXT" ],
        selectedNames marked
    )

[<Fact>]
let ``the parent entry under the cursor marks nothing`` () =
    let state = loaded (mockFs ()) |> standingOn ".."

    let marked =
        state
        |> applyLikeness Select Left SameExtension
        |> applyLikeness Select Left SameStem

    Assert.Empty(selectedNames marked)

[<Fact>]
let ``the parent entry under the cursor unmarks nothing`` () =
    let state =
        loaded (mockFs ()) |> applyPattern Select Left (pattern "*") |> standingOn ".."

    let unmarked =
        state
        |> applyLikeness Deselect Left SameExtension
        |> applyLikeness Deselect Left SameStem

    Assert.Equal<Set<string>>(everything, selectedNames unmarked)
