module Marbas.Tests.FileSystem.OperationsIntoItselfTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private stoppedAfterChecks limit =
    let checks = ref 0

    { Report = ignore
      IsCancelled =
        fun () ->
            checks.Value <- checks.Value + 1
            checks.Value > limit }

[<Fact>]
let ``a directory copied into its own subdirectory meets its fresh copy and copies it again until stopped`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/docs/sub/a.txt", MockFileData "a")
    let control = stoppedAfterChecks 20

    copy fs control (fp (at "data/docs/sub")) (fp (at "data/docs")) |> ignore

    Assert.True(fs.Directory.Exists(at "data/docs/sub/docs/sub/docs/sub/docs/sub"))

[<Theory>]
[<InlineData("data/docs")>]
[<InlineData("data/docs/sub")>]
[<InlineData("data/docs/sub/deep")>]
[<InlineData("data/notes.txt")>]
let ``a target at or under one of the sources lands within them`` (target: string) =
    let fs = MockFileSystem()

    let refused =
        landsWithinSource fs (fp (at target)) [ fp (at "data/notes.txt"); fp (at "data/docs") ]

    Assert.True refused

[<Theory>]
[<InlineData("data")>]
[<InlineData("data/backup")>]
[<InlineData("data/docs-old")>]
[<InlineData("data/docs2/sub")>]
let ``a target beside or above the sources lands outside them`` (target: string) =
    let fs = MockFileSystem()

    let refused =
        landsWithinSource fs (fp (at target)) [ fp (at "data/notes.txt"); fp (at "data/docs") ]

    Assert.False refused

[<UnixFact>]
let ``a target in a differently cased sibling lands outside the source where case tells them apart`` () =
    let fs = MockFileSystem()

    let refused = landsWithinSource fs (fp (at "data/DOCS/sub")) [ fp (at "data/docs") ]

    Assert.False refused

[<WindowsFact>]
let ``a target in a differently cased sibling lands within the source where case is ignored`` () =
    let fs = MockFileSystem()

    let refused = landsWithinSource fs (fp (at "data/DOCS/sub")) [ fp (at "data/docs") ]

    Assert.True refused
