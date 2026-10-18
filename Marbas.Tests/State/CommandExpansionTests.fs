module Marbas.Tests.State.CommandExpansionTests

open System
open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Locations
open Marbas.Find
open Marbas.FileSystem.DirectoryListing
open Marbas.State
open Marbas.State.AppState
open Marbas.State.CommandExpansion
open Marbas.State.Update
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private listFile = fp (at "session/marked.lst")

let private zipped (fs: MockFileSystem) path sources =
    Archive.write
        fs
        Operations.unattended
        { Path = fp path
          Format = ArchiveFormat.tryCreate Zip |> Expect.some
          Compression = Normal
          Password = None }
        sources
    |> ignore

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "notes")
    fs.AddFile(at "start/my file.txt", MockFileData "spaced")
    fs.AddFile(at "start/it's here.txt", MockFileData "quoted")
    fs.AddFile(at "start/docs/deep.txt", MockFileData "deep")
    fs.AddFile(at "other/report.pdf", MockFileData "report")
    fs.AddFile(at "other/plan.txt", MockFileData "plan")
    fs.AddFile(at "work/inner.txt", MockFileData "inner")
    zipped fs (at "other/backup.zip") [ fp (at "work/inner.txt") ]
    fs

let private ownName panel (entry: Entry) =
    PanelState.hitAt panel entry
    |> Option.map _.Entry.Name
    |> Option.defaultValue entry.Name
    |> EntryName.value

let private cursorOn side name state =
    let shown = panel side state
    setCursor side (shown.Entries |> Array.findIndex (fun entry -> ownName shown entry = name)) state

let private marking side names state =
    names
    |> List.fold (fun marked name -> marked |> cursorOn side name |> toggleSelection side) state

let private panels (fs: IFileSystem) =
    create (fp (at "start"))
    |> loadPanel fs Left (fp (at "start"))
    |> Expect.ok
    |> loadPanel fs Right (fp (at "other"))
    |> Expect.ok
    |> cursorOn Left "notes.txt"
    |> cursorOn Right "report.pdf"

let private rightInsideArchive (fs: IFileSystem) =
    panels fs
    |> activate Right
    |> cursorOn Right "backup.zip"
    |> enter fs
    |> Expect.ok
    |> cursorOn Right "inner.txt"
    |> activate Left

let private query =
    { What = FindWhat.create (NameMasks.read "*.txt") None FilesOnly
      Scope = FromDirectory
      Hidden = Concealed
      Links = StopAtLinks
      Archives = SkipArchives }

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

let private hits =
    [ { Location = OnDisk(fp (at "start/docs"))
        Entry = file "deep.txt" }
      { Location = OnDisk(fp (at "start"))
        Entry = file "notes.txt" }
      { Location = OnDisk(fp (at "other"))
        Entry = file "plan.txt" }
      { Location = InArchive(fp (at "other/backup.zip"), ArchivePath.root)
        Entry = file "inner.txt" } ]

