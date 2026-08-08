module Marbas.Tests.OperationsTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Operations

let private fp = FileSystemPath.create

let private expectOk result =
    match result with
    | Ok() -> ()
    | Error err -> failwith $"expected Ok, got Error %A{err}"

let private expectError result =
    match result with
    | Ok() -> failwith "expected Error, got Ok"
    | Error err -> err

[<Fact>]
let ``makeDirectory creates a directory in the given parent`` () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\data"

    makeDirectory fs (fp @"C:\data") "new" |> expectOk

    Assert.True(fs.Directory.Exists @"C:\data\new")

[<Fact>]
let ``makeDirectory refuses an existing name`` () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\data\new"

    let errorResult = makeDirectory fs (fp @"C:\data") "new" |> expectError

    Assert.Equal(TargetExists(fp @"C:\data\new"), errorResult)

[<Fact>]
let ``delete removes a file`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\notes.txt", MockFileData "hello")

    delete fs (fp @"C:\data\notes.txt") |> expectOk

    Assert.False(fs.File.Exists @"C:\data\notes.txt")

[<Fact>]
let ``delete removes a directory with its contents`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\docs\deep\inner.txt", MockFileData "x")

    delete fs (fp @"C:\data\docs") |> expectOk

    Assert.False(fs.Directory.Exists @"C:\data\docs")

[<Fact>]
let ``delete reports a missing source`` () =
    let fs = MockFileSystem()

    let errorResult = delete fs (fp @"C:\data\gone.txt") |> expectError

    Assert.Equal(SourceNotFound(fp @"C:\data\gone.txt"), errorResult)

[<Fact>]
let ``copy places a file in the target directory`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\notes.txt", MockFileData "hello")
    fs.AddDirectory @"C:\backup"

    copy fs false (fp @"C:\backup") (fp @"C:\data\notes.txt") |> expectOk

    Assert.Equal("hello", fs.File.ReadAllText @"C:\backup\notes.txt")
    Assert.True(fs.File.Exists @"C:\data\notes.txt")

[<Fact>]
let ``copy refuses to overwrite unless allowed`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\notes.txt", MockFileData "new")
    fs.AddFile(@"C:\backup\notes.txt", MockFileData "old")

    let errorResult = copy fs false (fp @"C:\backup") (fp @"C:\data\notes.txt") |> expectError

    Assert.Equal(
        TargetExists(fp @"C:\backup\notes.txt"),
        errorResult)

    Assert.Equal("old", fs.File.ReadAllText @"C:\backup\notes.txt")

[<Fact>]
let ``copy overwrites when allowed`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\notes.txt", MockFileData "new")
    fs.AddFile(@"C:\backup\notes.txt", MockFileData "old")

    copy fs true (fp @"C:\backup") (fp @"C:\data\notes.txt") |> expectOk

    Assert.Equal("new", fs.File.ReadAllText @"C:\backup\notes.txt")

[<Fact>]
let ``copy duplicates a whole directory tree`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\docs\deep\inner.txt", MockFileData "x")
    fs.AddFile(@"C:\data\docs\top.txt", MockFileData "y")
    fs.AddDirectory @"C:\backup"

    copy fs false (fp @"C:\backup") (fp @"C:\data\docs") |> expectOk

    Assert.Equal("x", fs.File.ReadAllText @"C:\backup\docs\deep\inner.txt")
    Assert.Equal("y", fs.File.ReadAllText @"C:\backup\docs\top.txt")

[<Fact>]
let ``copy reports a missing source`` () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\backup"

    let errorResult = copy fs false (fp @"C:\backup") (fp @"C:\data\gone.txt") |> expectError

    Assert.Equal(
        SourceNotFound(fp @"C:\data\gone.txt"),
        errorResult)

