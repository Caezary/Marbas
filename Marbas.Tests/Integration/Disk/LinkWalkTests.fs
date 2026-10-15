module Marbas.Tests.Integration.Disk.LinkWalkTests

open System
open System.IO
open System.IO.Abstractions
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Tests

let private looped () =
    let scratch = new Scratch()
    scratch.Directory "data/docs"
    scratch.Directory "backup"
    scratch.File "data/docs/inner.txt" "inner"

    Directory.CreateSymbolicLink(scratch.At "data/docs/loop", scratch.At "data")
    |> ignore

    scratch

let private disk = FileSystem() :> IFileSystem

[<SymbolicLinkFact>]
let ``enumerating a directory link walks into what it points at`` () =
    use scratch = looped ()

    let children =
        Directory.EnumerateFileSystemEntries(scratch.At "data/docs/loop")
        |> Seq.map (Path.GetFileName >> Expect.notNull)
        |> List.ofSeq

    Assert.Equal<string list>([ "docs" ], children)

[<SymbolicLinkFact>]
let ``deleting a directory recursively leaves what a link inside it points at`` () =
    use scratch = looped ()

    Directory.CreateSymbolicLink(scratch.At "backup/twin", scratch.At "data/docs")
    |> ignore

    Directory.Delete(scratch.At "backup", true)

    Assert.False(Directory.Exists(scratch.At "backup"))
    Assert.Equal("inner", File.ReadAllText(scratch.At "data/docs/inner.txt"))

[<SymbolicLinkFact>]
let ``the listing says a link is one`` () =
    use scratch = looped ()

    let entries = DirectoryListing.read disk (scratch.Path "data/docs") |> Expect.ok
    let loop = entries |> List.find (fun entry -> entry.Name = EntryName.create "loop")

    Assert.Equal(Some(scratch.Path "data"), loop.LinkTarget)

[<SymbolicLinkFact>]
let ``copying a tree holding a link to its own ancestor copies the link and terminates`` () =
    use scratch = looped ()

    copy disk unattended (scratch.Path "backup") (scratch.Path "data") |> Expect.ok

    Assert.Equal("inner", File.ReadAllText(scratch.At "backup/data/docs/inner.txt"))
    Assert.Equal(scratch.At "data", DirectoryInfo(scratch.At "backup/data/docs/loop").LinkTarget)

[<SymbolicLinkFact>]
let ``measuring a tree holding a link to its own ancestor terminates`` () =
    use scratch = looped ()

    let size = DirectoryMeasure.measure disk unattended (OnDisk(scratch.Path "data"))

    Assert.Equal(Exact 5L, size)

[<SymbolicLinkFact>]
let ``deleting a tree holding a link leaves what the link points at`` () =
    use scratch = looped ()

    Directory.CreateSymbolicLink(scratch.At "backup/twin", scratch.At "data/docs")
    |> ignore

    delete disk unattended (scratch.Path "backup") |> Expect.ok

    Assert.False(Directory.Exists(scratch.At "backup"))
    Assert.Equal("inner", File.ReadAllText(scratch.At "data/docs/inner.txt"))

[<SymbolicLinkFact>]
let ``wiping a tree holding a link leaves what the link points at`` () =
    use scratch = looped ()

    Directory.CreateSymbolicLink(scratch.At "backup/twin", scratch.At "data/docs")
    |> ignore

    wipe disk unattended (scratch.Path "backup") |> Expect.ok

    Assert.False(Directory.Exists(scratch.At "backup"))
    Assert.Equal("inner", File.ReadAllText(scratch.At "data/docs/inner.txt"))
