module Marbas.Tests.State.Shell.ShellArchiveFixture

open System.IO.Abstractions.TestingHelpers
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let entry raw =
    ArchivePath.tryCreate raw |> Expect.some

let dataZip = fp (at "start/data.zip")

let zipFormat = ArchiveFormat.tryCreate Zip |> Expect.some

let unpackReadyFs password =
    let fs = MockFileSystem()
    fs.AddFile(at "start/work/notes.txt", MockFileData "notes")
    fs.AddFile(at "start/work/docs/inner.txt", MockFileData "inner")
    fs.AddDirectory(at "backup")

    Archive.write
        fs
        unattended
        { Path = dataZip
          Format = zipFormat
          Compression = Normal
          Password = password }
        [ fp (at "start/work/notes.txt"); fp (at "start/work/docs") ]
    |> ignore

    fs
