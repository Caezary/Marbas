module Marbas.Tests.State.Shell.ShellPlacesTests

open System.Formats.Tar
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.State
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.Shell.ShellFixture
open Marbas.Tests.State.Shell.ShellArchiveFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private zipAt = fp (at "start/data.zip")

let private tarAt = fp (at "start/old.tar")

let private placesFs password =
    let fs = MockFileSystem()
    fs.AddFile(at "start/loose.txt", MockFileData "loose")
    fs.AddFile(at "work/notes.txt", MockFileData "notes")
    fs.AddFile(at "work/docs/inner.txt", MockFileData "inner")
    fs.AddDirectory(at "backup")

    Archive.write
        fs
        unattended
        { Path = zipAt
          Format = zipFormat
          Compression = Normal
          Password = password }
        [ fp (at "work/notes.txt"); fp (at "work/docs") ]
    |> ignore

    use file = fs.File.Create(at "start/old.tar")
    use tar = new TarWriter(file, TarEntryFormat.Pax, true)
    use kept = fs.File.OpenRead(at "work/notes.txt")
    tar.WriteEntry(PaxTarEntry(TarEntryType.RegularFile, "kept.txt", DataStream = kept))
    fs

let private places =
    [ FromDisk [ fp (at "start/loose.txt") ]
      FromArchive(zipAt, [ entry "notes.txt" ])
      FromArchive(tarAt, [ entry "kept.txt" ]) ]

let private zipNames (fs: MockFileSystem) =
    ArchiveListing.read fs zipAt ArchivePath.root
    |> Expect.ok
    |> List.map (_.Name >> EntryName.value)
    |> List.sort

let private recording (fs: MockFileSystem) (titles: ResizeArray<string>) =
    { Fakes.dependencies fs (at "start") with
        Runners.Batch =
            fun request ->
                titles.Add request.Title
                request.Work unattended |> request.Finish }

let private refusedTar =
    { Source = ItemOnDisk tarAt
      Outcome = Error(ArchiveFailed(KindCannotBeChanged tarAt)) }

[<Fact>]
let ``a mark on the disk, one in a zip and one in a tar copy out in one batch`` () =
    let sent, dispatch = capture ()
    let fs = placesFs None
    let titles = ResizeArray<string>()

    Shell.interpret (recording fs titles) dispatch (CopyPlaces(places, ToDirectory(fp (at "backup"))))

    Assert.Equal("loose", fs.File.ReadAllText(at "backup/loose.txt"))
    Assert.Equal("notes", fs.File.ReadAllText(at "backup/notes.txt"))
    Assert.Equal("notes", fs.File.ReadAllText(at "backup/kept.txt"))
    Assert.Equal<string list>([ Shell.CopyTitle ], List.ofSeq titles)

    Assert.Equal<Message list>(
        [ CopyFinished
              [ { Source = ItemOnDisk(fp (at "start/loose.txt"))
                  Outcome = Ok() }
                { Source = ItemInArchive(zipAt, entry "notes.txt")
                  Outcome = Ok() }
                { Source = ItemInArchive(tarAt, entry "kept.txt")
                  Outcome = Ok() } ] ],
        List.ofSeq sent
    )

[<Fact>]
let ``moving the same three moves two and refuses the tar by name`` () =
    let sent, dispatch = capture ()
    let fs = placesFs None
    let titles = ResizeArray<string>()

    Shell.interpret (recording fs titles) dispatch (MovePlaces(places, ToDirectory(fp (at "backup"))))

    Assert.False(fs.File.Exists(at "start/loose.txt"))
    Assert.True(fs.File.Exists(at "backup/loose.txt"))
    Assert.True(fs.File.Exists(at "backup/notes.txt"))
    Assert.False(fs.File.Exists(at "backup/kept.txt"))
    Assert.Equal<string list>([ "docs" ], zipNames fs)
    Assert.Equal<string list>([ Shell.MoveTitle ], List.ofSeq titles)

    Assert.Equal<Message list>(
        [ MoveFinished
              [ { Source = ItemOnDisk(fp (at "start/loose.txt"))
                  Outcome = Ok() }
                { Source = ItemInArchive(zipAt, entry "notes.txt")
                  Outcome = Ok() }
                refusedTar ] ],
        List.ofSeq sent
    )

