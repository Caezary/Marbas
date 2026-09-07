module Marbas.Tests.State.NewFileTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Domain
open Marbas.State.AppState
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private en = EntryName.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs

let private loaded (fs: MockFileSystem) =
    create (fp (at "start")) |> loadPanel fs Left (fp (at "start")) |> Expect.ok

[<Fact>]
let ``a new file is created in the directory the panel shows`` () =
    let fs = mockFs ()

    let _, path = makeFile fs Left (fp (at "start")) (en "draft.md") (loaded fs) |> Expect.ok

    Assert.Equal(fp (at "start/draft.md"), path)
    Assert.True(fs.File.Exists(at "start/draft.md"))

[<Fact>]
let ``the panel comes back with the cursor on the new file`` () =
    let fs = mockFs ()

    let state, _ = makeFile fs Left (fp (at "start")) (en "draft.md") (loaded fs) |> Expect.ok

    Assert.Equal(Some(en "draft.md"), cursorEntry state.Left |> Option.map _.Name)

[<Fact>]
let ``a name a file already carries opens that file untouched`` () =
    let fs = mockFs ()

    let _, path = makeFile fs Left (fp (at "start")) (en "notes.txt") (loaded fs) |> Expect.ok

    Assert.Equal(fp (at "start/notes.txt"), path)
    Assert.Equal("hi", fs.File.ReadAllText(at "start/notes.txt"))

[<Fact>]
let ``a name a directory already carries is refused`` () =
    let fs = mockFs ()

    let failure = makeFile fs Left (fp (at "start")) (en "docs") (loaded fs) |> Expect.error

    Assert.Equal(Operations.TargetExists(fp (at "start/docs")), failure)

[<Fact>]
let ``the marks already on the panel survive a new file`` () =
    let fs = mockFs ()
    let marked = loaded fs |> toggleSelection Left

    let state, _ = makeFile fs Left (fp (at "start")) (en "draft.md") marked |> Expect.ok

    Assert.Equal<Set<EntryName>>(marked.Left.Selected, state.Left.Selected)
