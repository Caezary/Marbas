module Marbas.Tests.State.NavigationErrorTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.State.AppState
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private fsWithVanishingDirectory () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/readme.txt", MockFileData "hi")
    fs

let private loaded side path (fs: IFileSystem) state =
    loadPanel fs side path state |> Expect.ok

[<Fact>]
let ``entering a directory that vanished reports an error and keeps the panel`` () =
    let fs = fsWithVanishingDirectory ()
    let state = create (fp (at "start")) |> loaded Left (fp (at "start")) fs |> setCursor Left 1
    fs.Directory.Delete(at "start/docs")

    let result = enter fs state

    Assert.Equal(Error(DirectoryNotFound(fp (at "start/docs"))), result)

[<Fact>]
let ``failed navigation leaves the panel where it was`` () =
    let fs = fsWithVanishingDirectory ()
    let state = create (fp (at "start")) |> loaded Left (fp (at "start")) fs |> setCursor Left 1
    fs.Directory.Delete(at "start/docs")

    let failure = enter fs state |> Expect.error

    Assert.Equal(DirectoryNotFound(fp (at "start/docs")), failure)
    Assert.Equal(OnDisk(fp (at "start")), state.Left.Path)
