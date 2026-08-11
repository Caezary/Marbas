module Marbas.Tests.FileSystem.DrivesTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem

let private fp = FileSystemPath.create

[<Fact>]
let ``lists every mounted drive in alphabetical order`` () =
    let fs = MockFileSystem()
    fs.AddDirectory @"D:\data"
    fs.AddDirectory @"C:\start"

    let drives = Drives.list fs

    Assert.Equal<FileSystemPath list>([ fp @"C:\"; fp @"D:\" ], drives)
