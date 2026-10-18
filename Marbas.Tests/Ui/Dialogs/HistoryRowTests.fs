module Marbas.Tests.Ui.Dialogs.HistoryRowTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Programs
open Marbas.Profile
open Marbas.Ui.Dialogs
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private now = DateTimeOffset(2026, 9, 28, 15, 30, 0, TimeSpan.FromHours 2.0)

[<Fact>]
let ``a moment of today is shown as its time`` () =
    let shown =
        HistoryRow.moment now (DateTimeOffset(2026, 9, 28, 9, 5, 0, TimeSpan.FromHours 2.0))

    Assert.Equal("09:05", shown)

[<Fact>]
let ``a moment of an earlier day is shown as its date`` () =
    let shown =
        HistoryRow.moment now (DateTimeOffset(2026, 9, 25, 23, 50, 0, TimeSpan.FromHours 2.0))

    Assert.Equal("2026-09-25", shown)

[<Fact>]
let ``a moment is read in the offset of now, not in the one it was written with`` () =
    let shown =
        HistoryRow.moment now (DateTimeOffset(2026, 9, 27, 23, 0, 0, TimeSpan.Zero))

    Assert.Equal("01:00", shown)

let private launch program elevation =
    { Target = fp (at "work/notes.md")
      Program = ProgramPath.tryCreate program
      WorkingDirectory = fp (at "work")
      Elevation = elevation }

[<Theory>]
[<InlineData("code.exe", " (code.exe)")>]
[<InlineData(@"C:\Program Files\Editor\editor.exe", " (editor.exe)")>]
[<InlineData("/usr/bin/vim", " (vim)")>]
let ``a file started with a program chosen is followed by the program's name`` (program: string) (named: string) =
    let what = HistoryRow.launchWhat (launch program AsUser)

    Assert.Equal(at "work/notes.md" + named, what)

[<Fact>]
let ``a file started with no program chosen is shown as its path`` () =
    let what = HistoryRow.launchWhat (launch "" AsUser)

    Assert.Equal(at "work/notes.md", what)

[<Theory>]
[<InlineData("")>]
[<InlineData("code.exe")>]
let ``a file started as the user says nothing of how`` (program: string) =
    let how = HistoryRow.launchHow (launch program AsUser)

    Assert.Equal("", how)

[<Theory>]
[<InlineData("")>]
[<InlineData("installer.exe")>]
let ``a file started as administrator says admin`` (program: string) =
    let how = HistoryRow.launchHow (launch program AsAdministrator)

    Assert.Equal("admin", how)

[<Fact>]
let ``a directory on disk is shown as its path`` () =
    let shown = HistoryRow.place (MockFileSystem()) (Place.OnDisk(fp (at "work/docs")))

    Assert.Equal(at "work/docs", shown)

[<Fact>]
let ``a level inside an archive is shown as the archive's path followed by the level`` () =
    let inside = Expect.some (ArchivePath.tryCreate "docs/2024")

    let shown =
        HistoryRow.place (MockFileSystem()) (Place.InArchive(fp (at "backup.zip"), inside))

    Assert.Equal(at "backup.zip/docs/2024", shown)

[<Fact>]
let ``the top of an archive is shown as the archive's path`` () =
    let shown =
        HistoryRow.place (MockFileSystem()) (Place.InArchive(fp (at "backup.zip"), ArchivePath.root))

    Assert.Equal(at "backup.zip", shown)

let private packing elevation : CommandRun =
    { Label = Expect.some (MenuLabel.tryCreate "Pack")
      Command =
        { Program = { Text = "7z"; Quoting = Bare }
          Arguments =
            [ { Text = "a"; Quoting = Bare }
              { Text = at "work/my notes.md"
                Quoting = Quoted } ]
          ListFile = None }
      Run = Directly
      Elevation = elevation
      WorkingDirectory = fp (at "work") }

[<Fact>]
let ``a command run from the menu is shown as its label and the line it ran, every path quoted`` () =
    let notes = at "work/my notes.md"

    let what = HistoryRow.startedWhat (StartedCommand(packing AsUser))

    Assert.Equal($"Pack: 7z a \"{notes}\"", what)

[<Fact>]
let ``a command run as administrator says so`` () =
    let how = HistoryRow.startedHow (StartedCommand(packing AsAdministrator))

    Assert.Equal(HistoryRow.AdministratorNote, how)

[<Fact>]
let ``a file started is shown as it always was`` () =
    let what = HistoryRow.startedWhat (StartedFile(launch "code.exe" AsUser))

    Assert.Equal(HistoryRow.launchWhat (launch "code.exe" AsUser), what)
