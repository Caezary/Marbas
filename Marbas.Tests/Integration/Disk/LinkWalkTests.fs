module Marbas.Tests.Integration.Disk.LinkWalkTests

open System
open System.IO
open System.IO.Abstractions
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Tests

type private Scratch() =
    let root = Path.Combine(Path.GetTempPath(), $"marbas-links-{Guid.NewGuid():N}")

    do
        Directory.CreateDirectory(Path.Combine(root, "data", "docs")) |> ignore
        Directory.CreateDirectory(Path.Combine(root, "backup")) |> ignore
        File.WriteAllText(Path.Combine(root, "data", "docs", "inner.txt"), "inner")
        Directory.CreateSymbolicLink(Path.Combine(root, "data", "docs", "loop"), Path.Combine(root, "data")) |> ignore

    member _.At(relative: string) = Path.Combine(root, Root.relative relative)

    member this.Path(relative: string) = FileSystemPath.create (this.At relative)

    interface IDisposable with
        member this.Dispose() =
            Directory.Delete(this.At "data/docs/loop")

            if Directory.Exists(this.At "backup/data/docs/loop") then
                Directory.Delete(this.At "backup/data/docs/loop")

            Directory.Delete(root, true)

let private disk = FileSystem() :> IFileSystem

[<SymbolicLinkFact>]
let ``enumerating a directory link walks into what it points at`` () =
    use scratch = new Scratch()

    let children =
        Directory.EnumerateFileSystemEntries(scratch.At "data/docs/loop")
        |> Seq.map Path.GetFileName
        |> List.ofSeq

    Assert.Equal<string list>([ "docs" ], children)

[<SymbolicLinkFact>]
let ``deleting a directory recursively leaves what a link inside it points at`` () =
    use scratch = new Scratch()
    Directory.CreateSymbolicLink(scratch.At "backup/twin", scratch.At "data/docs") |> ignore

    Directory.Delete(scratch.At "backup", true)

    Assert.False(Directory.Exists(scratch.At "backup"))
    Assert.Equal("inner", File.ReadAllText(scratch.At "data/docs/inner.txt"))

[<SymbolicLinkFact>]
let ``the listing says a link is one`` () =
    use scratch = new Scratch()

    let entries = DirectoryListing.read disk (scratch.Path "data/docs") |> Expect.ok
    let loop = entries |> List.find (fun entry -> entry.Name = EntryName.create "loop")

    Assert.Equal(Some(scratch.Path "data"), loop.LinkTarget)

[<SymbolicLinkFact>]
let ``copying a tree holding a link to its own ancestor copies the link and terminates`` () =
    use scratch = new Scratch()

    copy disk unattended (scratch.Path "backup") (scratch.Path "data") |> Expect.ok

    Assert.Equal("inner", File.ReadAllText(scratch.At "backup/data/docs/inner.txt"))
    Assert.Equal(scratch.At "data", DirectoryInfo(scratch.At "backup/data/docs/loop").LinkTarget)

[<SymbolicLinkFact>]
let ``measuring a tree holding a link to its own ancestor terminates`` () =
    use scratch = new Scratch()

    let size = DirectoryMeasure.measure disk unattended (OnDisk(scratch.Path "data"))

    Assert.Equal(Exact 5L, size)

[<SymbolicLinkFact>]
let ``deleting a tree holding a link leaves what the link points at`` () =
    use scratch = new Scratch()
    Directory.CreateSymbolicLink(scratch.At "backup/twin", scratch.At "data/docs") |> ignore

    delete disk (scratch.Path "backup") |> Expect.ok

    Assert.False(Directory.Exists(scratch.At "backup"))
    Assert.Equal("inner", File.ReadAllText(scratch.At "data/docs/inner.txt"))

[<SymbolicLinkFact>]
let ``wiping a tree holding a link leaves what the link points at`` () =
    use scratch = new Scratch()
    Directory.CreateSymbolicLink(scratch.At "backup/twin", scratch.At "data/docs") |> ignore

    wipe disk unattended (scratch.Path "backup") |> Expect.ok

    Assert.False(Directory.Exists(scratch.At "backup"))
    Assert.Equal("inner", File.ReadAllText(scratch.At "data/docs/inner.txt"))