[<Fact>]
let ``deleting the same three removes two and refuses the tar by name`` () =
    let sent, dispatch = capture ()
    let fs = placesFs None
    let titles = ResizeArray<string>()

    Shell.interpret (recording fs titles) dispatch (DeletePlaces(places, Permanently))

    Assert.False(fs.File.Exists(at "start/loose.txt"))
    Assert.Equal<string list>([ "docs" ], zipNames fs)
    Assert.Equal<string list>([ Shell.DeleteTitle ], List.ofSeq titles)

    Assert.Equal<Message list>(
        [ DeleteFinished
              [ { Source = ItemOnDisk(fp (at "start/loose.txt"))
                  Outcome = Ok() }
                { Source = ItemInArchive(zipAt, entry "notes.txt")
                  Outcome = Ok() }
                refusedTar ] ],
        List.ofSeq sent
    )

[<Fact>]
let ``wiping the three runs under the wiping title`` () =
    let _, dispatch = capture ()
    let fs = placesFs None
    let titles = ResizeArray<string>()

    Shell.interpret (recording fs titles) dispatch (DeletePlaces(places, ByWiping))

    Assert.False(fs.File.Exists(at "start/loose.txt"))
    Assert.Equal<string list>([ Shell.WipeTitle ], List.ofSeq titles)

[<Fact>]
let ``a batch stopped before it starts touches none of the places`` () =
    let _, dispatch = capture ()
    let fs = placesFs None

    let deps =
        { Fakes.dependencies fs (at "start") with
            Runners.Batch = fun request -> request.Work stopped |> request.Finish }

    Shell.interpret deps dispatch (MovePlaces(places, ToDirectory(fp (at "backup"))))

    Assert.True(fs.File.Exists(at "start/loose.txt"))
    Assert.Equal<string list>([ "docs"; "notes.txt" ], zipNames fs)

[<Fact>]
let ``a protected archive is asked for its password once for all its marks`` () =
    let _, dispatch = capture ()
    let fs = placesFs (ArchivePassword.tryCreate "opensesame")
    let mutable asked = 0

    let deps =
        { recording fs (ResizeArray()) with
            Prompts.AskPassword =
                fun _ ->
                    asked <- asked + 1
                    ArchivePassword.tryCreate "opensesame" }

    let marks = [ FromArchive(zipAt, [ entry "notes.txt"; entry "docs/inner.txt" ]) ]

    Shell.interpret deps dispatch (CopyPlaces(marks, ToDirectory(fp (at "backup"))))

    Assert.Equal(1, asked)
    Assert.Equal("inner", fs.File.ReadAllText(at "backup/inner.txt"))

[<Fact>]
let ``a password turned down leaves its archive out and the rest still runs`` () =
    let _, dispatch = capture ()
    let fs = placesFs (ArchivePassword.tryCreate "opensesame")

    let deps =
        { recording fs (ResizeArray()) with
            Prompts.AskPassword = fun _ -> None }

    Shell.interpret deps dispatch (CopyPlaces(places, ToDirectory(fp (at "backup"))))

    Assert.True(fs.File.Exists(at "backup/loose.txt"))
    Assert.False(fs.File.Exists(at "backup/notes.txt"))
    Assert.True(fs.File.Exists(at "backup/kept.txt"))

[<Fact>]
let ``a copy into a tar is refused as a failed item from one place and from two`` () =
    let sent, dispatch = capture ()
    let fs = placesFs None
    let mutable reported = ("", "")

    let deps =
        { recording fs (ResizeArray()) with
            Prompts.ReportError = fun report -> reported <- (report.Title, report.Message) }

    let intoTar = ToArchive(tarAt, ArchivePath.root)

    let scattered =
        [ FromDisk [ fp (at "start/loose.txt") ]
          FromDisk [ fp (at "work/notes.txt") ] ]

    Shell.interpret deps dispatch (CopyPlaces([ FromDisk [ fp (at "start/loose.txt") ] ], intoTar))
    Shell.interpret deps dispatch (CopyPlaces(scattered, intoTar))

    Assert.Equal<Message list>(
        [ CopyFinished [ refusedTar ]; CopyFinished [ refusedTar; refusedTar ] ],
        List.ofSeq sent
    )

    Assert.Equal(("", ""), reported)
