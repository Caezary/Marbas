module Marbas.Tests.Find.FindGroundTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Locations
open Marbas.Find
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private mounted () =
    let fs =
        Fakes.MountedFileSystem [ Fakes.Mount.ext4 (at ""); Fakes.Mount.ext4 (at "media/stick") ]

    fs.AddDirectory(at "start/docs")
    fs.AddDirectory(at "media/stick/photos")
    fs

let private resolved (fs: IFileSystem) location scope =
    FindGround.resolve fs location [] [] scope

let private roots (fs: IFileSystem) ground =
    ground.Roots |> List.map (PanelLocation.value fs)

let private otherRoots ground =
    ground.OtherRoots |> List.map FileSystemPath.value |> List.sort

[<Theory>]
[<InlineData("start/docs")>]
[<InlineData("media/stick/photos")>]
let ``a search from the panel starts where the panel stands`` directory =
    let fs = mounted ()

    let ground = resolved fs (OnDisk(fp (at directory))) FromDirectory

    Assert.Equal<string list>([ at directory ], roots fs ground)

[<Fact>]
let ``a search of one directory starts there too`` () =
    let fs = mounted ()

    let ground = resolved fs (OnDisk(fp (at "start/docs"))) DirectoryOnly

    Assert.Equal<string list>([ at "start/docs" ], roots fs ground)

[<Fact>]
let ``a panel inside an archive searches below the level it stands on`` () =
    let fs = mounted ()
    let level = ArchivePath.tryCreate "docs" |> Expect.some

    let ground =
        resolved fs (InArchive(fp (at "start/backup.zip"), level)) FromDirectory

    Assert.Equal<string list>([ at "start/backup.zip/docs" ], roots fs ground)

[<Fact>]
let ``a panel inside an archive leaves it for the root of the volume`` () =
    let fs = mounted ()
    let level = ArchivePath.tryCreate "docs" |> Expect.some

    let ground =
        resolved fs (InArchive(fp (at "start/backup.zip"), level)) FromVolumeRoot

    Assert.Equal<string list>([ at "" ], roots fs ground)

[<Fact>]
let ``a search of marked directories starts at each of them`` () =
    let fs = mounted ()

    let marked = [ OnDisk(fp (at "start/docs")); OnDisk(fp (at "media/stick/photos")) ]

    let ground =
        FindGround.resolve fs (OnDisk(fp (at "start"))) marked [] MarkedDirectories

    Assert.Equal<string list>([ at "start/docs"; at "media/stick/photos" ], roots fs ground)

[<Fact>]
let ``a search from the volume root starts at the drive the panel stands on`` () =
    let fs = mounted ()

    let ground = resolved fs (OnDisk(fp (at "media/stick/photos"))) FromVolumeRoot

    Assert.Equal<string list>([ at "media/stick" ], roots fs ground)

[<Fact>]
let ``a search from the volume root keeps the other drives out`` () =
    let fs = mounted ()

    let ground = resolved fs (OnDisk(fp (at "start/docs"))) FromVolumeRoot

    Assert.Equal<string list>([ at "media/stick" ], otherRoots ground)

[<Fact>]
let ``a search of all drives starts at every root`` () =
    let fs = mounted ()

    let ground = resolved fs (OnDisk(fp (at "start"))) AllDrives

    Assert.Equal<string list>([ at ""; at "media/stick" ], roots fs ground |> List.sort)

[<Fact>]
let ``a search of all drives crosses freely between them`` () =
    let fs = mounted ()

    let ground = resolved fs (OnDisk(fp (at "start"))) AllDrives

    Assert.Empty ground.OtherRoots

[<Fact>]
let ``a search of the path starts at the directories it was given`` () =
    let fs = mounted ()
    let onPath = [ fp (at "start/docs") ]

    let ground =
        FindGround.resolve fs (OnDisk(fp (at "start"))) [] onPath PathDirectories

    Assert.Equal<string list>([ at "start/docs" ], roots fs ground)

[<Fact>]
let ``the path is split at the semicolon Windows uses`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "bin")
    fs.AddDirectory(at "tools")
    let raw = $"""{at "bin"};{at "tools"}"""

    let directories = FindGround.pathDirectories fs ';' raw

    Assert.Equal<string list>([ at "bin"; at "tools" ], directories |> List.map FileSystemPath.value)

[<Fact>]
let ``the path is split at the colon Unix uses`` () =
    let fs = MockFileSystem()
    fs.AddDirectory "/opt/bin"
    fs.AddDirectory "/opt/tools"

    let directories = FindGround.pathDirectories fs ':' "/opt/bin:/opt/tools"

    Assert.Equal<string list>([ "/opt/bin"; "/opt/tools" ], directories |> List.map FileSystemPath.value)

[<Fact>]
let ``a path entry that is not there is left out`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "bin")
    let raw = $"""{at "bin"};{at "gone"}"""

    let directories = FindGround.pathDirectories fs ';' raw

    Assert.Equal<string list>([ at "bin" ], directories |> List.map FileSystemPath.value)

[<Fact>]
let ``a directory named twice on the path is walked once`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "bin")
    let separator = fs.Path.DirectorySeparatorChar
    let raw = $"""{at "bin"};{at "bin"}{separator};{at "bin"}"""

    let directories = FindGround.pathDirectories fs ';' raw

    Assert.Equal<string list>([ at "bin" ], directories |> List.map FileSystemPath.value)

[<Fact>]
let ``blank and quoted path entries are read as they are meant`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "bin")
    let raw = $""";  ;"{at "bin"}" """

    let directories = FindGround.pathDirectories fs ';' raw

    Assert.Equal<string list>([ at "bin" ], directories |> List.map FileSystemPath.value)

[<Fact>]
let ``an empty path answers no directories`` () =
    let fs = MockFileSystem()

    let directories = FindGround.pathDirectories fs ';' ""

    Assert.Empty directories

[<WindowsFact("drive paths are compared without case on Windows alone")>]
let ``a directory named twice on the path in two cases is walked once`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "bin")
    let raw = $"""{at "bin"};{at "BIN"}"""

    let directories = FindGround.pathDirectories fs ';' raw

    Assert.Equal<string list>([ at "bin" ], directories |> List.map FileSystemPath.value)
