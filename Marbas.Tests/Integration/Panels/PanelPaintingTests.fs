module Marbas.Tests.Integration.Panels.PanelPaintingTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Drawing
open Terminal.Gui.Drivers
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.Archives
open Marbas.Locations
open Marbas.Find
open Marbas.FileSystem.DirectoryListing
open Marbas.Ui.Theme
open Marbas.Ui.Keys
open Marbas.Ui.Panels
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/readme.txt", MockFileData "hi")
    fs.AddFile(at "start/setup.exe", MockFileData "hi")
    fs.AddFile(at "start/sources.zip", MockFileData "hi")
    fs

let private window () =
    focusedWindow (Fakes.dependencies (mockFs ()) (at "start"))

let private paintedRow (list: ListView) row =
    let args = ListViewRowEventArgs row
    list.OnRowRender args
    args.RowAttribute

[<Fact>]
let ``every kind of entry is painted with its own colour`` () =
    use window = window ()
    let list = leftList window
    list.SelectedItem <- 0

    let painted = [ 1..4 ] |> List.map (paintedRow list)

    Assert.Equal<Nullable<Attribute> list>(
        [ Nullable ClassicScheme.directoryEntry
          Nullable ClassicScheme.plainEntry
          Nullable ClassicScheme.executableEntry
          Nullable ClassicScheme.archiveEntry ],
        painted
    )

[<Fact>]
let ``a link is painted with the link colour`` () =
    let fs = mockFs ()
    fs.Directory.CreateSymbolicLink(at "start/twin", at "start/docs") |> ignore
    use window = focusedWindow (Fakes.dependencies fs (at "start"))
    let list = leftList window
    list.SelectedItem <- 0

    let painted = paintedRow list 2

    Assert.Equal(Nullable ClassicScheme.linkEntry, painted)

[<Fact>]
let ``the parent entry is painted like a directory`` () =
    use window = window ()
    let list = leftList window
    list.SelectedItem <- 1

    let painted = paintedRow list 0

    Assert.Equal(Nullable ClassicScheme.directoryEntry, painted)

[<Fact>]
let ``a marked entry is painted with the colour of a mark`` () =
    use window = window ()
    let list = leftList window
    list.SelectedItem <- 1
    press list KeyMap.selectionKeys.Head

    let painted = paintedRow list 1

    Assert.Equal(Nullable ClassicScheme.markedEntry, painted)

[<Fact>]
let ``a marked entry under the cursor keeps the colour of the cursor`` () =
    use window = window ()
    let list = leftList window
    list.SelectedItem <- 1
    press list KeyMap.selectionKeys.Head
    list.SelectedItem <- 1

    let painted = paintedRow list 1

    Assert.False painted.HasValue

[<Fact>]
let ``a marked entry under the cursor of an unfocused panel is painted with the colour of a mark`` () =
    use window = window ()
    let list = leftList window
    list.SelectedItem <- 1
    press list KeyMap.selectionKeys.Head
    list.SelectedItem <- 1

    window.AdvanceFocus(NavigationDirection.Forward, Nullable TabBehavior.TabStop)
    |> ignore

    let painted = paintedRow list 1

    Assert.False(list.HasFocus, "the left list should have given the focus away")
    Assert.Equal(Nullable ClassicScheme.markedEntry, painted)

let private longListing () =
    let fs = MockFileSystem()

    [ 1..40 ]
    |> List.iter (fun number -> fs.AddFile(at $"start/file{number:D2}.txt", MockFileData "hi"))

    fs

let private markedAndLaidOut () =
    let window = focusedWindow (Fakes.dependencies (longListing ()) (at "start"))
    window.Layout(System.Drawing.Size(80, 16)) |> ignore
    let list = leftList window
    list.SelectedItem <- 1
    press list KeyMap.selectionKeys.Head
    press list KeyMap.selectionKeys.Head
    list.SelectedItem <- 20
    window

[<Theory>]
[<InlineData(KeyCode.CursorUp)>]
[<InlineData(KeyCode.CursorDown)>]
[<InlineData(KeyCode.PageUp)>]
[<InlineData(KeyCode.PageDown)>]
[<InlineData(KeyCode.Home)>]
[<InlineData(KeyCode.End)>]
[<InlineData(KeyCode.CursorLeft)>]
[<InlineData(KeyCode.CursorRight)>]
let ``the marks stay in the listing when the cursor moves`` (moving: KeyCode) =
    use window = markedAndLaidOut ()
    let list = leftList window

    press list (Key moving)

    Assert.NotEqual(20, list.SelectedItem.GetValueOrDefault 20)
    Assert.Equal<bool list>([ true; true ], [ for row in 1..2 -> (Expect.notNull list.Source).IsMarked row ])

[<Theory>]
[<InlineData(KeyCode.CursorUp)>]
[<InlineData(KeyCode.CursorDown)>]
[<InlineData(KeyCode.PageUp)>]
[<InlineData(KeyCode.PageDown)>]
[<InlineData(KeyCode.Home)>]
[<InlineData(KeyCode.End)>]
[<InlineData(KeyCode.CursorLeft)>]
[<InlineData(KeyCode.CursorRight)>]
let ``the marked rows are still painted as marked when the cursor moves`` (moving: KeyCode) =
    use window = markedAndLaidOut ()
    let list = leftList window
    press list (Key moving)

    let painted = [ 1; 2 ] |> List.map (paintedRow list)

    Assert.Equal<Nullable<Attribute> list>(
        [ Nullable ClassicScheme.markedEntry; Nullable ClassicScheme.markedEntry ],
        painted
    )

