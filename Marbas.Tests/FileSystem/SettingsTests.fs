module Marbas.Tests.FileSystem.SettingsTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private en = EntryName.create

let private settingsPath = fp (at "config/marbas.conf")

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start")
    fs.AddDirectory(at "backup")
    fs

let private defaults = Settings.forPath (fp (at "start"))

let private stored: Settings =
    { Left =
        { Path = fp (at "backup")
          Sort = SortOrder.create BySize Descending
          Hidden = Shown }
      Right =
        { Path = fp (at "start")
          Sort = SortOrder.create ByDate Ascending
          Hidden = Concealed }
      LastCreatedDirectory = Some(en "work")
      Programs =
        { DefaultEditor = ProgramPath.tryCreate "code.exe"
          Associations =
            [ { Extension = Expect.some (FileExtension.tryCreate "fs")
                Role = ForEditing
                Program = Expect.some (ProgramPath.tryCreate "rider.exe") }
              { Extension = Expect.some (FileExtension.tryCreate "txt")
                Role = ForEditing
                Program = Expect.some (ProgramPath.tryCreate "notepad.exe") }
              { Extension = Expect.some (FileExtension.tryCreate "html")
                Role = ForOpening
                Program = Expect.some (ProgramPath.tryCreate "firefox.exe") } ] } }

let private write (fs: MockFileSystem) lines =
    fs.AddFile(FileSystemPath.value settingsPath, MockFileData(String.concat "\n" lines))

[<Fact>]
let ``the defaults put both panels on one path, sorted by name, hidden concealed`` () =
    let settings = Settings.forPath (fp (at "start"))

    Assert.Equal(fp (at "start"), settings.Left.Path)
    Assert.Equal(fp (at "start"), settings.Right.Path)
    Assert.Equal(SortOrder.byName, settings.Left.Sort)
    Assert.Equal(Concealed, settings.Left.Hidden)
    Assert.Equal(settings.Left, settings.Right)

[<Fact>]
let ``what is formatted is what is parsed back`` () =
    let parsed = Settings.format stored |> Settings.parse defaults

    Assert.Equal(stored, parsed)

[<Fact>]
let ``each panel is written under its own name`` () =
    let lines = Settings.format stored

    Assert.Contains("left.path=" + at "backup", lines)
    Assert.Contains("left.sort=size", lines)
    Assert.Contains("left.direction=descending", lines)
    Assert.Contains("left.hidden=shown", lines)
    Assert.Contains("right.path=" + at "start", lines)
    Assert.Contains("right.sort=date", lines)
    Assert.Contains("right.direction=ascending", lines)
    Assert.Contains("right.hidden=concealed", lines)

[<Fact>]
let ``every sort key is written under a name that is read back as itself`` () =
    let roundTrip key =
        let panel =
            { defaults.Left with
                Sort = SortOrder.create key Descending }

        (Settings.format { defaults with Left = panel } |> Settings.parse defaults).Left.Sort

    let parsed = [ ByName; ByExtension; ByDate; BySize ] |> List.map roundTrip

    Assert.Equal<SortOrder list>(
        [ SortOrder.create ByName Descending
          SortOrder.create ByExtension Descending
          SortOrder.create ByDate Descending
          SortOrder.create BySize Descending ],
        parsed
    )

[<Fact>]
let ``the programs are written under their own names, the role among them`` () =
    let lines = Settings.format stored

    Assert.Contains("editor.default=code.exe", lines)
    Assert.Contains("program.editing.fs=rider.exe", lines)
    Assert.Contains("program.editing.txt=notepad.exe", lines)
    Assert.Contains("program.opening.html=firefox.exe", lines)

[<Fact>]
let ``the programs that were written are the ones read back`` () =
    let parsed = Settings.parse defaults (Settings.format stored)

    Assert.Equal(stored.Programs, parsed.Programs)

[<Fact>]
let ``a file that names no program keeps the default programs`` () =
    let parsed = Settings.parse defaults [ "left.sort=size" ]

    Assert.Equal(ProgramSettings.none, parsed.Programs)

