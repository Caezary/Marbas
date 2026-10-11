module Marbas.Tests.FileSystem.OperationsFixture

open System.IO.Abstractions.TestingHelpers
open Marbas.FileSystem
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let twoSources () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/a.txt", MockFileData "new a")
    fs.AddFile(at "data/b.txt", MockFileData "new b")
    fs

let bothSources = [ fp (at "data/a.txt"); fp (at "data/b.txt") ]
