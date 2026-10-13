module Marbas.Tests.FileSystem.DirectoryListingTests

open System
open System.IO
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

[<Fact>]
let ``lists files and directories of the given path`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "data/docs")
    fs.AddFile(at "data/notes.txt", MockFileData "hello")

    let entries = read fs (fp (at "data")) |> Expect.ok

    Assert.Equal<string list>([ "docs"; "notes.txt" ], entries |> List.map (_.Name >> EntryName.value) |> List.sort)

[<Fact>]
let ``directories have no size and files report theirs`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "data/docs")
    fs.AddFile(at "data/notes.txt", MockFileData "hello")

    let entries = read fs (fp (at "data")) |> Expect.ok

    let byName name =
        entries |> List.find (fun e -> e.Name = EntryName.create name)

    Assert.Equal(Directory, (byName "docs").Kind)
    Assert.Equal(None, (byName "docs").Size)
    Assert.Equal(File, (byName "notes.txt").Kind)
    Assert.Equal(Some 5L, (byName "notes.txt").Size)

[<Fact>]
let ``does not descend into nested directories`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/docs/inner.txt", MockFileData "x")

    let entries = read fs (fp (at "data")) |> Expect.ok

    Assert.Equal<string list>([ "docs" ], entries |> List.map (_.Name >> EntryName.value))

[<Fact>]
let ``reports last modification time`` () =
    let fs = MockFileSystem()
    let modified = DateTime(2026, 7, 1, 12, 30, 0)
    let file = MockFileData "x"
    file.LastWriteTime <- DateTimeOffset modified
    fs.AddFile(at "data/notes.txt", file)

    let entries = read fs (fp (at "data")) |> Expect.ok

    Assert.Equal(modified, (List.exactlyOne entries).Modified)

[<Fact>]
let ``a directory link carries what it points at`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "data/docs")
    fs.Directory.CreateSymbolicLink(at "data/twin", at "data/docs") |> ignore

    let entries = read fs (fp (at "data")) |> Expect.ok
    let twin = entries |> List.find (fun e -> e.Name = EntryName.create "twin")

    Assert.Equal(Directory, twin.Kind)
    Assert.Equal(Some(fp (at "data/docs")), twin.LinkTarget)

[<Fact>]
let ``a file link carries what it points at`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/notes.txt", MockFileData "hello")
    fs.File.CreateSymbolicLink(at "data/twin.txt", at "data/notes.txt") |> ignore

    let entries = read fs (fp (at "data")) |> Expect.ok
    let twin = entries |> List.find (fun e -> e.Name = EntryName.create "twin.txt")

    Assert.Equal(File, twin.Kind)
    Assert.Equal(Some(fp (at "data/notes.txt")), twin.LinkTarget)

[<Fact>]
let ``an entry that is no link points at nothing`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "data/docs")
    fs.AddFile(at "data/notes.txt", MockFileData "hello")

    let entries = read fs (fp (at "data")) |> Expect.ok

    Assert.Equal<FileSystemPath option list>([ None; None ], entries |> List.map _.LinkTarget)

[<Fact>]
let ``missing directory is an error value, not an exception`` () =
    let fs = MockFileSystem()

    let result = read fs (fp (at "nope"))

    Assert.Equal(Error(DirectoryNotFound(fp (at "nope"))), result)

[<Fact>]
let ``access denied maps to an error value`` () =
    let denied = UnauthorizedAccessException()

    let listingError = mapException (fp (at "locked")) denied

    Assert.Equal(AccessDenied(fp (at "locked")), listingError)

[<Fact>]
let ``unexpected failures map to an error value`` () =
    let failure = IO.IOException "boom"

    let listingError = mapException (fp (at "bad")) failure

    Assert.Equal(ListingFailed(fp (at "bad"), "boom"), listingError)

let private walked () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "data/docs")
    fs.AddFile(at "data/notes.txt", MockFileData "hello")
    fs

let private spelled (children: Child list) =
    children
    |> List.map (fun child -> FileSystemPath.value child.Path)
    |> List.sort
    |> String.concat ","

[<Fact>]
let ``a child knows both where it is and what it is`` () =
    let fs = walked ()

    let walk = children fs (fp (at "data"))

    Assert.Equal($"""{at "data/docs"},{at "data/notes.txt"}""", spelled walk)

[<Fact>]
let ``a child carries what one enumeration already said`` () =
    let fs = walked ()

    let notes =
        children fs (fp (at "data")) |> List.find (fun child -> child.Entry.Kind = File)

    Assert.Equal(Some 5L, notes.Entry.Size)

[<Fact>]
let ``an entry is answered for the path it stands at`` () =
    let fs = walked ()

    let entry = existingEntry fs (fp (at "data/notes.txt")) |> Expect.some

    Assert.Equal(File, entry.Kind)

[<Fact>]
let ``a directory is answered for the path it stands at`` () =
    let fs = walked ()

    let entry = existingEntry fs (fp (at "data/docs")) |> Expect.some

    Assert.Equal(Directory, entry.Kind)

