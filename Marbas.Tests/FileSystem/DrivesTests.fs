module Marbas.Tests.FileSystem.DrivesTests

open System.IO
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Tests

let private fp = FileSystemPath.create

let private roots rows =
    rows |> List.map (DriveRow.root >> FileSystemPath.value)

[<Fact>]
let ``a mount is read into its root, label, kind, size and free space`` () =
    let fs =
        Fakes.MountedFileSystem
            [ { Fakes.Mount.ext4 "/media/user/stick" with
                  Format = "vfat"
                  DriveType = DriveType.Removable
                  Size = 64_000_000_000L
                  Free = 10_000_000_000L
                  Label = "STICK" } ]

    let rows = Drives.list fs None

    let expected =
        MountedDrive
            { Root = fp "/media/user/stick"
              Label = Some "STICK"
              Kind = Removable
              Size = 64_000_000_000L
              Free = 10_000_000_000L }

    Assert.Equal<DriveRow list>([ expected ], rows)

[<Theory>]
[<InlineData(DriveType.Fixed, "Fixed")>]
[<InlineData(DriveType.Removable, "Removable")>]
[<InlineData(DriveType.Network, "Network")>]
[<InlineData(DriveType.CDRom, "Optical")>]
[<InlineData(DriveType.Ram, "Fixed")>]
[<InlineData(DriveType.Unknown, "Fixed")>]
let ``the system's drive type is read into one of four kinds`` (driveType: DriveType) (kind: string) =
    let fs =
        Fakes.MountedFileSystem
            [ { Fakes.Mount.ext4 "/" with
                  DriveType = driveType } ]

    let rows = Drives.list fs None

    let drive = Assert.Single rows
    Assert.Equal(Some kind, DriveRow.kind drive |> Option.map string)

[<Fact>]
let ``a blank label is no label`` () =
    let fs =
        Fakes.MountedFileSystem
            [ { Fakes.Mount.ext4 "/" with
                  Label = "  " } ]

    let rows = Drives.list fs None

    let expected =
        MountedDrive
            { Root = fp "/"
              Label = None
              Kind = Fixed
              Size = 0L
              Free = 0L }

    Assert.Equal<DriveRow list>([ expected ], rows)

[<Fact>]
let ``a label that only repeats the root is no label`` () =
    let fs =
        Fakes.MountedFileSystem
            [ { Fakes.Mount.ext4 "/mnt/backup" with
                  Label = "/mnt/backup" } ]

    let rows = Drives.list fs None

    let expected =
        MountedDrive
            { Root = fp "/mnt/backup"
              Label = None
              Kind = Fixed
              Size = 0L
              Free = 0L }

    Assert.Equal<DriveRow list>([ expected ], rows)

[<Fact>]
let ``mounts are listed in alphabetical order of their roots`` () =
    let fs =
        Fakes.MountedFileSystem
            [ Fakes.Mount.ext4 "/mnt/backup"
              Fakes.Mount.ext4 "/"
              Fakes.Mount.ext4 "/boot" ]

    let rows = Drives.list fs None

    Assert.Equal<string list>([ "/"; "/boot"; "/mnt/backup" ], roots rows)

[<Fact>]
let ``a drive that is not ready is hidden`` () =
    let fs =
        Fakes.MountedFileSystem
            [ Fakes.Mount.ext4 "/"
              { Fakes.Mount.ext4 "/media/user/reader" with
                  Ready = false } ]

    let rows = Drives.list fs None

    Assert.Equal<string list>([ "/" ], roots rows)

[<Fact>]
let ``a drive whose size cannot be read is hidden`` () =
    let fs =
        Fakes.MountedFileSystem
            [ Fakes.Mount.ext4 "/"
              { Fakes.Mount.ext4 "/run/user/doc" with
                  Readable = false } ]

    let rows = Drives.list fs None

    Assert.Equal<string list>([ "/" ], roots rows)

