module Marbas.Tests.FileSystem.SettingsTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem

let private fp = FileSystemPath.create

let private settingsPath = fp @"C:\config\marbas.conf"

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start"
    fs.AddDirectory @"C:\backup"
    fs

let private defaults = Settings.forPath (fp @"C:\start")

let private stored: Settings =
    { Left =
        { Path = fp @"C:\backup"
          Sort = SortOrder.create BySize Descending
          Hidden = Shown }
      Right =
        { Path = fp @"C:\start"
          Sort = SortOrder.create ByDate Ascending
          Hidden = Concealed } }

let private write (fs: MockFileSystem) lines =
    fs.AddFile(FileSystemPath.value settingsPath, MockFileData(String.concat "\n" lines))

[<Fact>]
let ``the defaults put both panels on one path, sorted by name, hidden concealed`` () =
    let settings = Settings.forPath (fp @"C:\start")

    Assert.Equal(fp @"C:\start", settings.Left.Path)
    Assert.Equal(fp @"C:\start", settings.Right.Path)
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

    Assert.Contains(@"left.path=C:\backup", lines)
    Assert.Contains("left.sort=size", lines)
    Assert.Contains("left.direction=descending", lines)
    Assert.Contains("left.hidden=shown", lines)
    Assert.Contains(@"right.path=C:\start", lines)
    Assert.Contains("right.sort=date", lines)
    Assert.Contains("right.direction=ascending", lines)
    Assert.Contains("right.hidden=concealed", lines)

[<Fact>]
let ``a setting the file does not carry keeps its default`` () =
    let parsed = Settings.parse defaults [ @"left.path=C:\backup" ]

    Assert.Equal(fp @"C:\backup", parsed.Left.Path)
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

    Assert.Equal(fp @"C:\start", parsed.Left.Path)

[<Fact>]
let ``lines that are not settings are passed over`` () =
    let parsed =
        Settings.parse defaults [ ""; "# hand written"; "nonsense"; "top.path=C:\\gone"; "left.sort=size" ]

    Assert.Equal(BySize, parsed.Left.Sort.Key)
    Assert.Equal(defaults.Right, parsed.Right)

[<Fact>]
let ``keys and values are read whatever their case`` () =
    let parsed = Settings.parse defaults [ "LEFT.Sort=Size"; "Left.Hidden=SHOWN" ]

    Assert.Equal(BySize, parsed.Left.Sort.Key)
    Assert.Equal(Shown, parsed.Left.Hidden)

[<Fact>]
let ``a path keeps the case it was written in`` () =
    let parsed = Settings.parse defaults [ @"left.path=C:\Backup" ]

    Assert.Equal(fp @"C:\Backup", parsed.Left.Path)

[<Fact>]
let ``a settings file that is not there leaves the defaults`` () =
    let fs = mockFs ()

    let loaded = Settings.load fs defaults settingsPath

    Assert.Equal(defaults, loaded)

[<Fact>]
let ``a settings file that cannot be read leaves the defaults`` () =
    let fs = mockFs ()
    fs.AddDirectory(FileSystemPath.value settingsPath)

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
    write fs [ @"left.path=C:\gone"; "left.sort=size" ]

    let loaded = Settings.load fs defaults settingsPath

    Assert.Equal(fp @"C:\start", loaded.Left.Path)
    Assert.Equal(BySize, loaded.Left.Sort.Key)

[<Fact>]
let ``a stored path that names a file falls back to the default`` () =
    let fs = mockFs ()
    fs.AddFile(@"C:\notes.txt", MockFileData "x")
    write fs [ @"left.path=C:\notes.txt" ]

    let loaded = Settings.load fs defaults settingsPath

    Assert.Equal(fp @"C:\start", loaded.Left.Path)

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
    fs.AddFile(@"C:\blocked", MockFileData "x")

    Settings.save fs (fp @"C:\blocked\marbas.conf") stored

    Assert.False(fs.File.Exists @"C:\blocked\marbas.conf")