let private showingResults (fs: IFileSystem) =
    let answered =
        SearchAnswered
            { Landing = Some InPanel
              Results =
                { Query = query
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

    (update fs answered (panels fs)).State

let private line raw = Expect.some (CommandLine.tryCreate raw)

let private quoted text = { Text = text; Quoting = Quoted }

let private bare text = { Text = text; Quoting = Bare }

let private insideArchive (fs: IFileSystem) name =
    let level =
        match name with
        | "" -> ArchivePath.root
        | entry -> ArchivePath.child ArchivePath.root (EntryName.create entry)

    InsideArchive(ArchivePath.spelled fs (fp (at "other/backup.zip")) level)

[<Theory>]
[<InlineData("%n", "notes.txt")>]
[<InlineData("%Ln", "notes.txt")>]
[<InlineData("%Rn", "report.pdf")>]
let ``a name placeholder puts in the name under that panel's cursor`` (placeholder: string, name: string) =
    let fs = mockFs ()

    let expanded = expand fs listFile (line $"show {placeholder}") (panels fs)

    Assert.Equal<CommandWord list>([ quoted name ], (Expect.ok expanded).Arguments)

[<Theory>]
[<InlineData("%p", "start/notes.txt")>]
[<InlineData("%Lp", "start/notes.txt")>]
[<InlineData("%Rp", "other/report.pdf")>]
[<InlineData("%s", "start/notes.txt")>]
[<InlineData("%Ls", "start/notes.txt")>]
[<InlineData("%Rs", "other/report.pdf")>]
[<InlineData("%d", "start")>]
[<InlineData("%Ld", "start")>]
[<InlineData("%Rd", "other")>]
let ``a path placeholder puts in the full path from that panel, the cursor standing for the marks when there are none``
    (placeholder: string, path: string)
    =
    let fs = mockFs ()

    let expanded = expand fs listFile (line $"show {placeholder}") (panels fs)

    Assert.Equal<CommandWord list>([ quoted (at path) ], (Expect.ok expanded).Arguments)

[<Fact>]
let ``with the right panel active the placeholders without a side speak of it`` () =
    let fs = mockFs ()
    let state = panels fs |> activate Right

    let expanded = expand fs listFile (line "show %n %p %s %d") state

    Assert.Equal<CommandWord list>(
        [ quoted "report.pdf"
          quoted (at "other/report.pdf")
          quoted (at "other/report.pdf")
          quoted (at "other") ],
        (Expect.ok expanded).Arguments
    )

[<Fact>]
let ``the marked entries are put in each as an argument of its own, the cursor left out`` () =
    let fs = mockFs ()

    let state =
        panels fs |> marking Left [ "my file.txt"; "docs" ] |> cursorOn Left "notes.txt"

    let expanded = expand fs listFile (line "pack %s") state

    Assert.Equal<CommandWord list>(
        [ quoted (at "start/docs"); quoted (at "start/my file.txt") ],
        (Expect.ok expanded).Arguments
    )

[<Fact>]
let ``the marked entries of the other panel are put in the same way`` () =
    let fs = mockFs ()
    let state = panels fs |> marking Right [ "plan.txt"; "report.pdf" ]

    let expanded = expand fs listFile (line "pack %Rs") state

    Assert.Equal<CommandWord list>(
        [ quoted (at "other/plan.txt"); quoted (at "other/report.pdf") ],
        (Expect.ok expanded).Arguments
    )

[<Fact>]
let ``the program is started once with every entry after the words typed before them`` () =
    let fs = mockFs ()
    let state = panels fs |> marking Left [ "my file.txt"; "notes.txt" ]

    let expanded = expand fs listFile (line "cmd /c del %s") state

    let command = Expect.ok expanded
    Assert.Equal(bare "cmd", command.Program)

    Assert.Equal<CommandWord list>(
        [ bare "/c"
          bare "del"
          quoted (at "start/my file.txt")
          quoted (at "start/notes.txt") ],
        command.Arguments
    )

[<Fact>]
let ``text joined to the marked entries goes to the first of them and to the last`` () =
    let fs = mockFs ()
    let state = panels fs |> marking Left [ "my file.txt"; "notes.txt" ]

    let expanded = expand fs listFile (line "tool --files=%s;") state

    Assert.Equal<CommandWord list>(
        [ quoted ("--files=" + at "start/my file.txt")
          quoted (at "start/notes.txt" + ";") ],
        (Expect.ok expanded).Arguments
    )

[<Theory>]
[<InlineData("my file.txt")>]
[<InlineData("it's here.txt")>]
let ``a name that holds a space or a quote stays one argument`` (name: string) =
    let fs = mockFs ()
    let state = panels fs |> cursorOn Left name

    let expanded = expand fs listFile (line "show %n %p") state

    Assert.Equal<CommandWord list>([ quoted name; quoted (at $"start/{name}") ], (Expect.ok expanded).Arguments)

[<UnixFact>]
let ``a name that holds a double quote stays one argument`` () =
    let fs = mockFs ()
    fs.AddFile(at "start/say \"hi\".txt", MockFileData "hi")
    let state = panels fs |> cursorOn Left "say \"hi\".txt"

    let expanded = expand fs listFile (line "show %p") state

    Assert.Equal<CommandWord list>([ quoted (at "start/say \"hi\".txt") ], (Expect.ok expanded).Arguments)

[<Fact>]
let ``words typed in quotes are one word, and remembered as quoted`` () =
    let fs = mockFs ()

    let expanded =
        expand fs listFile (line "\"C:\\Program Files\\tool.exe\" -x \"a b\"") (panels fs)

    let command = Expect.ok expanded
    Assert.Equal(quoted "C:\\Program Files\\tool.exe", command.Program)
    Assert.Equal<CommandWord list>([ bare "-x"; quoted "a b" ], command.Arguments)

[<Fact>]
let ``a doubled percent sign is one percent sign`` () =
    let fs = mockFs ()

    let expanded = expand fs listFile (line "echo 100%% %%n") (panels fs)

    Assert.Equal<CommandWord list>([ bare "100%"; bare "%n" ], (Expect.ok expanded).Arguments)

[<Theory>]
[<InlineData("%x")>]
[<InlineData("%N")>]
[<InlineData("%Lf")>]
[<InlineData("%Lx")>]
[<InlineData("%L")>]
[<InlineData("%")>]
[<InlineData("50%")>]
let ``a percent sign followed by nothing it knows is kept as it was typed`` (typed: string) =
    let fs = mockFs ()

    let expanded = expand fs listFile (line $"echo {typed}") (panels fs)

    Assert.Equal<CommandWord list>([ bare typed ], (Expect.ok expanded).Arguments)

[<Fact>]
let ``the list file is put in by its path and asks for the marked entries`` () =
    let fs = mockFs ()
    let state = panels fs |> marking Left [ "my file.txt"; "notes.txt" ]

    let expanded = expand fs listFile (line "tool @%f") state

    let command = Expect.ok expanded
    Assert.Equal<CommandWord list>([ quoted ("@" + FileSystemPath.value listFile) ], command.Arguments)

    Assert.Equal(
        Some
            { FilePath = listFile
              Listed = [ fp (at "start/my file.txt"); fp (at "start/notes.txt") ] },
        command.ListFile
    )

[<Fact>]
let ``a line without the list file asks for none`` () =
    let fs = mockFs ()

    let expanded = expand fs listFile (line "show %s") (panels fs)

    Assert.Equal(None, (Expect.ok expanded).ListFile)

[<Theory>]
[<InlineData("%Rn", "inner.txt")>]
[<InlineData("%Rp", "inner.txt")>]
[<InlineData("%Rs", "inner.txt")>]
[<InlineData("%Rd", "")>]
let ``a placeholder naming a place inside an archive refuses the run`` (placeholder: string, named: string) =
    let fs = mockFs ()

    let expanded =
        expand fs listFile (line $"show {placeholder}") (rightInsideArchive fs)

    Assert.Equal(Error(insideArchive fs named), expanded)

[<Fact>]
let ``a panel inside an archive refuses nothing that does not name it`` () =
    let fs = mockFs ()

    let expanded = expand fs listFile (line "show %p %Ld") (rightInsideArchive fs)

    Assert.Equal<CommandWord list>(
        [ quoted (at "start/notes.txt"); quoted (at "start") ],
        (Expect.ok expanded).Arguments
    )

[<Fact>]
let ``in a panel of search results a hit is named by its own name and its own path`` () =
    let fs = mockFs ()
    let state = showingResults fs |> cursorOn Left "plan.txt"

    let expanded = expand fs listFile (line "show %n %p %s") state

    Assert.Equal<CommandWord list>(
        [ quoted "plan.txt"
          quoted (at "other/plan.txt")
          quoted (at "other/plan.txt") ],
        (Expect.ok expanded).Arguments
    )

[<Fact>]
let ``hits marked in a panel of search results are put in from the directories they were found in`` () =
    let fs = mockFs ()
    let state = showingResults fs |> marking Left [ "deep.txt"; "notes.txt" ]

    let expanded = expand fs listFile (line "pack %s") state

    Assert.Equal<CommandWord list>(
        [ quoted (at "start/docs/deep.txt"); quoted (at "start/notes.txt") ],
        (Expect.ok expanded).Arguments
    )

[<Fact>]
let ``a panel of search results has no directory to put in`` () =
    let fs = mockFs ()

    let expanded = expand fs listFile (line "show %d") (showingResults fs)

    Assert.Equal(Error(NoDirectory Left), expanded)

[<Fact>]
let ``a hit found inside an archive refuses the run`` () =
    let fs = mockFs ()
    let state = showingResults fs |> cursorOn Left "inner.txt"

    let expanded = expand fs listFile (line "show %p") state

    Assert.Equal(Error(insideArchive fs "inner.txt"), expanded)

[<Theory>]
[<InlineData("%n")>]
[<InlineData("%p")>]
[<InlineData("%s")>]
[<InlineData("%f")>]
let ``with the cursor on the parent and nothing marked there is no entry to put in`` (placeholder: string) =
    let fs = mockFs ()
    let state = panels fs |> setCursor Left 0

    let expanded = expand fs listFile (line $"show {placeholder}") state

    Assert.Equal(Error(NoEntryChosen Left), expanded)

[<Fact>]
let ``a line whose first word is empty names no program`` () =
    let fs = mockFs ()

    let expanded = expand fs listFile (line "\"\" %n") (panels fs)

    Assert.Equal(Error NoProgram, expanded)

[<Fact>]
let ``a placeholder may name the program itself`` () =
    let fs = mockFs ()

    let expanded = expand fs listFile (line "%p --help") (panels fs)

    let command = Expect.ok expanded
    Assert.Equal(quoted (at "start/notes.txt"), command.Program)
    Assert.Equal<CommandWord list>([ bare "--help" ], command.Arguments)