[<Fact>]
let ``a path that stands for nothing has no entry`` () =
    let fs = walked ()

    let entry = existingEntry fs (fp (at "data/gone.txt"))

    Assert.Equal(None, entry)

[<Fact>]
let ``the entry leading up is a plain directory named two dots, dated as it is told`` () =
    let moment = DateTimeOffset(2024, 3, 1, 9, 5, 7, TimeSpan.Zero)

    let entry = parentEntry moment

    Assert.Equal(
        { Name = ParentName
          Kind = Directory
          Size = None
          Measured = None
          Modified = moment
          Visibility = Plain
          Executability = NotExecutable
          LinkTarget = None
          Verification = Verified },
        entry
    )

let private ownerRuns =
    UnixFileMode.UserRead ||| UnixFileMode.UserWrite ||| UnixFileMode.UserExecute

let private ownerWrites = UnixFileMode.UserRead ||| UnixFileMode.UserWrite

let private moded (mode: UnixFileMode) =
    let data = MockFileData "#!/bin/sh"
    data.UnixMode <- mode
    data

let private withModes () =
    let fs = Fakes.ModeWitness()
    fs.AddDirectory(at "data/docs")
    fs.AddFile(at "data/tool", moded ownerRuns)
    fs.AddFile(at "data/notes", moded ownerWrites)
    fs.AddFile(at "data/setup.exe", moded ownerWrites)
    fs

let private executabilityOf name (entries: Entry list) =
    (entries |> List.find (fun entry -> entry.Name = EntryName.create name)).Executability

[<Fact>]
let ``on Unix a file with its executable bit set is listed as a program`` () =
    let fs = withModes ()

    let entries = readOn Unix fs (fp (at "data")) |> Expect.ok

    Assert.Equal(Executable, executabilityOf "tool" entries)

[<Fact>]
let ``on Unix a file without the bit is listed as a plain file`` () =
    let fs = withModes ()

    let entries = readOn Unix fs (fp (at "data")) |> Expect.ok

    Assert.Equal(NotExecutable, executabilityOf "notes" entries)

[<Fact>]
let ``on Unix a program's extension still makes a program of a file without the bit`` () =
    let fs = withModes ()

    let entries = readOn Unix fs (fp (at "data")) |> Expect.ok

    Assert.Equal(Executable, executabilityOf "setup.exe" entries)

[<Fact>]
let ``on Unix a directory is no program, whatever its mode`` () =
    let fs = withModes ()

    let entries = readOn Unix fs (fp (at "data")) |> Expect.ok

    Assert.Equal(NotExecutable, executabilityOf "docs" entries)

[<Fact>]
let ``on Windows the bit makes no program of a file`` () =
    let fs = withModes ()

    let entries = readOn Windows fs (fp (at "data")) |> Expect.ok

    Assert.Equal(NotExecutable, executabilityOf "tool" entries)

[<Fact>]
let ``on Windows a program is known by its extension`` () =
    let fs = withModes ()

    let entries = readOn Windows fs (fp (at "data")) |> Expect.ok

    Assert.Equal(Executable, executabilityOf "setup.exe" entries)

[<Fact>]
let ``on Unix the mode is asked once for each file and for no directory`` () =
    let fs = withModes ()

    readOn Unix fs (fp (at "data")) |> ignore

    Assert.Equal<string list>([ at "data/notes"; at "data/setup.exe"; at "data/tool" ], fs.AskedForMode)

[<Fact>]
let ``on Windows no file is asked for its mode`` () =
    let fs = withModes ()

    readOn Windows fs (fp (at "data")) |> ignore

    Assert.Empty fs.AskedForMode

[<Fact>]
let ``a walk asks no file for its mode`` () =
    let fs = withModes ()

    children fs (fp (at "data")) |> ignore

    Assert.Empty fs.AskedForMode

[<Fact>]
let ``a single entry looked up asks nothing for its mode`` () =
    let fs = withModes ()

    existingEntry fs (fp (at "data/tool")) |> ignore

    Assert.Empty fs.AskedForMode

[<Fact>]
let ``a file whose mode cannot be read is listed as a plain file and the listing stands`` () =
    let fs = Fakes.ModeWitness [ at "data/gone" ]
    fs.AddFile(at "data/gone", moded ownerRuns)
    fs.AddFile(at "data/tool", moded ownerRuns)

    let entries = readOn Unix fs (fp (at "data")) |> Expect.ok

    Assert.Equal(NotExecutable, executabilityOf "gone" entries)
    Assert.Equal(Executable, executabilityOf "tool" entries)

[<Fact>]
let ``the listing asks as the platform of its file system asks`` () =
    let fs = withModes ()
    let asPlatform = readOn (FileSystemPath.platformOf fs) fs (fp (at "data"))

    let entries = read fs (fp (at "data"))

    Assert.Equal(asPlatform, entries)
