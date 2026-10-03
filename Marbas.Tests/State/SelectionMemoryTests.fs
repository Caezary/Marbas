module Marbas.Tests.State.SelectionMemoryTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Domain
open Marbas.State
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private pattern raw = NamePattern.tryCreate raw |> Expect.some

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/docs/inner.txt", MockFileData "hi")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "start/readme.txt", MockFileData "hi")
    fs.AddFile(at "start/setup.fs", MockFileData "hi")
    fs

let private sending (fs: IFileSystem) messages state =
    messages |> List.fold (fun current message -> step fs message current |> fst) state

let private loaded (fs: IFileSystem) =
    create (fp (at "start")) |> loadPanel fs Left (fp (at "start")) |> Expect.ok

let private onSetup = CursorMoved { Side = Left; Index = 4 }

let private textMarked (fs: IFileSystem) =
    loaded fs |> sending fs [ PatternEntered(Select, pattern "*.txt"); onSetup ]

let private selectedNames state =
    PanelState.selectedEntries (activePanel state)
    |> List.map (_.Name >> EntryName.value)
    |> Set.ofList

let private textFiles = set [ "notes.txt"; "readme.txt" ]

let private markingCommands =
    Map
        [ "insert", SelectionToggled Downward
          "shift up", SelectionToggled Upward
          "page", SelectionToggledTo { Side = Left; Index = 1 }
          "pattern", PatternEntered(Select, pattern "*.fs")
          "unmarking pattern", PatternEntered(Deselect, pattern "notes*")
          "extension", LikenessRequested(Select, SameExtension)
          "name", LikenessRequested(Select, SameStem)
          "all files", AllFilesRequested Select
          "no files", AllFilesRequested Deselect
          "invert", InversionRequested ]

let private keepingMessages =
    Map
        [ "cursor", [ CursorMoved { Side = Left; Index = 2 } ]
          "search", [ SearchTyped 'r'; SearchBackspaced; SearchCancelled ]
          "tab", [ PanelFocused Right; PanelFocused Left ]
          "pattern prompt", [ PatternRequested Select ]
          "outside change", [ ExternalChangeDetected ] ]

[<Theory>]
[<InlineData("insert")>]
[<InlineData("shift up")>]
[<InlineData("page")>]
[<InlineData("pattern")>]
[<InlineData("unmarking pattern")>]
[<InlineData("extension")>]
[<InlineData("name")>]
[<InlineData("all files")>]
[<InlineData("no files")>]
[<InlineData("invert")>]
let ``one step back gives the marks the last marking command found`` (command: string) =
    let fs = mockFs ()
    let state = textMarked fs |> sending fs [ markingCommands[command] ]

    let recalled = sending fs [ MarksRecalled ] state

    Assert.Equal<Set<string>>(textFiles, selectedNames recalled)

[<Theory>]
[<InlineData("insert")>]
[<InlineData("shift up")>]
[<InlineData("page")>]
[<InlineData("pattern")>]
[<InlineData("unmarking pattern")>]
[<InlineData("extension")>]
[<InlineData("name")>]
[<InlineData("all files")>]
[<InlineData("no files")>]
[<InlineData("invert")>]
let ``a second step back goes forward to the marks the command made`` (command: string) =
    let fs = mockFs ()
    let state = textMarked fs |> sending fs [ markingCommands[command] ]
    let made = selectedNames state

    let twice = sending fs [ MarksRecalled; MarksRecalled ] state

    Assert.NotEqual<Set<string>>(textFiles, made)
    Assert.Equal<Set<string>>(made, selectedNames twice)

[<Fact>]
let ``with nothing to take back a step back changes nothing`` () =
    let fs = mockFs ()
    let state = loaded fs |> sending fs [ onSetup; SelectionToggled Downward; CopyRequested ]

    let recalled = sending fs [ MarksRecalled ] state

    Assert.Equal<Set<string>>(set [ "setup.fs" ], selectedNames recalled)

[<Fact>]
let ``a marking command that changes nothing leaves the memory as it was`` () =
    let fs = mockFs ()
    let state = textMarked fs |> sending fs [ PatternEntered(Select, pattern "*.zip") ]

    let recalled = sending fs [ MarksRecalled ] state

    Assert.Empty(selectedNames recalled)

[<Fact>]
let ``a file operation forgets the marks to go back to`` () =
    let fs = mockFs ()
    let state = textMarked fs |> sending fs [ CopyRequested ]

    let recalled = sending fs [ MarksRecalled ] state

    Assert.Equal<Set<string>>(textFiles, selectedNames recalled)

[<Fact>]
let ``changing directory forgets the marks to go back to`` () =
    let fs = mockFs ()

    let state =
        textMarked fs
        |> sending fs [ AllFilesRequested Deselect ]
        |> sending fs [ CursorMoved { Side = Left; Index = 1 }; EntryOpened ]
        |> sending fs [ CursorMoved { Side = Left; Index = 0 }; EntryOpened ]

    let recalled = sending fs [ MarksRecalled ] state

    Assert.Empty(selectedNames recalled)

[<Theory>]
[<InlineData("cursor")>]
[<InlineData("search")>]
[<InlineData("tab")>]
[<InlineData("pattern prompt")>]
[<InlineData("outside change")>]
let ``a message that changes nothing a user could lose keeps the marks to go back to`` (kept: string) =
    let fs = mockFs ()
    let state = textMarked fs |> sending fs keepingMessages[kept]

    let recalled = sending fs [ MarksRecalled ] state

    Assert.Empty(selectedNames recalled)

[<Fact>]
let ``names gone from the listing are left out when the marks come back`` () =
    let fs = mockFs ()
    let state = textMarked fs |> sending fs [ AllFilesRequested Deselect ]
    fs.RemoveFile(at "start/notes.txt")
    let reread = sending fs [ ExternalChangeDetected ] state

    let recalled = sending fs [ MarksRecalled ] reread

    Assert.Equal<Set<string>>(set [ "readme.txt" ], selectedNames recalled)

[<Fact>]
let ``each panel keeps its own marks to go back to`` () =
    let fs = mockFs ()

    let state =
        textMarked fs
        |> loadPanel fs Right (fp (at "start"))
        |> Expect.ok
        |> sending fs [ PanelFocused Right; PatternEntered(Select, pattern "*.fs"); PanelFocused Left ]

    let recalled = sending fs [ MarksRecalled ] state

    Assert.Empty(selectedNames recalled)
    Assert.Equal<Set<string>>(set [ "setup.fs" ], selectedNames (activate Right recalled))
