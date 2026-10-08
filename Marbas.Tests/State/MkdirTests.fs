module Marbas.Tests.State.MkdirTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Domain
open Marbas.State
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

let private loaded (fs: IFileSystem) =
    create (fp (at "start")) |> loadPanel fs Left (fp (at "start")) |> Expect.ok

let private names panel =
    panel.Entries |> Seq.map (_.Name >> EntryName.value) |> List.ofSeq

let private selectedNames panel =
    PanelState.selectedEntries panel
    |> Seq.map (_.Name >> EntryName.value)
    |> List.ofSeq

[<Fact>]
let ``making a directory adds it and lands the cursor on it`` () =
    let fs = mockFs ()
    let state = loaded fs

    let updated = makeDirectory fs Left (fp (at "start")) (en "work") state |> Expect.ok

    Assert.True(fs.Directory.Exists(at "start/work"))
    Assert.Contains("work", names updated.Left)
    Assert.Equal(Some "work", PanelState.cursorEntry updated.Left |> Option.map (_.Name >> EntryName.value))

[<Fact>]
let ``making a directory keeps the existing selection`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2 |> toggleSelectionMoving Downward Left

    let updated = makeDirectory fs Left (fp (at "start")) (en "work") state |> Expect.ok

    Assert.Equal<string list>([ "notes.txt" ], selectedNames state.Left)
    Assert.Equal<string list>([ "notes.txt" ], selectedNames updated.Left)

[<Fact>]
let ``an existing name is refused and leaves the panel unchanged`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = makeDirectory fs Left (fp (at "start")) (en "docs") state

    Assert.Equal(Error(TargetExists(fp (at "start/docs"))), result)

let private typed raw =
    EnteredText.tryCreate raw |> Expect.some

let private historyChanges effects =
    effects
    |> List.choose (fun effect ->
        match effect with
        | Update.ChangeHistory change -> Some change
        | _ -> None)

[<Fact>]
let ``the name a directory was given is remembered once the directory exists`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = Update.update fs (Update.NameEntered(en "work")) state

    Assert.True(fs.Directory.Exists(at "start/work"))
    Assert.Equal<HistoryChange list>([ Remember(Typed(NewDirectory, typed "work")) ], historyChanges result.Effects)

[<Fact>]
let ``a name that is refused is not remembered`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = Update.update fs (Update.NameEntered(en "docs")) state

    Assert.Empty(historyChanges result.Effects)
