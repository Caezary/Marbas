module Marbas.Tests.State.PanelStateTests

open System
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.State
open Marbas.Tests

let private modified = DateTimeOffset(2026, 7, 1, 12, 30, 0, TimeSpan.Zero)

let private en = EntryName.create

let private file name size =
    { Name = en name
      Kind = File
      Size = Some size
      Measured = None
      Modified = modified
      Visibility = Plain
      Executability = NotExecutable
      LinkTarget = None }

let private directory name =
    { file name 0L with
        Kind = Directory
        Size = None }

let private panel =
    { Path = OnDisk(FileSystemPath.create (Root.at "start"))
      Entries =
        [| parentEntry modified
           directory "docs"
           file "notes.txt" 30L
           file "readme.txt" 10L |]
      Cursor = 0
      Remembered = None
      Selected = Set.empty
      Sort = SortOrder.byName
      Hidden = Concealed
      Search = None }

let private standingAt index = PanelState.withCursor index panel

let private marked names =
    { panel with Selected = names |> List.map en |> Set.ofList }

let private names entries =
    entries |> List.map (_.Name >> EntryName.value)

let private cursorName p =
    PanelState.cursorEntry p |> Option.map (_.Name >> EntryName.value)

[<Fact>]
let ``the cursor entry is the one the cursor stands on`` () =
    let standing = standingAt 2

    let entry = PanelState.cursorEntry standing

    Assert.Equal(Some "notes.txt", entry |> Option.map (_.Name >> EntryName.value))

[<Fact>]
let ``a panel with nothing listed has no cursor entry`` () =
    let empty = { panel with Entries = Array.empty }

    let entry = PanelState.cursorEntry empty

    Assert.Equal(None, entry)

[<Fact>]
let ``typing into the search puts the cursor on the first entry that matches`` () =
    let searching = PanelState.typeIntoSearch 'r' panel

    let found = cursorName searching

    Assert.Equal(Some "readme.txt", found)

[<Fact>]
let ``a search that matches nothing leaves the cursor where it stood`` () =
    let searching = standingAt 1 |> PanelState.typeIntoSearch 'z'

    let found = cursorName searching

    Assert.Equal(Some "docs", found)

[<Fact>]
let ``taking back the last character of a search closes it`` () =
    let searching = PanelState.typeIntoSearch 'r' panel

    let closed = PanelState.backspaceSearch searching

    Assert.Equal(None, closed.Search)

[<Fact>]
let ``sorting keeps the cursor on the entry it stood on`` () =
    let standing = standingAt 3

    let sorted = PanelState.sortBy BySize standing

    Assert.Equal(Some "readme.txt", cursorName sorted)

[<Fact>]
let ``a run of marks stops short of its target and puts the cursor on it`` () =
    let standing = standingAt 1

    let toggled = PanelState.toggleSelectionShortOf 3 standing

    Assert.Equal<string list>([ "docs"; "notes.txt" ], PanelState.selectedEntries toggled |> names)
    Assert.Equal(3, toggled.Cursor)

[<Fact>]
let ``a run that passes the parent entry leaves it unmarked`` () =
    let toggled = PanelState.toggleSelectionShortOf 2 panel

    Assert.Equal<string list>([ "docs" ], PanelState.selectedEntries toggled |> names)

[<Fact>]
let ``a pattern marks the names it matches and never the parent entry`` () =
    let pattern = NamePattern.tryCreate "*" |> Expect.some

    let selected = PanelState.applyPattern Select pattern panel

    Assert.Equal<string list>([ "docs"; "notes.txt"; "readme.txt" ], PanelState.selectedEntries selected |> names)

[<Fact>]
let ``a pattern unmarks only the names it matches`` () =
    let pattern = NamePattern.tryCreate "*.txt" |> Expect.some

    let deselected = PanelState.applyPattern Deselect pattern (marked [ "docs"; "notes.txt" ])

    Assert.Equal<string list>([ "docs" ], PanelState.selectedEntries deselected |> names)

[<Fact>]
let ``with nothing marked the entry under the cursor is the one operated on`` () =
    let standing = standingAt 2

    let targets = PanelState.operationTargets standing

    Assert.Equal<string list>([ "notes.txt" ], names targets)

