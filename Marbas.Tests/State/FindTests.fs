module Marbas.Tests.State.FindTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddDirectory(at "backup")
    fs

let private loaded (fs: IFileSystem) =
    create (fp (at "start")) |> loadPanel fs Left (fp (at "start")) |> Expect.ok

let private marking state = state |> toggleSelectionMoving Downward Left

[<Fact>]
let ``the popup is asked about the panel the key was pressed in`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = update fs FindRequested state

    Assert.Equal<Effect list>([ PromptForFind(NoDirectoryMarked, Concealed) ], result.Effects)

[<Fact>]
let ``a panel showing its hidden entries offers to search them`` () =
    let fs = mockFs ()
    let state = loaded fs |> toggleHidden fs Left |> Expect.ok

    let result = update fs FindRequested state

    Assert.Equal<Effect list>([ PromptForFind(NoDirectoryMarked, Shown) ], result.Effects)

[<Fact>]
let ``a marked directory opens the scope that walks the marks`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 1 |> marking

    let result = update fs FindRequested state

    Assert.Equal<Effect list>([ PromptForFind(DirectoriesMarked, Concealed) ], result.Effects)

[<Fact>]
let ``a marked file leaves that scope closed`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2 |> marking

    let result = update fs FindRequested state

    Assert.Equal<Effect list>([ PromptForFind(NoDirectoryMarked, Concealed) ], result.Effects)

[<Fact>]
let ``the entry under the cursor is no mark of its own`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 1

    let result = update fs FindRequested state

    Assert.Equal<Effect list>([ PromptForFind(NoDirectoryMarked, Concealed) ], result.Effects)
