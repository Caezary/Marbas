module Marbas.Tests.FileSystem.LinksTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private data () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/docs/inner.txt", MockFileData "inner")
    fs.AddFile(at "data/notes.txt", MockFileData "notes")
    fs

let private symbolic target location =
    { Kind = Symbolic
      Target = fp target
      Location = fp location }

[<Theory>]
[<InlineData("data/docs", "data/twin")>]
[<InlineData("data/notes.txt", "data/twin.txt")>]
let ``a symbolic link is made and resolves to what it points at`` target location =
    let fs = data ()

    Links.create fs (symbolic (at target) (at location)) |> Expect.ok

    Assert.Equal(Some(fp (at target)), FileSystemPath.linkTarget fs (fp (at location)))

[<Fact>]
let ``a symbolic link to a directory is a directory`` () =
    let fs = data ()

    Links.create fs (symbolic (at "data/docs") (at "data/twin")) |> Expect.ok

    Assert.Equal(DirectoryEntry, FileSystemPath.kind fs (fp (at "data/twin")))

[<Fact>]
let ``a target given relative is written absolute, from where the link stands`` () =
    let fs = data ()

    Links.create fs (symbolic "docs" (at "data/twin")) |> Expect.ok

    Assert.Equal(Some(fp (at "data/docs")), FileSystemPath.linkTarget fs (fp (at "data/twin")))

[<Fact>]
let ``a link is not made over an entry that is already there`` () =
    let fs = data ()

    let failure =
        Links.create fs (symbolic (at "data/docs") (at "data/notes.txt")) |> Expect.error

    Assert.Equal(TargetExists(fp (at "data/notes.txt")), failure)

[<Fact>]
let ``a link is not made to nothing`` () =
    let fs = data ()

    let failure = Links.create fs (symbolic (at "data/gone") (at "data/twin")) |> Expect.error

    Assert.Equal(SourceNotFound(fp (at "data/gone")), failure)

[<Theory>]
[<InlineData("data/docs", "data/twin")>]
[<InlineData("data/notes.txt", "data/twin.txt")>]
let ``a symbolic link the system refuses for want of the right says so`` target location =
    let fs = Fakes.SymbolicLinksRefused(at "data")
    fs.AddFile(at "data/docs/inner.txt", MockFileData "inner")
    fs.AddFile(at "data/notes.txt", MockFileData "notes")

    let failure = Links.create fs (symbolic (at target) (at location)) |> Expect.error

    Assert.Equal(SymbolicLinkNotPermitted(fp (at location)), failure)

[<Fact>]
let ``a hard link has no managed way to be made and is refused by name`` () =
    let fs = data ()
    let request = { symbolic (at "data/notes.txt") (at "data/twin.txt") with Kind = Hard }

    let failure = Links.create fs request |> Expect.error

    Assert.Equal(LinkKindUnavailable(Hard, fp (at "data/twin.txt")), failure)
    Assert.False(fs.File.Exists(at "data/twin.txt"))

[<Fact>]
let ``a junction has no managed way to be made and is refused by name`` () =
    let fs = data ()
    let request = { symbolic (at "data/docs") (at "data/twin") with Kind = Junction }

    let failure = Links.create fs request |> Expect.error

    Assert.Equal(LinkKindUnavailable(Junction, fp (at "data/twin")), failure)
    Assert.False(fs.Directory.Exists(at "data/twin"))