[<Fact>]
let ``the parent entry under the cursor is never operated on`` () =
    let targets = PanelState.operationTargets panel

    Assert.Empty targets

[<Fact>]
let ``marks are what is operated on, wherever the cursor stands`` () =
    let standing = { marked [ "readme.txt" ] with Cursor = 1 }

    let targets = PanelState.operationTargets standing

    Assert.Equal<string list>([ "readme.txt" ], names targets)

[<Fact>]
let ``only the marks still listed survive`` () =
    let kept = PanelState.keepSurviving (Set.ofList [ en "notes.txt"; en "gone.txt" ]) panel

    Assert.Equal<string list>([ "notes.txt" ], PanelState.selectedEntries kept |> names)

[<Fact>]
let ``a marked directory makes a search ground of marked directories`` () =
    let ground = PanelState.markedGround (marked [ "docs" ])

    Assert.Equal(DirectoriesMarked, ground)

[<Fact>]
let ``marked files alone make no search ground of directories`` () =
    let ground = PanelState.markedGround (marked [ "notes.txt" ])

    Assert.Equal(NoDirectoryMarked, ground)

[<Fact>]
let ``the name a panel stands on skips the parent entry for the one it remembers`` () =
    let remembering = { panel with Remembered = Some(en "readme.txt") }

    let standing = PanelState.standingName remembering

    Assert.Equal(Some(en "readme.txt"), standing)

[<Fact>]
let ``a cleared panel keeps its entries and loses its marks and its search`` () =
    let busy = marked [ "docs" ] |> PanelState.typeIntoSearch 'n'

    let cleared = PanelState.cleared busy

    Assert.Empty cleared.Selected
    Assert.Equal(None, cleared.Search)
    Assert.Equal(panel.Entries.Length, cleared.Entries.Length)

[<Fact>]
let ``a measured directory carries its size and takes its place in a sort by size`` () =
    let bySize = { standingAt 1 with Sort = SortOrder.create BySize Descending }

    let measured =
        PanelState.withMeasurements [ { Name = en "docs"; Size = Exact 100L } ] bySize

    Assert.Equal(Some(Exact 100L), (PanelState.cursorEntry measured |> Expect.some).Measured)
    Assert.Equal(Some "docs", cursorName measured)

let private archive = FileSystemPath.create (Root.at "start/backup.zip")

let private inArchive raw = ArchivePath.tryCreate raw |> Expect.some

let private archivePanel =
    { panel with Path = InArchive(archive, inArchive "docs") }

[<Fact>]
let ``the file under the cursor inside an archive has its place there`` () =
    let standing = PanelState.withCursor 2 archivePanel

    let place = PanelState.cursorFileInArchive standing

    Assert.Equal(Some(archive, inArchive "docs/notes.txt"), place)

[<Fact>]
let ``a directory under the cursor is no file inside an archive`` () =
    let standing = PanelState.withCursor 1 archivePanel

    let place = PanelState.cursorFileInArchive standing

    Assert.Equal(None, place)

[<Fact>]
let ``the entry under the cursor inside an archive has its place there, a directory too`` () =
    let standing = PanelState.withCursor 1 archivePanel

    let place = PanelState.cursorEntryInArchive standing

    Assert.Equal(Some(archive, inArchive "docs/docs"), place)

[<Fact>]
let ``the parent entry has no place inside an archive`` () =
    let place = PanelState.cursorEntryInArchive archivePanel

    Assert.Equal(None, place)

[<Fact>]
let ``an entry on disk has no place inside an archive`` () =
    let place = PanelState.cursorEntryInArchive (standingAt 2)

    Assert.Equal(None, place)

[<Fact>]
let ``a marked directory inside an archive is a target that reaches into it`` () =
    let marking = { archivePanel with Selected = Set.ofList [ en "docs"; en "notes.txt" ] }

    let reaches = PanelState.targetsDirectoryInArchive marking

    Assert.True reaches

[<Fact>]
let ``marked files inside an archive reach no directory`` () =
    let marking = { archivePanel with Selected = Set.ofList [ en "notes.txt" ] }

    let reaches = PanelState.targetsDirectoryInArchive marking

    Assert.False reaches

[<Fact>]
let ``a marked directory on disk is no directory inside an archive`` () =
    let reaches = PanelState.targetsDirectoryInArchive (marked [ "docs" ])

    Assert.False reaches