[<Theory>]
[<InlineData("squashfs")>]
[<InlineData("tmpfs")>]
[<InlineData("devtmpfs")>]
[<InlineData("overlay")>]
[<InlineData("proc")>]
[<InlineData("sysfs")>]
[<InlineData("cgroup2")>]
[<InlineData("devpts")>]
[<InlineData("fuse.portal")>]
let ``a mount that is not storage is hidden`` (format: string) =
    let fs =
        Fakes.MountedFileSystem
            [ Fakes.Mount.ext4 "/"
              { Fakes.Mount.ext4 "/snap/core/1" with
                  Format = format } ]

    let rows = Drives.list fs None

    Assert.Equal<string list>([ "/" ], roots rows)

[<Fact>]
let ``the home directory is offered after the drives`` () =
    let fs = Fakes.MountedFileSystem [ Fakes.Mount.ext4 "/home"; Fakes.Mount.ext4 "/" ]

    let rows = Drives.list fs (Some(fp "/home/user"))

    Assert.Equal<string list>([ "/"; "/home"; "/home/user" ], roots rows)

[<Fact>]
let ``a home directory that is a drive's root is offered once`` () =
    let fs =
        Fakes.MountedFileSystem [ Fakes.Mount.ext4 "/"; Fakes.Mount.ext4 "/home/user" ]

    let rows = Drives.list fs (Some(fp "/home/user"))

    Assert.Equal<string list>([ "/"; "/home/user" ], roots rows)

[<Fact>]
let ``the home row is the home directory itself`` () =
    let fs = Fakes.MountedFileSystem [ Fakes.Mount.ext4 "/" ]

    let rows = Drives.list fs (Some(fp "/home/user"))

    Assert.Equal(HomeDirectory(fp "/home/user"), List.last rows)

[<Theory>]
[<InlineData("/home/user/docs", "/")>]
[<InlineData("/media/user/stick/photos", "/media/user/stick")>]
[<InlineData("/media/user/stick", "/media/user/stick")>]
[<InlineData("/media/user/sticker", "/")>]
let ``a path is held by the longest drive root it lies under`` (path: string) (expected: string) =
    let fs =
        Fakes.MountedFileSystem [ Fakes.Mount.ext4 "/"; Fakes.Mount.ext4 "/media/user/stick" ]

    let drive = Drives.holding fs (fp path)

    Assert.Equal(Some(fp expected), drive)

[<WindowsFact>]
let ``a drive letter holds a path spelled in another case`` () =
    let fs =
        Fakes.MountedFileSystem [ Fakes.Mount.ext4 @"C:\"; Fakes.Mount.ext4 @"D:\" ]

    let drive = Drives.holding fs (fp @"d:\data")

    Assert.Equal(Some(fp @"D:\"), drive)

[<Fact>]
let ``a path under no drive is held by none`` () =
    let fs = Fakes.MountedFileSystem [ Fakes.Mount.ext4 @"C:\" ]

    let drive = Drives.holding fs (fp @"E:\data")

    Assert.Equal(None, drive)

[<WindowsFact>]
let ``drives added to the mock are listed by their letters`` () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start"

    fs.AddDrive(
        @"D:\",
        MockDriveData(
            DriveType = DriveType.Network,
            TotalSize = 500L,
            AvailableFreeSpace = 200L,
            VolumeLabel = "Share",
            DriveFormat = "NTFS"
        )
    )

    let rows = Drives.list fs None

    let expected =
        MountedDrive
            { Root = fp @"D:\"
              Label = Some "Share"
              Kind = Network
              Size = 500L
              Free = 200L }

    Assert.Equal<string list>([ @"C:\"; @"D:\" ], roots rows)
    Assert.Equal(expected, List.last rows)

[<WindowsFact>]
let ``a drive letter that is not ready is hidden`` () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start"
    fs.AddDrive(@"E:\", MockDriveData(DriveType = DriveType.Removable, IsReady = false))

    let rows = Drives.list fs None

    Assert.Equal<string list>([ @"C:\" ], roots rows)
