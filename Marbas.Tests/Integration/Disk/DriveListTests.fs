module Marbas.Tests.Integration.Disk.DriveListTests

open System.IO
open System.IO.Abstractions
open Xunit
open Marbas.FileSystem
open Marbas.Tests

let private disk = FileSystem() :> IFileSystem

[<Fact>]
let ``the system's drives include the one the working directory is on`` () =
    let root = Path.GetPathRoot(Directory.GetCurrentDirectory())

    let rows = Drives.list disk None

    Assert.Contains(FileSystemPath.create root, rows |> List.map DriveRow.root)
