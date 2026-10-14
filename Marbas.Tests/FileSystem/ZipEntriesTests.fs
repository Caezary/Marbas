module Marbas.Tests.FileSystem.ZipEntriesTests

open ICSharpCode.SharpZipLib.Zip
open Xunit
open Marbas.Domain
open Marbas.FileSystem

[<Literal>]
let private UnixHost = 3

[<Literal>]
let private FatHost = 0

[<Literal>]
let private OrdinaryFile = 0o100644u

[<Literal>]
let private OrdinaryDirectory = 0o040755u

[<Literal>]
let private DirectoryAttribute = 16

[<Literal>]
let private NothingSaid = -1

let private modeOf (entry: ZipEntry) =
    uint32 entry.ExternalFileAttributes >>> 16

[<Fact>]
let ``a file entry made on Unix says it was made there`` () =
    let entry = ZipEntries.file Unix None "notes.txt" 5L

    Assert.Equal(UnixHost, entry.HostSystem)

[<Fact>]
let ``a file entry made on Unix carries the mode of an ordinary file`` () =
    let entry = ZipEntries.file Unix None "notes.txt" 5L

    Assert.Equal(OrdinaryFile, modeOf entry)

[<Fact>]
let ``a file entry made on Unix keeps a backslash in its name`` () =
    let entry = ZipEntries.file Unix None "a\\b.txt" 5L

    Assert.Equal("a\\b.txt", entry.Name)

[<Fact>]
let ``a file entry says the size it is given`` () =
    let entry = ZipEntries.file Unix None "notes.txt" 5L

    Assert.Equal(5L, entry.Size)

[<Fact>]
let ``a file entry made on Windows says nothing of where it was made`` () =
    let entry = ZipEntries.file Windows None "notes.txt" 5L

    Assert.Equal(FatHost, entry.HostSystem)
    Assert.Equal(NothingSaid, entry.ExternalFileAttributes)

[<Fact>]
let ``a directory entry made on Unix says it was made there`` () =
    let entry = ZipEntries.directory Unix None "docs"

    Assert.Equal(UnixHost, entry.HostSystem)

[<Fact>]
let ``a directory entry made on Unix carries the mode of a directory`` () =
    let entry = ZipEntries.directory Unix None "docs"

    Assert.Equal(OrdinaryDirectory, modeOf entry)

[<Fact>]
let ``a directory entry made on Unix is a directory in the field Windows reads too`` () =
    let entry = ZipEntries.directory Unix None "docs"

    Assert.Equal(DirectoryAttribute, entry.ExternalFileAttributes &&& DirectoryAttribute)

[<Fact>]
let ``a directory entry made on Unix keeps a backslash in its name`` () =
    let entry = ZipEntries.directory Unix None "dir\\below"

    Assert.Equal("dir\\below/", entry.Name)

[<Fact>]
let ``a directory entry made on Windows says nothing of where it was made`` () =
    let entry = ZipEntries.directory Windows None "docs"

    Assert.Equal(FatHost, entry.HostSystem)
    Assert.Equal(NothingSaid, entry.ExternalFileAttributes)

[<Fact>]
let ``a directory added on Unix keeps the backslash typed into its name`` () =
    let entry = ZipEntries.addedDirectory Unix None "made\\here"

    Assert.Equal("made\\here/", entry.Name)

[<Fact>]
let ``a directory added on Unix says it was made there`` () =
    let entry = ZipEntries.addedDirectory Unix None "docs"

    Assert.Equal(UnixHost, entry.HostSystem)
    Assert.Equal(OrdinaryDirectory, modeOf entry)

[<Fact>]
let ``a directory added on Unix says it holds nothing`` () =
    let entry = ZipEntries.addedDirectory Unix None "docs"

    Assert.Equal(0L, entry.Size)
    Assert.Equal(0L, entry.CompressedSize)

[<Fact>]
let ``a directory added on Windows has a backslash typed into its name read as a separator`` () =
    let entry = ZipEntries.addedDirectory Windows None "made\\here"

    Assert.Equal("made/here/", entry.Name)

[<Fact>]
let ``a directory added on Windows says it is one and nothing of Unix`` () =
    let entry = ZipEntries.addedDirectory Windows None "docs"

    Assert.Equal(FatHost, entry.HostSystem)
    Assert.Equal(DirectoryAttribute, entry.ExternalFileAttributes)

[<Fact>]
let ``a directory added on Windows says it holds nothing`` () =
    let entry = ZipEntries.addedDirectory Windows None "docs"

    Assert.Equal(0L, entry.Size)
    Assert.Equal(0L, entry.CompressedSize)

[<Fact>]
let ``a name holding a backslash is taken for a file`` () =
    let taken = ZipEntries.takesFile "a\\b.txt"

    Assert.True taken

[<Fact>]
let ``a name ending in a backslash is not taken for a file`` () =
    let taken = ZipEntries.takesFile "end\\"

    Assert.False taken
