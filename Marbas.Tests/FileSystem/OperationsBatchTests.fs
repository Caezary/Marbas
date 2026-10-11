module Marbas.Tests.FileSystem.OperationsBatchTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Tests
open Marbas.Tests.FileSystem.OperationsFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private sources (results: ItemResult list) = results |> List.map _.Source

let private watching cancelAfter =
    let reported = ResizeArray<FileSystemPath>()

    let control =
        { Report = reported.Add
          IsCancelled = fun () -> reported.Count >= cancelAfter }

    reported, control

[<Fact>]
let ``batch results carry the source and keep the input order`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/a.txt", MockFileData "a")
    fs.AddFile(at "data/b.txt", MockFileData "b")

    let results = deleteAll fs unattended [ fp (at "data/a.txt"); fp (at "data/b.txt") ]

    Assert.Equal<ItemSource list>(
        [ ItemOnDisk(fp (at "data/a.txt")); ItemOnDisk(fp (at "data/b.txt")) ],
        sources results
    )

    Assert.All(results, fun r -> Assert.Equal(Ok(), r.Outcome))

[<Fact>]
let ``a failing item does not stop the rest of the batch`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/b.txt", MockFileData "b")

    let results =
        deleteAll fs unattended [ fp (at "data/gone.txt"); fp (at "data/b.txt") ]

    Assert.Equal(Error(SourceNotFound(fp (at "data/gone.txt"))), results[0].Outcome)
    Assert.Equal(Ok(), results[1].Outcome)
    Assert.False(fs.File.Exists(at "data/b.txt"))

[<Fact>]
let ``a deleted tree names every entry it removes`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "work/docs/inner.txt", MockFileData "x")
    let reported, control = watching Int32.MaxValue

    delete fs control (fp (at "work")) |> Expect.ok

    Assert.Equal<FileSystemPath list>(
        [ fp (at "work"); fp (at "work/docs"); fp (at "work/docs/inner.txt") ],
        List.ofSeq reported
    )

[<Fact>]
let ``a cancelled delete names where it stopped and leaves the rest of the tree`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "work/docs/inner.txt", MockFileData "x")
    let _, control = watching 2

    let failure = delete fs control (fp (at "work")) |> Expect.error

    Assert.Equal(Cancelled(fp (at "work/docs/inner.txt")), failure)
    Assert.True(fs.File.Exists(at "work/docs/inner.txt"))

[<Fact>]
let ``deleteAll stops where it is cancelled`` () =
    let fs = twoSources ()
    let _, control = watching 1

    let results = deleteAll fs control bothSources

    Assert.Equal<ItemSource list>([ ItemOnDisk(fp (at "data/a.txt")) ], sources results)
    Assert.True(fs.File.Exists(at "data/b.txt"))

[<Fact>]
let ``trashAll names every source it hands over`` () =
    let fs = twoSources ()
    let reported, control = watching Int32.MaxValue

    trashAll (delete fs unattended) control bothSources |> ignore

    Assert.Equal<FileSystemPath list>(bothSources, List.ofSeq reported)

[<Fact>]
let ``trashAll stops where it is cancelled`` () =
    let fs = twoSources ()
    let _, control = watching 1

    let results = trashAll (delete fs unattended) control bothSources

    Assert.Equal<ItemSource list>([ ItemOnDisk(fp (at "data/a.txt")) ], sources results)
    Assert.True(fs.File.Exists(at "data/b.txt"))

[<Fact>]
let ``the whole outcome of a batch is its first failure`` () =
    let fs = twoSources ()

    let outcome =
        deleteAll fs unattended [ fp (at "data/gone.txt"); fp (at "data/a.txt") ]
        |> wholeOutcome

    Assert.Equal(Error(SourceNotFound(fp (at "data/gone.txt"))), outcome)

[<Fact>]
let ``the whole outcome of a batch that went through is Ok`` () =
    let fs = twoSources ()

    let outcome = deleteAll fs unattended bothSources |> wholeOutcome

    Assert.Equal(Ok(), outcome)

[<Fact>]
let ``copyAll copies every source it is given`` () =
    let fs = twoSources ()
    fs.AddFile(at "backup/a.txt", MockFileData "old a")

    let copied = copyAll fs unattended bothSources (fp (at "backup"))

    Assert.Equal<ItemSource list>(
        [ ItemOnDisk(fp (at "data/a.txt")); ItemOnDisk(fp (at "data/b.txt")) ],
        sources copied
    )

    Assert.Equal("new a", fs.File.ReadAllText(at "backup/a.txt"))
    Assert.Equal("new b", fs.File.ReadAllText(at "backup/b.txt"))

[<Fact>]
let ``moveAll moves every source it is given`` () =
    let fs = twoSources ()
    fs.AddDirectory(at "backup")

    let moved = moveAll fs unattended bothSources (fp (at "backup"))

    Assert.Equal<ItemSource list>([ ItemOnDisk(fp (at "data/a.txt")); ItemOnDisk(fp (at "data/b.txt")) ], sources moved)
    Assert.Equal("new a", fs.File.ReadAllText(at "backup/a.txt"))
    Assert.False(fs.File.Exists(at "data/a.txt"))

[<Fact>]
let ``a missing source is reported`` () =
    let fs = twoSources ()
    fs.AddDirectory(at "backup")

    let copied = copyAll fs unattended [ fp (at "data/gone.txt") ] (fp (at "backup"))

    Assert.Equal(Error(SourceNotFound(fp (at "data/gone.txt"))), (List.exactlyOne copied).Outcome)

