module Marbas.Tests.State.Update.UpdateFixture

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Marbas.FileSystem
open Marbas.Domain
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let cursor side index = { Side = side; Index = index }

let names (entries: DirectoryListing.Entry seq) =
    entries |> Seq.map (_.Name >> EntryName.value) |> List.ofSeq

let mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "start/setup.fs", MockFileData "hi")
    fs.AddDirectory(at "backup")
    fs

let loaded (fs: IFileSystem) =
    create (fp (at "start")) |> loadPanel fs Left (fp (at "start")) |> Expect.ok

let loadedBoth (fs: IFileSystem) =
    loaded fs |> loadPanel fs Right (fp (at "backup")) |> Expect.ok

let withHelpAt command (state: AppState) =
    { state with
        HelpPlace = helpPlaceOf command |> Expect.some }
