module Marbas.Tests.State.ClipboardTests

open System
open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.State
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "start/readme.txt", MockFileData "hi")
    fs.AddFile(at "start/a/one.txt", MockFileData "1")
    fs.AddFile(at "start/b/two.txt", MockFileData "2")
    fs

let private started (fs: IFileSystem) =
    create (fp (at "start"))
    |> loadPanel fs Left (fp (at "start"))
    |> Expect.ok
    |> loadPanel fs Right (fp (at "start"))
    |> Expect.ok

let private indexOf name state =
    (activePanel state).Entries
    |> Array.findIndex (fun entry -> EntryName.value entry.Name = name)

let private standingOn name state = setCursor Left (indexOf name state) state

let private markedOn names state =
    names
    |> List.fold (fun marked name -> marked |> standingOn name |> toggleSelection Left) state

let private copying lines =
    [ PutOnClipboard(ClipboardText.tryCreate lines |> Expect.some) ]

let private file name =
    { Name = EntryName.create name
      Kind = File
      Size = Some 1L
      Measured = None
      Modified = DateTimeOffset.MinValue
      Visibility = Plain
      Executability = NotExecutable
      LinkTarget = None
      Verification = Verified }

let private hitIn where name =
    { Location = OnDisk(fp (at where))
      Entry = file name }

let private sentToPanel hits =
    SearchAnswered
        { Landing = Some InPanel
          Results =
            { Query =
                { What = FindWhat.create (NameMasks.read "*.txt") None FilesOnly
                  Scope = FromDirectory
                  Hidden = Concealed
                  Links = StopAtLinks
                  Archives = SkipArchives }
              Ground =
                { Roots = [ OnDisk(fp (at "start")) ]
                  OtherRoots = [] }
              Hits = hits
              Outcome =
                FileSearch.Finished
                    { Files = List.length hits
                      Directories = 0
                      Skipped = 0 }
              Cursor = 0 } }

let private everything = NamePattern.tryCreate "*" |> Expect.some

[<Fact>]
let ``the name of the entry under the cursor is put on the clipboard`` () =
    let fs = mockFs ()
    let state = started fs |> standingOn "notes.txt"

    let result = update fs NamesCopyRequested state

    Assert.Equal<Effect list>(copying [ "notes.txt" ], result.Effects)

[<Fact>]
let ``a directory is named without a separator after it`` () =
    let fs = mockFs ()
    let state = started fs |> standingOn "docs"

    let result = update fs NamesCopyRequested state

    Assert.Equal<Effect list>(copying [ "docs" ], result.Effects)

[<Fact>]
let ``the marked entries are named in the order of the listing, whatever the cursor stands on`` () =
    let fs = mockFs ()
    let state = started fs |> markedOn [ "readme.txt"; "docs" ] |> standingOn "notes.txt"

    let result = update fs NamesCopyRequested state

    Assert.Equal<Effect list>(copying [ "docs"; "readme.txt" ], result.Effects)

[<Fact>]
let ``nothing is copied with the parent entry under the cursor and nothing marked`` () =
    let fs = mockFs ()
    let state = started fs |> standingOn ".."

    let result = update fs NamesCopyRequested state

    Assert.Empty result.Effects

[<Fact>]
let ``the marks stay where they were after the names are copied`` () =
    let fs = mockFs ()
    let state = started fs |> markedOn [ "readme.txt"; "docs" ]

    let result = update fs NamesCopyRequested state

    Assert.Equal<Set<EntryName>>(state.Left.Selected, result.State.Left.Selected)

[<Fact>]
let ``each hit in a panel of search results gives its own name`` () =
    let fs = mockFs ()

    let state =
        started fs
        |> update fs (sentToPanel [ hitIn "start/a" "one.txt"; hitIn "start/b" "two.txt" ])
        |> _.State
        |> update fs (PatternEntered(Select, everything))
        |> _.State

    let result = update fs NamesCopyRequested state

    Assert.Equal<Effect list>(copying [ "one.txt"; "two.txt" ], result.Effects)

[<Fact>]
let ``the entries inside an archive are named as the archive holds them`` () =
    let fs = ArchiveFixture.packed ()

    let state =
        ArchiveFixture.loaded fs
        |> standInside fs Left (InArchive(ArchiveFixture.archive, ArchivePath.root))
        |> Expect.ok
        |> update fs (PatternEntered(Select, everything))
        |> _.State

    let result = update fs NamesCopyRequested state

    Assert.Equal<Effect list>(copying [ "docs"; "notes.txt" ], result.Effects)

[<Fact>]
let ``a clipboard the system does not offer is reported the first time`` () =
    let fs = mockFs ()

    let result = update fs (ClipboardUnwritten ClipboardUnavailable) (started fs)

    Assert.Equal<Effect list>([ ReportClipboardError ClipboardUnavailable ], result.Effects)

[<Fact>]
let ``a clipboard the system does not offer is not reported again in the same session`` () =
    let fs = mockFs ()
    let reported = update fs (ClipboardUnwritten ClipboardUnavailable) (started fs)

    let result = update fs (ClipboardUnwritten ClipboardUnavailable) reported.State

    Assert.Empty result.Effects

[<Fact>]
let ``a clipboard that failed is reported every time`` () =
    let fs = mockFs ()
    let failure = ClipboardFailed "The clipboard is held by another program."
    let reported = update fs (ClipboardUnwritten failure) (started fs)

    let result = update fs (ClipboardUnwritten failure) reported.State

    Assert.Equal<Effect list>([ ReportClipboardError failure ], result.Effects)
