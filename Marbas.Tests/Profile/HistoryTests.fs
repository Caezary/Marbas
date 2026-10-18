module Marbas.Tests.Profile.HistoryTests

open System
open System.Collections.Generic
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Profile
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private historyPath = fp (at "config/history.conf")

let private moment minute =
    DateTimeOffset(2026, 9, 28, 14, minute, 0, TimeSpan.FromHours 2.0)

let private onDisk path = Place.OnDisk(fp (at path))

let private inArchive archive inside =
    Place.InArchive(fp (at archive), Expect.some (ArchivePath.tryCreate inside))

let private text raw = Expect.some (EnteredText.tryCreate raw)

let private launch target elevation =
    { Target = fp (at target)
      Program = None
      WorkingDirectory = fp (at "tools")
      Elevation = elevation }

let private word text quoting = { Text = text; Quoting = quoting }

let private packing: CommandRun =
    { Label = Expect.some (MenuLabel.tryCreate "Pack")
      Command =
        { Program = word "7z" Bare
          Arguments =
            [ word "a" Bare
              word (at "work/out.7z") Quoted
              word ("@" + at "session/marked-1.txt") Quoted ]
          ListFile =
            Some
                { FilePath = fp (at "session/marked-1.txt")
                  Listed = [ fp (at "work/a.txt"); fp (at "work/b c.txt") ] } }
      Run = InShellKeptOpen
      Elevation = AsAdministrator
      WorkingDirectory = fp (at "work") }

let private remember platform minute item history =
    History.apply platform (moment minute) (Remember item) history