[<Fact>]
let ``an association is read whatever the case its extension was written in`` () =
    let parsed = Settings.parse defaults [ "program.editing.TXT=notepad.exe" ]

    Assert.Equal(
        ProgramPath.tryCreate "notepad.exe",
        ProgramSettings.programFor ForEditing (en "notes.txt") parsed.Programs
    )

[<Fact>]
let ``an association written before the role existed is one for editing`` () =
    let parsed = Settings.parse defaults [ "editor.extension.txt=notepad.exe"; "editor.default=code.exe" ]

    Assert.Equal(
        ProgramPath.tryCreate "notepad.exe",
        ProgramSettings.programFor ForEditing (en "notes.txt") parsed.Programs
    )

    let txt = Expect.some (FileExtension.tryCreate "txt")

    Assert.Equal(None, ProgramSettings.associated ForOpening txt parsed.Programs)
    Assert.Equal(ProgramPath.tryCreate "code.exe", parsed.Programs.DefaultEditor)

[<Fact>]
let ``an association whose role is not one of the two is passed over`` () =
    let parsed = Settings.parse defaults [ "program.printing.txt=notepad.exe" ]

    Assert.Equal(ProgramSettings.none, parsed.Programs)

[<Fact>]
let ``an association with nothing on either side is passed over`` () =
    let parsed =
        Settings.parse
            defaults
            [ "program.editing.=notepad.exe"
              "program.opening.md=  "
              "editor.extension.=notepad.exe"
              "editor.default=   " ]

    Assert.Equal(ProgramSettings.none, parsed.Programs)

[<Fact>]
let ``a setting the file does not carry keeps its default`` () =
    let parsed = Settings.parse defaults [ "left.path=" + at "backup" ]

    Assert.Equal(fp (at "backup"), parsed.Left.Path)
    Assert.Equal(SortOrder.byName, parsed.Left.Sort)
    Assert.Equal(Concealed, parsed.Left.Hidden)
    Assert.Equal(defaults.Right, parsed.Right)

[<Fact>]
let ``a value that means nothing keeps its default`` () =
    let parsed =
        Settings.parse defaults [ "left.sort=colour"; "left.direction=sideways"; "left.hidden=maybe" ]

    Assert.Equal(defaults.Left, parsed.Left)

[<Fact>]
let ``a blank path keeps its default`` () =
    let parsed = Settings.parse defaults [ "left.path=   " ]

    Assert.Equal(fp (at "start"), parsed.Left.Path)

[<Fact>]
let ``lines that are not settings are passed over`` () =
    let parsed =
        Settings.parse defaults [ ""; "# hand written"; "nonsense"; "top.path=" + at "gone"; "left.sort=size" ]

    Assert.Equal(BySize, parsed.Left.Sort.Key)
    Assert.Equal(defaults.Right, parsed.Right)

[<Fact>]
let ``keys and values are read whatever their case`` () =
    let parsed = Settings.parse defaults [ "LEFT.Sort=Size"; "Left.Hidden=SHOWN" ]

    Assert.Equal(BySize, parsed.Left.Sort.Key)
    Assert.Equal(Shown, parsed.Left.Hidden)

[<Fact>]
let ``a path keeps the case it was written in`` () =
    let parsed = Settings.parse defaults [ "left.path=" + at "Backup" ]

    Assert.Equal(fp (at "Backup"), parsed.Left.Path)

[<Fact>]
let ``the settings file sits in the directory the platform keeps application data in`` () =
    let fs = mockFs ()

    let location = Settings.location fs (at "home/user/AppData") (at "home/user")

    Assert.Equal(fp (at "home/user/AppData/Marbas/settings.conf"), location)

[<Fact>]
let ``a platform that names no application data directory falls back to .config at home`` () =
    let fs = mockFs ()

    let location = Settings.location fs "" (at "home/user")

    Assert.Equal(fp (at "home/user/.config/Marbas/settings.conf"), location)

