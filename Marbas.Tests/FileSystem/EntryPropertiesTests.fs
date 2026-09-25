module Marbas.Tests.FileSystem.EntryPropertiesTests

open System
open System.IO
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs

let private read fs path = EntryProperties.read fs path |> Expect.ok

[<Fact>]
let ``the properties of a directory are read off it`` () =
    let fs = mockFs ()
    let stamp = DateTimeOffset(DateTime(2024, 3, 1, 10, 30, 0))
    fs.DirectoryInfo.New(at "start/docs").CreationTime <- stamp.LocalDateTime

    let properties = read fs (fp (at "start/docs"))

    Assert.Equal(fp (at "start/docs"), properties.Path)
    Assert.Equal(stamp, properties.Created)

[<Fact>]
let ``a directory the file system marks hidden reads as hidden`` () =
    let fs = mockFs ()
    let info = fs.DirectoryInfo.New(at "start/docs")
    info.Attributes <- info.Attributes ||| FileAttributes.Hidden

    let properties = read fs (fp (at "start/docs"))

    Assert.True(EntryProperties.has HiddenFlag properties)
    Assert.False(EntryProperties.has ReadOnlyFlag properties)

[<Fact>]
let ``the properties of a file are read off it`` () =
    let fs = mockFs ()

    let properties = read fs (fp (at "start/notes.txt"))

    Assert.False(EntryProperties.has HiddenFlag properties)

[<Fact>]
let ``an entry that is not there has no properties`` () =
    let fs = mockFs ()

    let outcome = EntryProperties.read fs (fp (at "start/gone"))

    Assert.Equal(Error(Operations.SourceNotFound(fp (at "start/gone"))), outcome)

[<Fact>]
let ``a raised flag is held by the properties`` () =
    let fs = mockFs ()
    let properties = read fs (fp (at "start/docs"))

    let hidden = EntryProperties.withFlag HiddenFlag properties

    Assert.True(EntryProperties.has HiddenFlag hidden)

[<Fact>]
let ``a lowered flag is held no longer`` () =
    let fs = mockFs ()
    let hidden = read fs (fp (at "start/docs")) |> EntryProperties.withFlag HiddenFlag

    let lowered = EntryProperties.withoutFlag HiddenFlag hidden

    Assert.False(EntryProperties.has HiddenFlag lowered)

[<Fact>]
let ``the flags written to a directory are read back from it`` () =
    let fs = mockFs ()
    let path = fp (at "start/docs")

    read fs path
    |> EntryProperties.withFlag HiddenFlag
    |> EntryProperties.withFlag ReadOnlyFlag
    |> EntryProperties.write fs
    |> Expect.ok

    let written = read fs path

    Assert.True(EntryProperties.has HiddenFlag written)
    Assert.True(EntryProperties.has ReadOnlyFlag written)

[<Fact>]
let ``clearing a flag takes it off the entry`` () =
    let fs = mockFs ()
    let path = fp (at "start/docs")
    read fs path |> EntryProperties.withFlag HiddenFlag |> EntryProperties.write fs |> Expect.ok

    read fs path
    |> EntryProperties.withoutFlag HiddenFlag
    |> EntryProperties.write fs
    |> Expect.ok

    Assert.False(EntryProperties.has HiddenFlag (read fs path))

[<Fact>]
let ``writing the properties keeps the entry a directory`` () =
    let fs = mockFs ()
    let path = fp (at "start/docs")

    read fs path |> EntryProperties.withFlag ArchiveFlag |> EntryProperties.write fs |> Expect.ok

    Assert.Equal(DirectoryEntry, FileSystemPath.kind fs path)

[<Fact>]
let ``the timestamps written to a directory are read back from it`` () =
    let fs = mockFs ()
    let path = fp (at "start/docs")
    let created = DateTimeOffset(DateTime(2001, 2, 3, 4, 5, 6))
    let modified = DateTimeOffset(DateTime(2002, 3, 4, 5, 6, 7))

    read fs path
    |> fun properties ->
        { properties with
            Created = created
            Modified = modified }
    |> EntryProperties.write fs
    |> Expect.ok

    let written = read fs path

    Assert.Equal(created, written.Created)
    Assert.Equal(modified, written.Modified)

[<Fact>]
let ``a stamp written in another offset comes back as the same moment`` () =
    let fs = mockFs ()
    let path = fp (at "start/docs")
    let elsewhere = DateTimeOffset(DateTime(2001, 2, 3, 4, 5, 6), TimeSpan.FromHours 5.0)
    let properties = read fs path

    { properties with Created = elsewhere } |> EntryProperties.write fs |> Expect.ok

    Assert.Equal(elsewhere, (read fs path).Created)

[<Fact>]
let ``properties cannot be written to an entry that is gone`` () =
    let fs = mockFs ()
    let properties = read fs (fp (at "start/docs"))
    fs.Directory.Delete(at "start/docs")

    let outcome = EntryProperties.write fs properties

    Assert.Equal(Error(Operations.SourceNotFound(fp (at "start/docs"))), outcome)

[<Fact>]
let ``every flag has a name to put on a screen`` () =
    let named = EntryProperties.flags |> List.map EntryProperties.label

    Assert.Equal<string list>([ "Read-only"; "Hidden"; "System"; "Archive" ], named)
