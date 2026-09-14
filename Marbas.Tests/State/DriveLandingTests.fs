module Marbas.Tests.State.DriveLandingTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.ArchiveFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private systemDrive = MountedDrive(Fakes.driveAt (at ""))

let private stick = MountedDrive(Fakes.driveAt (at "media/stick"))

let private mounted () =
    let fs =
        Fakes.MountedFileSystem [ Fakes.Mount.ext4 (at ""); Fakes.Mount.ext4 (at "media/stick") ]

    fs.AddDirectory(at "start")
    fs.AddDirectory(at "backup")
    fs.AddDirectory(at "media/stick/photos")
    fs.AddFile(at "work/notes.txt", MockFileData "notes")
    fs

let private standing (fs: IFileSystem) left right =
    create (fp (at "start"))
    |> loadPanel fs Left (fp left)
    |> Expect.ok
    |> loadPanel fs Right (fp right)
    |> Expect.ok

[<Fact>]
let ``the drive the other panel stands on takes the panel to that panel's directory`` () =
    let fs = mounted ()
    let state = standing fs (at "media/stick/photos") (at "backup")

    let result = update fs (DriveChosen(Left, systemDrive)) state

    Assert.Equal(OnDisk(fp (at "backup")), result.State.Left.Path)

[<Fact>]
let ``the drive both panels stand on takes the panel to the other panel's directory`` () =
    let fs = mounted ()
    let state = standing fs (at "start") (at "backup")

    let result = update fs (DriveChosen(Left, systemDrive)) state

    Assert.Equal(OnDisk(fp (at "backup")), result.State.Left.Path)

[<Fact>]
let ``the right panel is taken to the left panel's directory the same way`` () =
    let fs = mounted ()
    let state = standing fs (at "media/stick/photos") (at "backup")

    let result = update fs (DriveChosen(Right, stick)) state

    Assert.Equal(OnDisk(fp (at "media/stick/photos")), result.State.Right.Path)

[<Fact>]
let ``a drive the other panel is not on gives its root`` () =
    let fs = mounted ()
    let state = standing fs (at "start") (at "backup")

    let result = update fs (DriveChosen(Left, stick)) state

    Assert.Equal(OnDisk(fp (at "media/stick")), result.State.Left.Path)

[<Fact>]
let ``the drive only this panel stands on gives its root`` () =
    let fs = mounted ()
    let state = standing fs (at "media/stick/photos") (at "backup")

    let result = update fs (DriveChosen(Left, stick)) state

    Assert.Equal(OnDisk(fp (at "media/stick")), result.State.Left.Path)

[<Fact>]
let ``a panel on a mount inside a drive stands on the mount and not on the drive around it`` () =
    let fs = mounted ()
    let state = standing fs (at "start") (at "media/stick/photos")

    let result = update fs (DriveChosen(Left, systemDrive)) state

    Assert.Equal(OnDisk(fp (at "")), result.State.Left.Path)

[<Fact>]
let ``the other panel inside an archive lends the directory that holds the archive`` () =
    let fs = mounted ()
    Archive.write fs Operations.unattended (request Zip (at "backup/work.zip")) [ fp (at "work/notes.txt") ] |> ignore

    let state =
        standing fs (at "media/stick/photos") (at "backup")
        |> loadLocationFocusing fs Right (InArchive(fp (at "backup/work.zip"), ArchivePath.root)) Top
        |> Expect.ok

    let result = update fs (DriveChosen(Left, systemDrive)) state

    Assert.Equal(OnDisk(fp (at "backup")), result.State.Left.Path)

[<Fact>]
let ``the home directory is where the home row goes`` () =
    let fs = mounted ()
    fs.AddDirectory(at "home/user")
    let state = standing fs (at "start") (at "backup")

    let result = update fs (DriveChosen(Left, HomeDirectory(fp (at "home/user")))) state

    Assert.Equal(OnDisk(fp (at "home/user")), result.State.Left.Path)

[<Fact>]
let ``a drive that went away is reported and the panel stays where it was`` () =
    let fs = mounted ()
    let state = standing fs (at "start") (at "backup")
    let gone = MountedDrive(Fakes.driveAt (at "media/gone"))

    let result = update fs (DriveChosen(Left, gone)) state

    Assert.Equal(state, result.State)
    Assert.Equal<Effect list>([ ReportListingError(DirectoryListing.DirectoryNotFound(fp (at "media/gone"))) ], result.Effects)

[<Fact>]
let ``a drive handed over is started with the system's own choice, standing in its root`` () =
    let fs = mounted ()
    let state = standing fs (at "start") (at "backup")

    let result = update fs (DriveHandedOver stick) state

    let expected =
        { Target = fp (at "media/stick")
          Program = None
          WorkingDirectory = fp (at "media/stick")
          Elevation = AsUser }

    Assert.Equal<Effect list>([ RunProgram expected ], result.Effects)

[<Fact>]
let ``a drive handed over moves neither panel`` () =
    let fs = mounted ()
    let state = standing fs (at "start") (at "backup")

    let result = update fs (DriveHandedOver stick) state

    Assert.Equal(state, result.State)
