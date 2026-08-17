module Marbas.Tests.FileSystem.RootsTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

[<WindowsFact>]
let ``mounted drives are listed in alphabetical order`` () =
    let fs = MockFileSystem()
    fs.AddDirectory @"D:\data"
    fs.AddDirectory @"C:\start"

    let roots = Roots.list fs MountedDrives

    Assert.Equal<FileSystemPath list>([ fp @"C:\"; fp @"D:\" ], roots)

[<Fact>]
let ``a home directory is offered beside the root it sits under`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "home/user")

    let roots = Roots.list fs (RootAndHome(fp (at "home/user")))

    Assert.Equal<FileSystemPath list>([ fp (at ""); fp (at "home/user") ], roots)

[<Fact>]
let ``a home directory that is the root itself is offered once`` () =
    let fs = MockFileSystem()

    let roots = Roots.list fs (RootAndHome(fp (at "")))

    Assert.Equal<FileSystemPath list>([ fp (at "") ], roots)
