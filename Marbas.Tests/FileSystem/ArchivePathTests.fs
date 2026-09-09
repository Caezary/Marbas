module Marbas.Tests.FileSystem.ArchivePathTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Tests

let private created raw = ArchivePath.tryCreate raw |> Expect.some

let private en = EntryName.create

let private spelled path = ArchivePath.value path

let private archive = FileSystemPath.create (Root.at "work/backup.zip")

[<Fact>]
let ``the root of an archive is spelled by nothing at all`` () =
    Assert.Equal("", spelled ArchivePath.root)
    Assert.True(ArchivePath.isRoot ArchivePath.root)
    Assert.Equal<EntryName list>([], ArchivePath.segments ArchivePath.root)

[<Fact>]
let ``an empty text is the root`` () =
    Assert.Equal(ArchivePath.root, created "")

[<Theory>]
[<InlineData("docs")>]
[<InlineData("docs/deep")>]
[<InlineData("docs/deep/leaf.txt")>]
let ``a path is kept exactly as it was spelled`` raw =
    Assert.Equal(raw, spelled (created raw))

[<Fact>]
let ``a path is its own list of names`` () =
    Assert.Equal<EntryName list>([ en "docs"; en "deep" ], ArchivePath.segments (created "docs/deep"))

[<Fact>]
let ``a path below the root is not the root`` () =
    Assert.False(ArchivePath.isRoot (created "docs"))

[<Theory>]
[<InlineData("/docs")>]
[<InlineData("..")>]
[<InlineData("docs/..")>]
[<InlineData("../docs")>]
[<InlineData(".")>]
[<InlineData("docs/.")>]
[<InlineData("docs//deep")>]
[<InlineData("docs/")>]
[<InlineData(" ")>]
let ``a path that is rooted, climbs or names nothing is refused`` raw =
    Assert.Equal(None, ArchivePath.tryCreate raw)

[<Fact>]
let ``a child hangs a name under the path`` () =
    Assert.Equal("docs", spelled (ArchivePath.child ArchivePath.root (en "docs")))
    Assert.Equal("docs/deep", spelled (ArchivePath.child (created "docs") (en "deep")))

[<Fact>]
let ``the parent of a path is the level above it`` () =
    Assert.Equal(Some(created "docs"), ArchivePath.parent (created "docs/deep"))
    Assert.Equal(Some ArchivePath.root, ArchivePath.parent (created "docs"))

[<Fact>]
let ``the root has no level above it`` () =
    Assert.Equal(None, ArchivePath.parent ArchivePath.root)

[<Fact>]
let ``a place inside an archive is spelled below the archive`` () =
    let fs = MockFileSystem()

    let path = ArchivePath.spelled fs archive (created "docs/deep")

    Assert.Equal(FileSystemPath.create (Root.at "work/backup.zip/docs/deep"), path)

[<Fact>]
let ``the root of an archive is spelled by the archive itself`` () =
    let fs = MockFileSystem()

    let path = ArchivePath.spelled fs archive ArchivePath.root

    Assert.Equal(archive, path)
