module Marbas.Tests.FileSystem.DrivesTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem

[<Fact>]
let ``lists every mounted drive in alphabetical order`` () =
    let fs = MockFileSystem()
    fs.AddDirectory @"D:\data"
    fs.AddDirectory @"C:\start"

    let drives = Drives.list fs

    Assert.Equal<string list>([ @"C:\"; @"D:\" ], drives)
