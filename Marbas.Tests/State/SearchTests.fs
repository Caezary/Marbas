module Marbas.Tests.State.SearchTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Domain
open Marbas.State.AppState
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "start/readme.txt", MockFileData "hi")
    fs.AddFile(at "start/Setup.exe", MockFileData "hi")
    fs

let private loaded () =
    let fs = mockFs ()
    create (fp (at "start")) |> loadPanel fs Left (fp (at "start")) |> Expect.ok

let private typeText (text: string) side state =
    text |> Seq.fold (fun current character -> typeIntoSearch side character current) state

let private cursorName state =
    (panel Left state) |> cursorEntry |> Option.map (_.Name >> EntryName.value)

[<Fact>]
let ``the first character opens a search and moves the cursor to what it matches`` () =
    let state = loaded ()

    let searched = typeIntoSearch Left 'r' state

    Assert.Equal(Some "r", searched.Left.Search |> Option.map SearchText.value)
    Assert.Equal(Some "readme.txt", cursorName searched)

[<Fact>]
let ``a search matches anywhere in the name, whatever the case`` () =
    let state = loaded ()

    let searched = typeText "TUP" Left state

    Assert.Equal(Some "Setup.exe", cursorName searched)

[<Fact>]
let ``a search takes the first entry it matches`` () =
    let state = loaded ()

    let searched = typeText "s" Left state

    Assert.Equal(Some "docs", cursorName searched)

[<Fact>]
let ``a character that matches nothing joins the search and leaves the cursor`` () =
    let state = loaded () |> typeText "readme" Left

    let searched = typeIntoSearch Left 'z' state

    Assert.Equal(Some "readmez", searched.Left.Search |> Option.map SearchText.value)
    Assert.Equal(Some "readme.txt", cursorName searched)

[<Fact>]
let ``backspace shortens the search and moves the cursor back to what is left`` () =
    let state = loaded () |> typeText "notes" Left

    let shortened = state |> backspaceSearch Left |> backspaceSearch Left |> backspaceSearch Left

    Assert.Equal(Some "no", shortened.Left.Search |> Option.map SearchText.value)
    Assert.Equal(Some "notes.txt", cursorName shortened)

[<Fact>]
let ``backspace over the last character closes the search`` () =
    let state = loaded () |> typeText "n" Left

    let closed = backspaceSearch Left state

    Assert.Equal<SearchText option>(None, closed.Left.Search)

[<Fact>]
let ``backspace with no search open changes nothing`` () =
    let state = loaded ()

    let unchanged = backspaceSearch Left state

    Assert.Equal(state, unchanged)

[<Fact>]
let ``closing a search leaves the cursor where the search put it`` () =
    let state = loaded () |> typeText "setup" Left

    let closed = closeSearch Left state

    Assert.Equal<SearchText option>(None, closed.Left.Search)
    Assert.Equal(Some "Setup.exe", cursorName closed)

[<Fact>]
let ``switching panels closes the search`` () =
    let state = loaded () |> typeText "readme" Left

    let switched = toggleActive state

    Assert.Equal<SearchText option>(None, switched.Left.Search)

[<Fact>]
let ``reloading a panel closes its search`` () =
    let fs = mockFs ()
    let state = create (fp (at "start")) |> loadPanel fs Left (fp (at "start")) |> Expect.ok

    let reloaded =
        state |> typeText "readme" Left |> loadPanel fs Left (fp (at "start")) |> Expect.ok

    Assert.Equal<SearchText option>(None, reloaded.Left.Search)

[<Fact>]
let ``each panel searches on its own`` () =
    let fs = mockFs ()

    let state =
        create (fp (at "start"))
        |> loadPanel fs Left (fp (at "start"))
        |> Expect.ok
        |> loadPanel fs Right (fp (at "start"))
        |> Expect.ok

    let searched = typeText "readme" Left state

    Assert.Equal<SearchText option>(None, searched.Right.Search)
    Assert.Equal(0, searched.Right.Cursor)
