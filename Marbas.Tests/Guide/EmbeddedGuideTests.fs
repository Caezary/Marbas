module Marbas.Tests.Guide.EmbeddedGuideTests

open System.IO
open Xunit
open Marbas.Guide
open Marbas.Tests

let private directory =
    Path.Combine(__SOURCE_DIRECTORY__, "..", "..", "docs", "user-guide")

let private chapter name =
    ChapterName.tryCreate name |> Expect.some

[<Fact>]
let ``the embedded chapters are the files of the guide's directory`` () =
    let files =
        Directory.GetFiles(directory, "*.md")
        |> Array.map Path.GetFileName
        |> Array.sort
        |> List.ofArray

    let embedded = EmbeddedGuide.names () |> List.map ChapterName.value

    Assert.Equal<string list>(files, embedded)

[<Fact>]
let ``a chapter is read by its name`` () =
    let name = chapter "02-panels.md"

    let read = EmbeddedGuide.read name |> Expect.ok

    Assert.Equal(name, read.Name)

[<Fact>]
let ``a chapter read is parsed`` () =
    let name = chapter "02-panels.md"

    let read = EmbeddedGuide.read name |> Expect.ok

    Assert.NotEmpty read.Contents

[<Fact>]
let ``a name no file has is absent`` () =
    let name = chapter "99-nowhere.md"

    let read = EmbeddedGuide.read name

    Assert.Equal(Error(Absent name), read)

[<Fact>]
let ``the contents are the guide's README`` () =
    let contents = ChapterName.contents

    let read = EmbeddedGuide.read contents |> Expect.ok

    Assert.Equal("README.md", ChapterName.value read.Name)