let private items (entries: Remembered<'item> list) = entries |> List.map _.Item

let private sample: History =
    { Folders =
        [ { Item = onDisk "work"; At = moment 5 }
          { Item = inArchive "backup.zip" "docs/2024"
            At = moment 4 } ]
      Launches =
        [ { Item =
              StartedFile
                  { launch "tools/setup.exe" AsAdministrator with
                      Program = ProgramPath.tryCreate "installer.exe" }
            At = moment 3 }
          { Item = StartedFile(launch "notes.md" AsUser)
            At = moment 2 }
          { Item = StartedCommand packing
            At = moment 9 } ]
      FileVisits =
        [ { Item =
              { Where = onDisk "notes.md"
                How = Edited }
            At = moment 1 }
          { Item =
              { Where = inArchive "backup.zip" "docs/readme.txt"
                How = Viewed }
            At = moment 0 } ]
      Inputs =
        Map.ofList
            [ CopyOrMoveTarget,
              [ { Item = text (at "backup")
                  At = moment 6 } ]
              FindMasks,
              [ { Item = text "*.fs;*.md"
                  At = moment 7 }
                { Item = text "a=b"; At = moment 8 } ] ] }

let private write (fs: MockFileSystem) lines =
    fs.AddFile(FileSystemPath.value historyPath, MockFileData(String.concat "\n" lines))

let private unwritten error =
    match error with
    | HistoryAccessDenied path -> path
    | HistoryFailed(path, _) -> path

[<Fact>]
let ``a place remembered goes on top with the moment it was used`` () =
    let history = History.empty |> remember Windows 1 (Visited(onDisk "work"))

    let remembered = remember Windows 2 (Visited(onDisk "notes")) history

    Assert.Equal<Remembered<Place> list>(
        [ { Item = onDisk "notes"; At = moment 2 }
          { Item = onDisk "work"; At = moment 1 } ],
        remembered.Folders
    )

[<Fact>]
let ``a place remembered again moves to the top rather than repeating`` () =
    let history =
        History.empty
        |> remember Windows 1 (Visited(onDisk "work"))
        |> remember Windows 2 (Visited(onDisk "notes"))

    let remembered = remember Windows 3 (Visited(onDisk "work")) history

    Assert.Equal<Remembered<Place> list>(
        [ { Item = onDisk "work"; At = moment 3 }
          { Item = onDisk "notes"; At = moment 2 } ],
        remembered.Folders
    )

[<Fact>]
let ``on Windows a place spelled in another case is the same place`` () =
    let history = History.empty |> remember Windows 1 (Visited(onDisk "Work"))

    let remembered = remember Windows 2 (Visited(onDisk "work")) history

    Assert.Equal<Place list>([ onDisk "work" ], items remembered.Folders)

[<Fact>]
let ``on Unix a place spelled in another case is another place`` () =
    let history = History.empty |> remember Unix 1 (Visited(onDisk "Work"))

    let remembered = remember Unix 2 (Visited(onDisk "work")) history

    Assert.Equal<Place list>([ onDisk "work"; onDisk "Work" ], items remembered.Folders)

[<Fact>]
let ``a level inside an archive is another place than the archive's own directory`` () =
    let history = History.empty |> remember Windows 1 (Visited(onDisk "backup.zip"))

    let remembered = remember Windows 2 (Visited(inArchive "backup.zip" "docs")) history

    Assert.Equal<Place list>([ inArchive "backup.zip" "docs"; onDisk "backup.zip" ], items remembered.Folders)

[<Fact>]
let ``a file viewed and then edited is one entry, now edited`` () =
    let history =
        History.empty
        |> remember
            Windows
            1
            (Opened
                { Where = onDisk "notes.md"
                  How = Viewed })

    let remembered =
        remember
            Windows
            2
            (Opened
                { Where = onDisk "notes.md"
                  How = Edited })
            history

    Assert.Equal<FileVisit list>(
        [ { Where = onDisk "notes.md"
            How = Edited } ],
        items remembered.FileVisits
    )

[<Fact>]
let ``a file started again another way is one entry, started the latest way`` () =
    let history =
        History.empty |> remember Windows 1 (Launched(launch "setup.exe" AsUser))

    let remembered =
        remember Windows 2 (Launched(launch "setup.exe" AsAdministrator)) history

    Assert.Equal<Started list>([ StartedFile(launch "setup.exe" AsAdministrator) ], items remembered.Launches)

[<Fact>]
let ``the same text typed into two kinds of field is remembered by both`` () =
    let history =
        History.empty |> remember Windows 1 (Typed(NewDirectory, text "build"))

    let remembered = remember Windows 2 (Typed(NewFile, text "build")) history

    Assert.Equal<EnteredText list>([ text "build" ], items remembered.Inputs[NewDirectory])
    Assert.Equal<EnteredText list>([ text "build" ], items remembered.Inputs[NewFile])

[<Fact>]
let ``text is the same text only when it is spelled the same`` () =
    let history =
        History.empty |> remember Windows 1 (Typed(SelectionPattern, text "*.TXT"))

    let remembered = remember Windows 2 (Typed(SelectionPattern, text "*.txt")) history

    Assert.Equal<EnteredText list>([ text "*.txt"; text "*.TXT" ], items remembered.Inputs[SelectionPattern])

[<Fact>]
let ``a list keeps the hundred most recent entries`` () =
    let full =
        [ 0..99 ]
        |> List.fold (fun history number -> remember Unix 0 (Visited(onDisk $"dir{number}")) history) History.empty

    let remembered = remember Unix 1 (Visited(onDisk "newest")) full

    Assert.Equal(100, remembered.Folders.Length)
    Assert.Equal(onDisk "newest", remembered.Folders.Head.Item)
    Assert.DoesNotContain(onDisk "dir0", items remembered.Folders)

[<Fact>]
let ``a place whose path breaks a line is not remembered`` () =
    let remembered =
        remember Unix 1 (Visited(Place.OnDisk(fp "/tmp/first\nsecond"))) History.empty

    Assert.Equal(History.empty, remembered)

[<Fact>]
let ``a forgotten entry leaves its list, found the way remembering finds it`` () =
    let history =
        History.empty
        |> remember Windows 1 (Visited(onDisk "work"))
        |> remember Windows 2 (Visited(onDisk "notes"))

    let forgotten =
        History.apply Windows (moment 3) (Forget(Visited(onDisk "WORK"))) history

    Assert.Equal<Place list>([ onDisk "notes" ], items forgotten.Folders)

[<Fact>]
let ``clearing a list empties that list and no other`` () =
    let cleared =
        History.apply Windows (moment 9) (ClearList(InputHistory FindMasks)) sample

    Assert.Empty(History.inputs FindMasks cleared)
    Assert.Equal<Remembered<EnteredText> list>(sample.Inputs[CopyOrMoveTarget], History.inputs CopyOrMoveTarget cleared)
    Assert.Equal<Remembered<Place> list>(sample.Folders, cleared.Folders)

[<Fact>]
let ``a kind of field nothing was typed into has an empty list`` () =
    let typed = History.inputs OpenWithProgram sample

    Assert.Empty typed

[<Fact>]
let ``a command run from the menu goes on the same list as the files that were started`` () =
    let history =
        History.empty |> remember Windows 1 (Launched(launch "setup.exe" AsUser))

    let remembered = remember Windows 2 (Ran packing) history

    Assert.Equal<Started list>(
        [ StartedCommand packing; StartedFile(launch "setup.exe" AsUser) ],
        items remembered.Launches
    )

[<Fact>]
let ``a command run again exactly as before is one entry`` () =
    let history = History.empty |> remember Windows 1 (Ran packing)

    let remembered = remember Windows 2 (Ran packing) history

    Assert.Equal<Remembered<Started> list>(
        [ { Item = StartedCommand packing
            At = moment 2 } ],
        remembered.Launches
    )

[<Fact>]
let ``a command run another way is an entry of its own`` () =
    let history = History.empty |> remember Windows 1 (Ran packing)

    let remembered = remember Windows 2 (Ran { packing with Run = Directly }) history

    Assert.Equal(2, remembered.Launches.Length)

[<Fact>]
let ``a forgotten command run is gone`` () =
    let history = History.empty |> remember Windows 1 (Ran packing)

    let forgotten = History.apply Windows (moment 2) (Forget(Ran packing)) history

    Assert.Empty forgotten.Launches

[<Fact>]
let ``a command run is written with its label, its words, its list file and the way it ran`` () =
    let lines = History.format sample

    Assert.Subset(
        HashSet lines,
        HashSet
            [ "run.3=Pack"
              "run.3.kind=command"
              "run.3.command=7z"
              "run.3.command.quoting=bare"
              "run.3.argument.1=a"
              "run.3.argument.1.quoting=bare"
              "run.3.argument.2=" + at "work/out.7z"
              "run.3.argument.2.quoting=quoted"
              "run.3.argument.3=@" + at "session/marked-1.txt"
              "run.3.list=" + at "session/marked-1.txt"
              "run.3.list.1=" + at "work/a.txt"
              "run.3.list.2=" + at "work/b c.txt"
              "run.3.mode=shell-keep"
              "run.3.directory=" + at "work"
              "run.3.elevation=administrator" ]
    )

[<Theory>]
[<InlineData("run.3=")>]
[<InlineData("run.3.command=")>]
[<InlineData("run.3.mode=")>]
[<InlineData("run.3.directory=")>]
[<InlineData("run.3.elevation=")>]
let ``a command run missing a part it is always written with is passed over`` (missing: string) =
    let lines =
        History.format sample
        |> List.filter (fun line -> not (line.StartsWith(missing, StringComparison.Ordinal)))

    let parsed = History.parse lines

    Assert.Equal<Started list>(items sample.Launches |> List.truncate 2, items parsed.Launches)

[<Fact>]
let ``a command run whose words break a line is not remembered`` () =
    let broken =
        { packing with
            Command =
                { packing.Command with
                    Arguments = [ word "first\nsecond" Quoted ] } }

    let remembered = remember Windows 1 (Ran broken) History.empty

    Assert.Empty remembered.Launches

[<Fact>]
let ``what is formatted is what is parsed back`` () =
    let parsed = History.format sample |> History.parse

    Assert.Equal(sample, parsed)

[<Fact>]
let ``each list is written under its own numbered keys`` () =
    let lines = History.format sample

    Assert.Contains("folder.1=" + at "work", lines)
    Assert.Contains("folder.1.at=2026-09-28T14:05:00.0000000+02:00", lines)
    Assert.Contains("folder.2=" + at "backup.zip", lines)
    Assert.Contains("folder.2.inside=docs/2024", lines)
    Assert.Contains("run.1=" + at "tools/setup.exe", lines)
    Assert.Contains("run.1.program=installer.exe", lines)
    Assert.Contains("run.1.directory=" + at "tools", lines)
    Assert.Contains("run.1.elevation=administrator", lines)
    Assert.Contains("run.2.elevation=user", lines)
    Assert.Contains("view.1=" + at "notes.md", lines)
    Assert.Contains("view.1.use=edited", lines)
    Assert.Contains("view.2.use=viewed", lines)
    Assert.Contains("input.target.1=" + at "backup", lines)
    Assert.Contains("input.find.masks.2=a=b", lines)

[<Fact>]
let ``every kind of field is written under a key of its own and read back as itself`` () =
    let history =
        [ CopyOrMoveTarget
          NewDirectory
          NewFile
          SelectionPattern
          FindMasks
          SearchedText
          SearchedBytes
          ArchiveTarget
          LinkDestination
          OpenWithProgram ]
        |> List.fold (fun history kind -> remember Windows 1 (Typed(kind, text "typed")) history) History.empty

    let lines = History.format history
    let parsed = History.parse lines

    Assert.Subset(
        HashSet lines,
        HashSet
            [ "input.target.1=typed"
              "input.mkdir.1=typed"
              "input.newfile.1=typed"
              "input.pattern.1=typed"
              "input.find.masks.1=typed"
              "input.find.text.1=typed"
              "input.bytes.1=typed"
              "input.archive.1=typed"
              "input.link.1=typed"
              "input.program.1=typed" ]
    )

    Assert.Equal(history, parsed)

[<Fact>]
let ``nothing to read is an empty history`` () =
    let parsed = History.parse []

    Assert.Equal(History.empty, parsed)

[<Fact>]
let ``entries are read in the order of their numbers, gaps and all`` () =
    let lines =
        [ "folder.7=" + at "later"
          "folder.7.at=2026-09-28T14:01:00+02:00"
          "folder.2=" + at "sooner"
          "folder.2.at=2026-09-28T14:02:00+02:00" ]

    let parsed = History.parse lines

    Assert.Equal<Place list>([ onDisk "sooner"; onDisk "later" ], items parsed.Folders)

[<Theory>]
[<InlineData("folder.1.at=yesterday")>]
[<InlineData("view.1.use=printed")>]
[<InlineData("run.1.elevation=root")>]
[<InlineData("folder.2.inside=../outside")>]
let ``an entry with a part that cannot be read costs that entry alone`` (broken: string) =
    let lines =
        [ "folder.1=" + at "work"
          "folder.1.at=2026-09-28T14:01:00+02:00"
          "folder.2=" + at "backup.zip"
          "folder.2.at=2026-09-28T14:02:00+02:00"
          "view.1=" + at "notes.md"
          "view.1.use=viewed"
          "view.1.at=2026-09-28T14:03:00+02:00"
          "run.1=" + at "setup.exe"
          "run.1.directory=" + at "tools"
          "run.1.elevation=user"
          "run.1.at=2026-09-28T14:04:00+02:00"
          broken ]

    let parsed = History.parse lines

    let count =
        parsed.Folders.Length + parsed.FileVisits.Length + parsed.Launches.Length

    Assert.Equal(3, count)

[<Theory>]
[<InlineData("folder.1.at")>]
[<InlineData("run.1.directory")>]
[<InlineData("run.1.elevation")>]
[<InlineData("view.1.use")>]
let ``an entry missing a part it is always written with is passed over`` (missing: string) =
    let lines =
        History.format sample
        |> List.filter (fun line -> not (line.StartsWith(missing + "=", StringComparison.Ordinal)))

    let parsed = History.parse lines

    let count =
        parsed.Folders.Length + parsed.FileVisits.Length + parsed.Launches.Length

    Assert.Equal(6, count)

[<Fact>]
let ``a line that is not a key and a value is passed over`` () =
    let lines = "no equals sign here" :: "unknown.key=value" :: History.format sample

    let parsed = History.parse lines

    Assert.Equal(sample, parsed)

[<Fact>]
let ``the history sits beside the settings, in a file of its own`` () =
    let fs = MockFileSystem()

    let path = History.beside fs (fp (at "config/Marbas/settings.conf"))

    Assert.Equal(fp (at "config/Marbas/history.conf"), path)

[<Fact>]
let ``a history never written loads as an empty one`` () =
    let fs = MockFileSystem()

    let loaded = History.load fs historyPath

    Assert.Equal(Ok History.empty, loaded)

[<Fact>]
let ``a change is applied on top of what the file holds, not of what was held before`` () =
    let fs = MockFileSystem()
    write fs (History.format sample)

    let changed =
        History.change fs historyPath (moment 30) (Remember(Visited(onDisk "elsewhere")))

    let expected =
        History.apply Windows (moment 30) (Remember(Visited(onDisk "elsewhere"))) sample

    Assert.Equal(Ok expected, changed)
    Assert.Equal(Ok expected, History.load fs historyPath)

[<Fact>]
let ``the first change creates the file and the directory it lives in`` () =
    let fs = MockFileSystem()

    let changed =
        History.change fs historyPath (moment 1) (Remember(Visited(onDisk "work")))

    let loaded = History.load fs historyPath
    Assert.Equal(changed, loaded)
    Assert.Equal<Place list>([ onDisk "work" ], items (Expect.ok loaded).Folders)

[<Fact>]
let ``a history that cannot be written answers an error naming the file`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "blocked", MockFileData "x")
    let blocked = fp (at "blocked/history.conf")

    let changed =
        History.change fs blocked (moment 1) (Remember(Visited(onDisk "work")))

    Assert.Equal(blocked, unwritten (Expect.error changed))

[<Fact>]
let ``a history that cannot be read is answered as an error and left as it was`` () =
    let fs = Fakes.UnreadableFile(FileSystemPath.value historyPath)
    write fs (History.format sample)

    let changed =
        History.change fs historyPath (moment 1) (Remember(Visited(onDisk "work")))

    let kept = fs.GetFile(FileSystemPath.value historyPath).TextContents
    Assert.Equal(historyPath, unwritten (Expect.error changed))
    Assert.Equal(String.concat "\n" (History.format sample), kept)
