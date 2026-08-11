module Marbas.Tests.FileSystem.OperationsTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Tests

let private fp = FileSystemPath.create

[<Fact>]
let ``makeDirectory creates a directory in the given parent`` () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\data"

    makeDirectory fs (fp @"C:\data") "new" |> Expect.ok

    Assert.True(fs.Directory.Exists @"C:\data\new")

[<Fact>]
let ``makeDirectory refuses an existing name`` () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\data\new"

    let failure = makeDirectory fs (fp @"C:\data") "new" |> Expect.error

    Assert.Equal(TargetExists(fp @"C:\data\new"), failure)

[<Fact>]
let ``delete removes a file`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\notes.txt", MockFileData "hello")

    delete fs (fp @"C:\data\notes.txt") |> Expect.ok

    Assert.False(fs.File.Exists @"C:\data\notes.txt")

[<Fact>]
let ``delete removes a directory with its contents`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\docs\deep\inner.txt", MockFileData "x")

    delete fs (fp @"C:\data\docs") |> Expect.ok

    Assert.False(fs.Directory.Exists @"C:\data\docs")

[<Fact>]
let ``delete reports a missing source`` () =
    let fs = MockFileSystem()

    let failure = delete fs (fp @"C:\data\gone.txt") |> Expect.error

    Assert.Equal(SourceNotFound(fp @"C:\data\gone.txt"), failure)

[<Fact>]
let ``copy places a file in the target directory`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\notes.txt", MockFileData "hello")
    fs.AddDirectory @"C:\backup"

    copy fs unattended (fp @"C:\backup") (fp @"C:\data\notes.txt") |> Expect.ok

    Assert.Equal("hello", fs.File.ReadAllText @"C:\backup\notes.txt")
    Assert.True(fs.File.Exists @"C:\data\notes.txt")

[<Fact>]
let ``copy replaces whatever is already there`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\notes.txt", MockFileData "new")
    fs.AddFile(@"C:\backup\notes.txt", MockFileData "old")

    copy fs unattended (fp @"C:\backup") (fp @"C:\data\notes.txt") |> Expect.ok

    Assert.Equal("new", fs.File.ReadAllText @"C:\backup\notes.txt")

[<Fact>]
let ``copy duplicates a whole directory tree`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\docs\deep\inner.txt", MockFileData "x")
    fs.AddFile(@"C:\data\docs\top.txt", MockFileData "y")
    fs.AddDirectory @"C:\backup"

    copy fs unattended (fp @"C:\backup") (fp @"C:\data\docs") |> Expect.ok

    Assert.Equal("x", fs.File.ReadAllText @"C:\backup\docs\deep\inner.txt")
    Assert.Equal("y", fs.File.ReadAllText @"C:\backup\docs\top.txt")

[<Fact>]
let ``copy reports a missing source`` () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\backup"

    let failure = copy fs unattended (fp @"C:\backup") (fp @"C:\data\gone.txt") |> Expect.error

    Assert.Equal(SourceNotFound(fp @"C:\data\gone.txt"), failure)

[<Fact>]
let ``move takes a file to the target directory`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\notes.txt", MockFileData "hello")
    fs.AddDirectory @"C:\backup"

    move fs unattended (fp @"C:\backup") (fp @"C:\data\notes.txt") |> Expect.ok

    Assert.Equal("hello", fs.File.ReadAllText @"C:\backup\notes.txt")
    Assert.False(fs.File.Exists @"C:\data\notes.txt")

[<Fact>]
let ``move takes a directory tree to the target directory`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\docs\inner.txt", MockFileData "x")
    fs.AddDirectory @"C:\backup"

    move fs unattended (fp @"C:\backup") (fp @"C:\data\docs") |> Expect.ok

    Assert.Equal("x", fs.File.ReadAllText @"C:\backup\docs\inner.txt")
    Assert.False(fs.Directory.Exists @"C:\data\docs")

[<Fact>]
let ``move replaces whatever is already there`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\notes.txt", MockFileData "new")
    fs.AddFile(@"C:\backup\notes.txt", MockFileData "old")

    move fs unattended (fp @"C:\backup") (fp @"C:\data\notes.txt") |> Expect.ok

    Assert.Equal("new", fs.File.ReadAllText @"C:\backup\notes.txt")
    Assert.False(fs.File.Exists @"C:\data\notes.txt")

[<Fact>]
let ``rename keeps the entry in its own directory`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\notes.txt", MockFileData "hello")

    rename fs (fp @"C:\data\notes.txt") "readme.txt" |> Expect.ok

    Assert.Equal("hello", fs.File.ReadAllText @"C:\data\readme.txt")
    Assert.False(fs.File.Exists @"C:\data\notes.txt")

[<Fact>]
let ``rename refuses an existing name`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\notes.txt", MockFileData "hello")
    fs.AddFile(@"C:\data\readme.txt", MockFileData "other")

    let failure = rename fs (fp @"C:\data\notes.txt") "readme.txt" |> Expect.error

    Assert.Equal(TargetExists(fp @"C:\data\readme.txt"), failure)