[<Fact>]
let ``a blank application data directory falls back the same way`` () =
    let fs = mockFs ()

    let location = Settings.location fs "   " (at "home/user")

    Assert.Equal(fp (at "home/user/.config/Marbas/settings.conf"), location)

[<Fact>]
let ``a settings file that is not there leaves the defaults`` () =
    let fs = mockFs ()

    let loaded = Settings.load fs defaults settingsPath

    Assert.Equal(defaults, loaded)

[<Fact>]
let ``a settings path taken by a directory leaves the defaults`` () =
    let fs = mockFs ()
    fs.AddDirectory(FileSystemPath.value settingsPath)

    let loaded = Settings.load fs defaults settingsPath

    Assert.Equal(defaults, loaded)

[<Fact>]
let ``a settings file the file system refuses to read leaves the defaults`` () =
    let fs = mockFs ()
    let held = MockFileData(String.concat "\n" (Settings.format stored))
    held.AllowedFileShare <- System.IO.FileShare.None
    fs.AddFile(FileSystemPath.value settingsPath, held)

    let loaded = Settings.load fs defaults settingsPath

    Assert.Equal(defaults, loaded)

[<Fact>]
let ``a stored session is loaded panel by panel`` () =
    let fs = mockFs ()
    write fs (Settings.format stored)

    let loaded = Settings.load fs defaults settingsPath

    Assert.Equal(stored, loaded)

[<Fact>]
let ``a stored path that is no longer a directory falls back to the default`` () =
    let fs = mockFs ()
    write fs [ "left.path=" + at "gone"; "left.sort=size" ]

    let loaded = Settings.load fs defaults settingsPath

    Assert.Equal(fp (at "start"), loaded.Left.Path)
    Assert.Equal(BySize, loaded.Left.Sort.Key)

[<Fact>]
let ``a stored path that names a file falls back to the default`` () =
    let fs = mockFs ()
    fs.AddFile(at "notes.txt", MockFileData "x")
    write fs [ "left.path=" + at "notes.txt" ]

    let loaded = Settings.load fs defaults settingsPath

    Assert.Equal(fp (at "start"), loaded.Left.Path)

[<Fact>]
let ``what is saved is what is loaded back`` () =
    let fs = mockFs ()

    Settings.save fs settingsPath stored

    Assert.Equal(stored, Settings.load fs defaults settingsPath)

[<Fact>]
let ``saving creates the directory it writes into`` () =
    let fs = mockFs ()

    Settings.save fs settingsPath stored

    Assert.True(fs.File.Exists(FileSystemPath.value settingsPath))

[<Fact>]
let ``saving over an older file replaces it`` () =
    let fs = mockFs ()
    Settings.save fs settingsPath stored

    Settings.save fs settingsPath defaults

    Assert.Equal(defaults, Settings.load fs defaults settingsPath)

[<Fact>]
let ``saving where nothing can be written is passed over in silence`` () =
    let fs = mockFs ()
    fs.AddFile(at "blocked", MockFileData "x")

    Settings.save fs (fp (at "blocked/marbas.conf")) stored

    Assert.False(fs.File.Exists(at "blocked/marbas.conf"))

[<Fact>]
let ``the name of the last created directory is written under its own key`` () =
    let lines = Settings.format stored

    Assert.Contains("mkdir.name=work", lines)

[<Fact>]
let ``nothing is written when no directory was ever created`` () =
    let lines = Settings.format defaults

    Assert.DoesNotContain(lines, fun (line: string) -> line.StartsWith "mkdir.")

[<Fact>]
let ``a remembered name survives being written and read back`` () =
    let parsed = Settings.parse defaults [ "mkdir.name=Work Files" ]

    Assert.Equal(Some(en "Work Files"), parsed.LastCreatedDirectory)

[<Fact>]
let ``a blank remembered name is read as none`` () =
    let parsed = Settings.parse defaults [ "mkdir.name=   " ]

    Assert.Equal(None, parsed.LastCreatedDirectory)
