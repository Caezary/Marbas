module Marbas.Tests.State.LinkTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private names (entries: DirectoryListing.Entry seq) =
    entries |> Seq.map (_.Name >> EntryName.value) |> List.ofSeq

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "start/setup.fs", MockFileData "hi")
    fs.AddDirectory(at "backup")
    fs

let private loadedBoth (fs: IFileSystem) =
    create (fp (at "start"))
    |> loadPanel fs Left (fp (at "start"))
    |> Expect.ok
    |> loadPanel fs Right (fp (at "backup"))
    |> Expect.ok

let private marking state =
    state |> toggleSelectionMoving Downward Left

let private symbolic target location =
    { Kind = Symbolic
      Target = fp target
      Location = fp location }

[<Fact>]
let ``a link for the entry under the cursor is offered in the other panel under its name`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2

    let result = update fs LinkRequested state

    Assert.Equal<Effect list>(
        [ PromptForLink([ fp (at "start/notes.txt") ], fp (at "start"), fp (at "backup/notes.txt")) ],
        result.Effects
    )

[<Fact>]
let ``links for several marks are offered in the other panel's directory alone`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 1 |> marking |> marking

    let result = update fs LinkRequested state

    Assert.Equal<Effect list>(
        [ PromptForLink([ fp (at "start/docs"); fp (at "start/notes.txt") ], fp (at "start"), fp (at "backup")) ],
        result.Effects
    )

[<Fact>]
let ``the way out is not an entry to link`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let result = update fs LinkRequested state

    Assert.Empty result.Effects

[<Fact>]
let ``an entered link is made where it was typed, of the kind that was chosen`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2

    let result =
        update fs (LinkEntered { Target = at "backup/twin.txt"; Kind = Symbolic }) state

    Assert.Equal<Effect list>([ MakeLinks [ symbolic (at "start/notes.txt") (at "backup/twin.txt") ] ], result.Effects)

[<Fact>]
let ``an empty answer makes no link`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2

    let result = update fs (LinkEntered { Target = " "; Kind = Symbolic }) state

    Assert.Empty result.Effects

[<Fact>]
let ``a made link is shown in the other panel with the cursor on it`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2
    fs.File.CreateSymbolicLink(at "backup/twin.txt", at "start/notes.txt") |> ignore
    let results = [ { Source = Operations.ItemOnDisk(fp (at "start/notes.txt")); Outcome = Ok() } ]

    let result = update fs (LinksMade([ fp (at "backup/twin.txt") ], results)) state

    Assert.Equal<string list>([ ".."; "twin.txt" ], names result.State.Right.Entries)
    Assert.Equal(1, result.State.Right.Cursor)
    Assert.Equal(Some(fp (at "start/notes.txt")), result.State.Right.Entries[1].LinkTarget)
    Assert.Empty result.Effects

[<Fact>]
let ``a link made beside its entry is shown in the active panel with the cursor on it`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2
    fs.File.CreateSymbolicLink(at "start/twin.txt", at "start/notes.txt") |> ignore
    let results = [ { Source = Operations.ItemOnDisk(fp (at "start/notes.txt")); Outcome = Ok() } ]

    let result = update fs (LinksMade([ fp (at "start/twin.txt") ], results)) state

    Assert.Equal("twin.txt", names result.State.Left.Entries |> List.item result.State.Left.Cursor)

[<Fact>]
let ``a link that could not be made is reported and keeps its mark`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2 |> marking
    let refused = Error(LinkRefused(HardLinkToDirectory, fp (at "backup/notes.txt")))
    let results = [ { Source = Operations.ItemOnDisk(fp (at "start/notes.txt")); Outcome = refused } ]

    let result = update fs (LinksMade([], results)) state

    Assert.Equal<Effect list>([ ReportItemFailures results ], result.Effects)
    Assert.Equal<string list>([ "notes.txt" ], selectedEntries result.State.Left |> names)