[<Fact>]
let ``the entry under the cursor keeps the colour of the cursor`` () =
    use window = window ()
    let list = leftList window
    list.SelectedItem <- 2

    let painted = paintedRow list 2

    Assert.True(list.HasFocus, "the focused panel's list should hold the focus")
    Assert.False painted.HasValue

[<Fact>]
let ``the cursor of an unfocused panel gives its row back to the colours`` () =
    use window = window ()
    let list = rightList window
    list.SelectedItem <- 2

    let painted = paintedRow list 2

    Assert.Equal(Nullable ClassicScheme.plainEntry, painted)

[<Fact>]
let ``moving the cursor leaves the rows of the panel as they were`` () =
    use window = window ()
    let list = leftList window
    let painted = list.Source

    list.SelectedItem <- 2

    Assert.Same(painted, list.Source)
    Assert.Equal(2, list.SelectedItem.GetValueOrDefault -1)

[<Fact>]
let ``the panel nobody touched keeps the rows it had`` () =
    use window = window ()
    let untouched = (rightList window).Source
    let list = leftList window
    list.SelectedItem <- 1

    press list Terminal.Gui.Input.Key.Enter

    Assert.Equal(at "start/docs", shownPath FilePanel.LeftId window)
    Assert.Same(untouched, (rightList window).Source)

[<Fact>]
let ``marking an entry paints the rows again`` () =
    use window = window ()
    let list = leftList window
    list.SelectedItem <- 1
    let painted = list.Source

    press list KeyMap.selectionKeys.Head

    Assert.NotSame(painted, list.Source)
    Assert.True((Expect.notNull list.Source).IsMarked 1)

[<Fact>]
let ``a directory opened paints the rows again`` () =
    use window = window ()
    let list = leftList window
    list.SelectedItem <- 1
    let painted = list.Source

    press list Terminal.Gui.Input.Key.Enter

    Assert.NotSame(painted, list.Source)

let private fp = FileSystemPath.create

let private withArchives () =
    let fs = mockFs ()
    fs.AddFile(at "work/inner.txt", MockFileData "inner")
    fs.AddFile(at "start/broken.zip", MockFileData "not a zip")

    Archive.write
        fs
        Operations.unattended
        { Path = fp (at "start/data.zip")
          Format = ArchiveFormat.tryCreate Zip |> Expect.some
          Compression = Normal
          Password = None }
        [ fp (at "work/inner.txt") ]
    |> ignore

    fs

let private query =
    { What = FindWhat.create (NameMasks.read "*.txt") None FilesOnly
      Scope = FromDirectory
      Hidden = Concealed
      Links = StopAtLinks
      Archives = SearchArchives }

let private hit location name =
    { Location = location
      Entry =
        { Name = EntryName.create name
          Kind = File
          Size = Some 2L
          Measured = None
          Modified = DateTimeOffset.MinValue
          Visibility = Plain
          Executability = NotExecutable
          LinkTarget = None
          Verification = Verified } }

let private acrossArchives =
    [ hit (OnDisk(fp (at "start"))) "readme.txt"
      hit (InArchive(fp (at "start/data.zip"), ArchivePath.root)) "inner.txt"
      hit (InArchive(fp (at "start/broken.zip"), ArchivePath.root)) "lost.txt"
      hit (InArchive(fp (at "start/broken.zip"), ArchivePath.root)) "other.txt" ]

let private sentToPanel =
    { Landing = Some InPanel
      Results =
        { Query = query
          Ground =
            { Roots = [ OnDisk(fp (at "start")) ]
              OtherRoots = [] }
          Hits = acrossArchives
          Outcome =
            FileSearch.Finished
                { Files = 4
                  Directories = 0
                  Skipped = 0 }
          Cursor = 0 } }

let private resultsWindow (reported: ResizeArray<string>) =
    let window =
        focusedWindow
            { Fakes.dependencies (withArchives ()) (at "start") with
                Prompts.AskFind =
                    fun _ ->
                        Some
                            { Choice = Search query
                              Masks = "*.*"
                              Text = "" }
                Prompts.ReportError = fun report -> reported.Add report.Message
                Runners.Search = fun request -> request.Finish sentToPanel }

    pressKey window KeyMap.findKey
    window

[<Fact>]
let ``hits inside an archive that cannot be read are painted unverified among the hits that could be checked`` () =
    use window = resultsWindow (ResizeArray())
    let list = leftList window
    list.SelectedItem <- 0

    let painted = [ 1..4 ] |> List.map (paintedRow list)

    Assert.Equal<Nullable<Attribute> list>(
        [ Nullable ClassicScheme.unverifiedEntry
          Nullable ClassicScheme.unverifiedEntry
          Nullable ClassicScheme.plainEntry
          Nullable ClassicScheme.plainEntry ],
        painted
    )

[<Fact>]
let ``an archive that cannot be read is reported once when its hits are shown`` () =
    let reported = ResizeArray<string>()

    use window = resultsWindow reported

    Assert.Contains(at "start/broken.zip", Assert.Single reported)
