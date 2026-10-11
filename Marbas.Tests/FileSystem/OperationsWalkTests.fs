module Marbas.Tests.FileSystem.OperationsWalkTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private linked () =
    let fs = Fakes.LinkFollowing()
    fs.AddFile(at "data/docs/inner.txt", MockFileData "inner")
    fs.Directory.CreateSymbolicLink(at "data/twin", at "data/docs") |> ignore
    fs.AddDirectory(at "backup")
    fs

let private linkTargetOf (fs: IO.Abstractions.IFileSystem) path = fs.DirectoryInfo.New(path).LinkTarget

[<Fact>]
let ``copy takes a directory link as a link rather than the tree behind it`` () =
    let fs = linked ()

    copy fs unattended (fp (at "backup")) (fp (at "data/twin")) |> Expect.ok

    Assert.Equal(at "data/docs", linkTargetOf fs (at "backup/twin"))

[<Fact>]
let ``copy of a tree holding a link to its own ancestor terminates`` () =
    let fs = linked ()
    fs.Directory.CreateSymbolicLink(at "data/docs/loop", at "data") |> ignore

    copy fs unattended (fp (at "backup")) (fp (at "data")) |> Expect.ok

    Assert.Equal("inner", fs.File.ReadAllText(at "backup/data/docs/inner.txt"))
    Assert.Equal(at "data", linkTargetOf fs (at "backup/data/docs/loop"))

[<Fact>]
let ``move takes a directory link as a link and leaves what it pointed at`` () =
    let fs = linked ()

    move fs unattended (fp (at "backup")) (fp (at "data/twin")) |> Expect.ok

    Assert.Equal(at "data/docs", linkTargetOf fs (at "backup/twin"))
    Assert.Null(linkTargetOf fs (at "data/twin"))
    Assert.Equal("inner", fs.File.ReadAllText(at "data/docs/inner.txt"))

[<Fact>]
let ``delete of a directory link leaves what it points at`` () =
    let fs = linked ()

    delete fs unattended (fp (at "data/twin")) |> Expect.ok

    Assert.False(fs.Directory.Exists(at "data/twin"))
    Assert.Equal("inner", fs.File.ReadAllText(at "data/docs/inner.txt"))

[<Fact>]
let ``delete of a tree holding a link leaves what the link points at`` () =
    let fs = linked ()
    fs.Directory.CreateSymbolicLink(at "backup/twin", at "data/docs") |> ignore

    delete fs unattended (fp (at "backup")) |> Expect.ok

    Assert.False(fs.Directory.Exists(at "backup"))
    Assert.Equal("inner", fs.File.ReadAllText(at "data/docs/inner.txt"))

let private watched () =
    let fs = Fakes.WalkWitness()
    fs.AddDirectory(at "out")
    fs.AddFile(at "work/notes.txt", MockFileData "notes")
    fs.AddFile(at "work/docs/inner.txt", MockFileData "inner")
    fs.AddFile(at "work/docs/deep/leaf.txt", MockFileData "leaf")
    fs

[<Fact>]
let ``deleting a tree enumerates each directory once`` () =
    let fs = watched ()

    delete fs unattended (fp (at "work")) |> Expect.ok

    Assert.Equal(3, fs.Enumerations)

[<Fact>]
let ``deleting a tree asks nothing about an entry it enumerated`` () =
    let fs = watched ()

    delete fs unattended (fp (at "work")) |> Expect.ok

    Assert.Equal(0, fs.AskedAbout(at "work/docs/inner.txt"))

[<Fact>]
let ``copying a tree enumerates each directory once`` () =
    let fs = watched ()

    copy fs unattended (fp (at "out")) (fp (at "work")) |> Expect.ok

    Assert.Equal(3, fs.Enumerations)

[<Fact>]
let ``copying a tree asks nothing about an entry it enumerated`` () =
    let fs = watched ()

    copy fs unattended (fp (at "out")) (fp (at "work")) |> Expect.ok

    Assert.Equal(0, fs.AskedAbout(at "work/docs/inner.txt"))
