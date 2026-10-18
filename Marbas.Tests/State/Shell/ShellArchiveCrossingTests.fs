module Marbas.Tests.State.Shell.ShellArchiveCrossingTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Archives
open Marbas.FileSystem.Operations
open Marbas.Domain
open Marbas.State
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.Shell.ShellFixture
open Marbas.Tests.State.Shell.ShellArchiveFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private otherAt = fp (at "start/other.zip")

let private twoArchives () =
    let fs = unpackReadyFs None

    Archive.write
        fs
        unattended
        { Path = otherAt
          Format = zipFormat
          Compression = Normal
          Password = None }
        [ fp (at "start/work/notes.txt") ]
    |> ignore

    fs

let private namesIn (fs: MockFileSystem) archive level =
    ArchiveListing.read fs archive level
    |> Expect.ok
    |> List.map (_.Name >> EntryName.value)
    |> List.sort

[<Fact>]
let ``an entry copied across archives lands in the other and stays in its own`` () =
    let sent, dispatch = capture ()
    let fs = twoArchives ()

    let deps =
        { noDeps () with
            FileSystem = fs
            WorkspacePath = fp (at "temp/Marbas/session") }

    Shell.interpret
        deps
        dispatch
        (CopyPlaces([ FromArchive(dataZip, [ entry "docs" ]) ], ToArchive(otherAt, ArchivePath.root)))

    Assert.Equal<string list>([ "docs"; "notes.txt" ], namesIn fs otherAt ArchivePath.root)
    Assert.Equal<string list>([ "inner.txt" ], namesIn fs otherAt (entry "docs"))
    Assert.Equal<string list>([ "docs"; "notes.txt" ], namesIn fs dataZip ArchivePath.root)

    Assert.Equal<Message list>(
        [ CopyFinished
              [ { Source = ItemInArchive(dataZip, entry "docs")
                  Outcome = Ok() } ] ],
        List.ofSeq sent
    )

[<Fact>]
let ``an entry moved across archives leaves the one it came from`` () =
    let _, dispatch = capture ()
    let fs = twoArchives ()

    let deps =
        { noDeps () with
            FileSystem = fs
            WorkspacePath = fp (at "temp/Marbas/session") }

    Shell.interpret
        deps
        dispatch
        (MovePlaces([ FromArchive(dataZip, [ entry "docs" ]) ], ToArchive(otherAt, ArchivePath.root)))

    Assert.Equal<string list>([ "docs"; "notes.txt" ], namesIn fs otherAt ArchivePath.root)
    Assert.Equal<string list>([ "notes.txt" ], namesIn fs dataZip ArchivePath.root)

[<Fact>]
let ``a name already at the target level of the other archive is asked about`` () =
    let _, dispatch = capture ()
    let fs = twoArchives ()
    let asked = ResizeArray<FileSystemPath>()

    let deps =
        { noDeps () with
            FileSystem = fs
            WorkspacePath = fp (at "temp/Marbas/session")
            Prompts.AskOverwrite =
                fun request ->
                    asked.Add request.Target
                    No }

    Shell.interpret
        deps
        dispatch
        (CopyPlaces([ FromArchive(dataZip, [ entry "notes.txt" ]) ], ToArchive(otherAt, ArchivePath.root)))

    Assert.Equal(at "start/other.zip/notes.txt", FileSystemPath.value (Assert.Single asked))

[<Fact>]
let ``the workspace is empty once a crossing has finished`` () =
    let _, dispatch = capture ()
    let fs = twoArchives ()

    let deps =
        { noDeps () with
            FileSystem = fs
            WorkspacePath = fp (at "temp/Marbas/session") }

    Shell.interpret
        deps
        dispatch
        (CopyPlaces([ FromArchive(dataZip, [ entry "docs" ]) ], ToArchive(otherAt, ArchivePath.root)))

    Assert.Empty(fs.Directory.EnumerateFileSystemEntries(at "temp/Marbas/session"))

[<Fact>]
let ``a crossing out of a locked archive asks for its password once`` () =
    let _, dispatch = capture ()
    let fs = unpackReadyFs (ArchivePassword.tryCreate "opensesame")

    Archive.write
        fs
        unattended
        { Path = otherAt
          Format = zipFormat
          Compression = Normal
          Password = None }
        [ fp (at "start/work/notes.txt") ]
    |> ignore

    let asked = ResizeArray<EntryName>()

    let deps =
        { noDeps () with
            FileSystem = fs
            WorkspacePath = fp (at "temp/Marbas/session")
            Prompts.AskPassword =
                fun request ->
                    asked.Add request.Archive
                    ArchivePassword.tryCreate "opensesame" }

    Shell.interpret
        deps
        dispatch
        (CopyPlaces([ FromArchive(dataZip, [ entry "docs" ]) ], ToArchive(otherAt, ArchivePath.root)))

    Assert.Single asked |> ignore
    Assert.Equal<string list>([ "docs"; "notes.txt" ], namesIn fs otherAt ArchivePath.root)