[<Fact>]
let ``every file of a tree is reported as it is worked on`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/docs/a.txt", MockFileData "a")
    fs.AddFile(at "data/docs/deep/b.txt", MockFileData "b")
    fs.AddDirectory(at "backup")
    let reported, control = watching 99

    copyAll fs control [ fp (at "data/docs") ] (fp (at "backup")) |> ignore

    Assert.Equal<FileSystemPath list>(
        [ fp (at "data/docs/a.txt"); fp (at "data/docs/deep/b.txt") ],
        reported |> Seq.sort |> List.ofSeq
    )

[<Fact>]
let ``a cancelled batch leaves the sources it never reached alone`` () =
    let fs = twoSources ()
    fs.AddDirectory(at "backup")
    let _, control = watching 1

    let copied = copyAll fs control bothSources (fp (at "backup"))

    Assert.Equal<ItemSource list>([ ItemOnDisk(fp (at "data/a.txt")) ], sources copied)
    Assert.False(fs.File.Exists(at "backup/b.txt"))

[<Fact>]
let ``a tree cancelled halfway is reported as stopped`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/docs/a.txt", MockFileData "a")
    fs.AddFile(at "data/docs/b.txt", MockFileData "b")
    fs.AddDirectory(at "backup")
    let _, control = watching 1

    let copied = copyAll fs control [ fp (at "data/docs") ] (fp (at "backup"))

    match (List.exactlyOne copied).Outcome with
    | Error(Cancelled _) -> ()
    | other -> failwith $"expected a cancelled item, got %A{other}"

[<Fact>]
let ``a move cancelled before it starts keeps the source where it is`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/docs/a.txt", MockFileData "a")
    fs.AddFile(at "data/docs/b.txt", MockFileData "b")
    fs.AddDirectory(at "backup")
    let _, control = watching 0

    moveAll fs control [ fp (at "data/docs") ] (fp (at "backup")) |> ignore

    Assert.True(fs.Directory.Exists(at "data/docs"))
    Assert.Equal("a", fs.File.ReadAllText(at "data/docs/a.txt"))
    Assert.Equal("b", fs.File.ReadAllText(at "data/docs/b.txt"))

[<Fact>]
let ``a move that fell back to copying and was cancelled halfway keeps the source where it is`` () =
    let fs = Fakes.DirectoryMoveRefused()
    fs.AddFile(at "data/docs/a.txt", MockFileData "a")
    fs.AddFile(at "data/docs/b.txt", MockFileData "b")
    fs.AddDirectory(at "backup")
    let _, control = watching 2

    moveAll fs control [ fp (at "data/docs") ] (fp (at "backup")) |> ignore

    Assert.True(fs.Directory.Exists(at "data/docs"))
    Assert.Equal("a", fs.File.ReadAllText(at "data/docs/a.txt"))
    Assert.Equal("b", fs.File.ReadAllText(at "data/docs/b.txt"))

[<Fact>]
let ``failures keeps only the items that went wrong`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/b.txt", MockFileData "b")

    let failed =
        deleteAll fs unattended [ fp (at "data/gone.txt"); fp (at "data/b.txt") ]
        |> failures

    Assert.Equal<ItemSource list>([ ItemOnDisk(fp (at "data/gone.txt")) ], sources failed)

[<Fact>]
let ``copy reports a directory it may not read`` () =
    let fs = Fakes.UnreadableDirectory(at "data/docs")
    fs.AddFile(at "data/docs/inner.txt", MockFileData "inner")
    fs.AddDirectory(at "backup")

    let failure =
        copy fs unattended (fp (at "backup")) (fp (at "data/docs")) |> Expect.error

    Assert.Equal(AccessDenied(fp (at "data/docs")), failure)

[<Fact>]
let ``a directory that may not be read leaves the other sources to finish`` () =
    let fs = Fakes.UnreadableDirectory(at "data/docs")
    fs.AddFile(at "data/docs/inner.txt", MockFileData "inner")
    fs.AddFile(at "data/notes.txt", MockFileData "notes")
    fs.AddDirectory(at "backup")

    let copied =
        copyAll fs unattended [ fp (at "data/docs"); fp (at "data/notes.txt") ] (fp (at "backup"))

    Assert.Equal<ItemSource list>(
        [ ItemOnDisk(fp (at "data/docs")); ItemOnDisk(fp (at "data/notes.txt")) ],
        sources copied
    )

    Assert.Equal<ItemSource list>([ ItemOnDisk(fp (at "data/docs")) ], sources (failures copied))
    Assert.Equal("notes", fs.File.ReadAllText(at "backup/notes.txt"))

[<Fact>]
let ``guarded answers what work that finishes answers`` () =
    let work _ =
        [ { Source = ItemOnDisk(fp (at "data/notes.txt"))
            Outcome = Ok() } ]

    let results = guarded "Copying" work unattended

    Assert.Equal<ItemSource list>([ ItemOnDisk(fp (at "data/notes.txt")) ], sources results)

[<Fact>]
let ``guarded turns work that throws into one failure named after the batch`` () =
    let work _ : ItemResult list =
        raise (InvalidOperationException "no writer for tar")

    let results = guarded "Copying" work unattended

    let failure = List.exactlyOne results
    Assert.Equal<ItemSource>(WholeBatch "Copying", failure.Source)
    Assert.Equal(BatchFailed "no writer for tar", Expect.error failure.Outcome)