[<Fact>]
let ``batch results carry the entry name and keep the input order`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\a.txt", MockFileData "a")
    fs.AddFile(@"C:\data\b.txt", MockFileData "b")

    let results = deleteAll fs [ fp @"C:\data\a.txt"; fp @"C:\data\b.txt" ]

    Assert.Equal<string list>([ "a.txt"; "b.txt" ], results |> List.map _.Name)
    Assert.All(results, fun r -> Assert.Equal(Ok(), r.Outcome))

[<Fact>]
let ``a failing item does not stop the rest of the batch`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\b.txt", MockFileData "b")

    let results = deleteAll fs [ fp @"C:\data\gone.txt"; fp @"C:\data\b.txt" ]

    Assert.Equal(Error(SourceNotFound(fp @"C:\data\gone.txt")), results[0].Outcome)
    Assert.Equal(Ok(), results[1].Outcome)
    Assert.False(fs.File.Exists @"C:\data\b.txt")

let private answering answers =
    let asked = ResizeArray<FileSystemPath>()
    let remaining = ref answers

    let ask path =
        asked.Add path

        match remaining.Value with
        | answer :: rest ->
            remaining.Value <- rest
            answer
        | [] -> failwith $"unexpected question about %A{path}"

    asked, ask

let private never (path: FileSystemPath) = failwith $"unexpected question about %A{path}"

let private twoSources () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\a.txt", MockFileData "new a")
    fs.AddFile(@"C:\data\b.txt", MockFileData "new b")
    fs

let private bothSources = [ fp @"C:\data\a.txt"; fp @"C:\data\b.txt" ]

let private watching cancelAfter =
    let reported = ResizeArray<FileSystemPath>()

    let control =
        { Report = reported.Add
          IsCancelled = fun () -> reported.Count >= cancelAfter }

    reported, control

[<Fact>]
let ``approve keeps every source and asks nothing when nothing collides`` () =
    let fs = twoSources ()
    fs.AddDirectory @"C:\backup"

    let approved = approve fs never bothSources (fp @"C:\backup")

    Assert.Equal<FileSystemPath list>(bothSources, approved)

[<Fact>]
let ``a collision is asked about and yes keeps that source`` () =
    let fs = twoSources ()
    fs.AddFile(@"C:\backup\a.txt", MockFileData "old a")
    let asked, ask = answering [ Yes ]

    let approved = approve fs ask bothSources (fp @"C:\backup")

    Assert.Equal<FileSystemPath list>([ fp @"C:\backup\a.txt" ], List.ofSeq asked)
    Assert.Equal<FileSystemPath list>(bothSources, approved)

[<Fact>]
let ``no drops the colliding source and keeps the rest`` () =
    let fs = twoSources ()
    fs.AddFile(@"C:\backup\a.txt", MockFileData "old a")
    let _, ask = answering [ No ]

    let approved = approve fs ask bothSources (fp @"C:\backup")

    Assert.Equal<FileSystemPath list>([ fp @"C:\data\b.txt" ], approved)

[<Fact>]
let ``all answers the remaining collisions too`` () =
    let fs = twoSources ()
    fs.AddFile(@"C:\backup\a.txt", MockFileData "old a")
    fs.AddFile(@"C:\backup\b.txt", MockFileData "old b")
    let asked, ask = answering [ All ]

    let approved = approve fs ask bothSources (fp @"C:\backup")

    Assert.Single asked |> ignore
    Assert.Equal<FileSystemPath list>(bothSources, approved)

[<Fact>]
let ``cancel abandons the whole batch`` () =
    let fs = twoSources ()
    fs.AddFile(@"C:\backup\b.txt", MockFileData "old b")
    let _, ask = answering [ Cancel ]

    let approved = approve fs ask bothSources (fp @"C:\backup")

    Assert.Empty approved