[<Fact>]
let ``move takes a file to the target directory`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\notes.txt", MockFileData "hello")
    fs.AddDirectory @"C:\backup"

    move fs false (fp @"C:\backup") (fp @"C:\data\notes.txt") |> expectOk

    Assert.Equal("hello", fs.File.ReadAllText @"C:\backup\notes.txt")
    Assert.False(fs.File.Exists @"C:\data\notes.txt")

[<Fact>]
let ``move takes a directory tree to the target directory`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\docs\inner.txt", MockFileData "x")
    fs.AddDirectory @"C:\backup"

    move fs false (fp @"C:\backup") (fp @"C:\data\docs") |> expectOk

    Assert.Equal("x", fs.File.ReadAllText @"C:\backup\docs\inner.txt")
    Assert.False(fs.Directory.Exists @"C:\data\docs")

[<Fact>]
let ``move refuses to overwrite unless allowed`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\notes.txt", MockFileData "new")
    fs.AddFile(@"C:\backup\notes.txt", MockFileData "old")

    let errorResult = move fs false (fp @"C:\backup") (fp @"C:\data\notes.txt") |> expectError

    Assert.Equal(
        TargetExists(fp @"C:\backup\notes.txt"),
        errorResult)

    Assert.Equal("old", fs.File.ReadAllText @"C:\backup\notes.txt")
    Assert.True(fs.File.Exists @"C:\data\notes.txt")

[<Fact>]
let ``rename keeps the entry in its own directory`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\notes.txt", MockFileData "hello")

    rename fs (fp @"C:\data\notes.txt") "readme.txt" |> expectOk

    Assert.Equal("hello", fs.File.ReadAllText @"C:\data\readme.txt")
    Assert.False(fs.File.Exists @"C:\data\notes.txt")

[<Fact>]
let ``rename refuses an existing name`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\notes.txt", MockFileData "hello")
    fs.AddFile(@"C:\data\readme.txt", MockFileData "other")

    let errorResult = rename fs (fp @"C:\data\notes.txt") "readme.txt" |> expectError

    Assert.Equal(
        TargetExists(fp @"C:\data\readme.txt"),
        errorResult)

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

[<Fact>]
let ``copyAll and moveAll report every item`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\a.txt", MockFileData "a")
    fs.AddFile(@"C:\data\b.txt", MockFileData "b")
    fs.AddDirectory @"C:\backup"

    let copied = copyAll fs false [ fp @"C:\data\a.txt"; fp @"C:\data\b.txt" ] (fp @"C:\backup")
    let moved = moveAll fs false [ fp @"C:\data\a.txt" ] (fp @"C:\backup")

    Assert.Equal<string list>([ "a.txt"; "b.txt" ], copied |> List.map _.Name)
    Assert.Equal(Error(TargetExists(fp @"C:\backup\a.txt")), (List.exactlyOne moved).Outcome)

[<Fact>]
let ``a directory holding entries is non-empty`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\docs\inner.txt", MockFileData "x")

    Assert.True(isNonEmptyDirectory fs (fp @"C:\data\docs"))

[<Fact>]
let ``an empty directory and a file are not non-empty directories`` () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\data\empty"
    fs.AddFile(@"C:\data\notes.txt", MockFileData "x")

    Assert.False(isNonEmptyDirectory fs (fp @"C:\data\empty"))
    Assert.False(isNonEmptyDirectory fs (fp @"C:\data\notes.txt"))

[<Fact>]
let ``failures keeps only the items that went wrong`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\b.txt", MockFileData "b")

    let failed = deleteAll fs [ fp @"C:\data\gone.txt"; fp @"C:\data\b.txt" ] |> failures

    Assert.Equal<string list>([ "gone.txt" ], failed |> List.map _.Name)

[<Fact>]
let ``access denied maps to an error value`` () =
    Assert.Equal(AccessDenied(fp @"C:\locked"), mapException (fp @"C:\locked") (UnauthorizedAccessException()))

[<Fact>]
let ``unexpected failures map to an error value`` () =
    Assert.Equal(OperationFailed(fp @"C:\bad", "boom"), mapException (fp @"C:\bad") (IO.IOException "boom"))