[<Fact>]
let ``a colliding directory is asked about once, by its own name`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\docs\a.txt", MockFileData "new a")
    fs.AddFile(@"C:\backup\docs\a.txt", MockFileData "old a")
    let asked, ask = answering [ Yes ]

    let approved = approve fs ask [ fp @"C:\data\docs" ] (fp @"C:\backup")

    Assert.Equal<FileSystemPath list>([ fp @"C:\backup\docs" ], List.ofSeq asked)
    Assert.Equal<FileSystemPath list>([ fp @"C:\data\docs" ], approved)

[<Fact>]
let ``copyAll copies every source it is given`` () =
    let fs = twoSources ()
    fs.AddFile(@"C:\backup\a.txt", MockFileData "old a")

    let copied = copyAll fs unattended bothSources (fp @"C:\backup")

    Assert.Equal<string list>([ "a.txt"; "b.txt" ], copied |> List.map _.Name)
    Assert.Equal("new a", fs.File.ReadAllText @"C:\backup\a.txt")
    Assert.Equal("new b", fs.File.ReadAllText @"C:\backup\b.txt")

[<Fact>]
let ``moveAll moves every source it is given`` () =
    let fs = twoSources ()
    fs.AddDirectory @"C:\backup"

    let moved = moveAll fs unattended bothSources (fp @"C:\backup")

    Assert.Equal<string list>([ "a.txt"; "b.txt" ], moved |> List.map _.Name)
    Assert.Equal("new a", fs.File.ReadAllText @"C:\backup\a.txt")
    Assert.False(fs.File.Exists @"C:\data\a.txt")

[<Fact>]
let ``a missing source is reported`` () =
    let fs = twoSources ()
    fs.AddDirectory @"C:\backup"

    let copied = copyAll fs unattended [ fp @"C:\data\gone.txt" ] (fp @"C:\backup")

    Assert.Equal(Error(SourceNotFound(fp @"C:\data\gone.txt")), (List.exactlyOne copied).Outcome)

[<Fact>]
let ``every file of a tree is reported as it is worked on`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\docs\a.txt", MockFileData "a")
    fs.AddFile(@"C:\data\docs\deep\b.txt", MockFileData "b")
    fs.AddDirectory @"C:\backup"
    let reported, control = watching 99

    copyAll fs control [ fp @"C:\data\docs" ] (fp @"C:\backup") |> ignore

    Assert.Equal<FileSystemPath list>(
        [ fp @"C:\data\docs\a.txt"; fp @"C:\data\docs\deep\b.txt" ],
        reported |> Seq.sort |> List.ofSeq
    )

[<Fact>]
let ``a cancelled batch leaves the sources it never reached alone`` () =
    let fs = twoSources ()
    fs.AddDirectory @"C:\backup"
    let _, control = watching 1

    let copied = copyAll fs control bothSources (fp @"C:\backup")

    Assert.Equal<string list>([ "a.txt" ], copied |> List.map _.Name)
    Assert.False(fs.File.Exists @"C:\backup\b.txt")

[<Fact>]
let ``a tree cancelled halfway is reported as stopped`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\docs\a.txt", MockFileData "a")
    fs.AddFile(@"C:\data\docs\b.txt", MockFileData "b")
    fs.AddDirectory @"C:\backup"
    let _, control = watching 1

    let copied = copyAll fs control [ fp @"C:\data\docs" ] (fp @"C:\backup")

    match (List.exactlyOne copied).Outcome with
    | Error(Cancelled _) -> ()
    | other -> failwith $"expected a cancelled item, got %A{other}"

[<Fact>]
let ``a move cancelled halfway keeps the source where it is`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\docs\a.txt", MockFileData "a")
    fs.AddFile(@"C:\data\docs\b.txt", MockFileData "b")
    fs.AddDirectory @"C:\backup"
    let _, control = watching 1

    moveAll fs control [ fp @"C:\data\docs" ] (fp @"C:\backup") |> ignore

    Assert.True(fs.Directory.Exists @"C:\data\docs")
    Assert.Equal("a", fs.File.ReadAllText @"C:\data\docs\a.txt")
    Assert.Equal("b", fs.File.ReadAllText @"C:\data\docs\b.txt")

[<Fact>]
let ``a directory holding entries is non-empty`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\docs\inner.txt", MockFileData "x")

    let nonEmpty = isNonEmptyDirectory fs (fp @"C:\data\docs")

    Assert.True nonEmpty

[<Fact>]
let ``an empty directory and a file are not non-empty directories`` () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\data\empty"
    fs.AddFile(@"C:\data\notes.txt", MockFileData "x")

    let emptyDirectory = isNonEmptyDirectory fs (fp @"C:\data\empty")
    let file = isNonEmptyDirectory fs (fp @"C:\data\notes.txt")

    Assert.False emptyDirectory
    Assert.False file

[<Fact>]
let ``failures keeps only the items that went wrong`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\b.txt", MockFileData "b")

    let failed = deleteAll fs [ fp @"C:\data\gone.txt"; fp @"C:\data\b.txt" ] |> failures

    Assert.Equal<string list>([ "gone.txt" ], failed |> List.map _.Name)

[<Fact>]
let ``access denied maps to an error value`` () =
    let denied = UnauthorizedAccessException()

    let operationError = mapException (fp @"C:\locked") denied

    Assert.Equal(AccessDenied(fp @"C:\locked"), operationError)

[<Fact>]
let ``unexpected failures map to an error value`` () =
    let failure = IO.IOException "boom"

    let operationError = mapException (fp @"C:\bad") failure

    Assert.Equal(OperationFailed(fp @"C:\bad", "boom"), operationError)
