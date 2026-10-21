module Marbas.Tests.Guide.ChapterNameTests

open Xunit
open Marbas.Guide

[<Theory>]
[<InlineData("README.md")>]
[<InlineData("05-file-operations.md")>]
let ``a Markdown file beside the chapter is a chapter`` (raw: string) =
    let name = ChapterName.tryCreate raw |> Option.map ChapterName.value

    Assert.Equal(Some raw, name)

[<Theory>]
[<InlineData("")>]
[<InlineData(".md")>]
[<InlineData("notes.txt")>]
[<InlineData("marking.MD")>]
[<InlineData("../README.md")>]
[<InlineData("guide/03-marking.md")>]
[<InlineData("guide\\03-marking.md")>]
[<InlineData("C:03-marking.md")>]
let ``anything else is no chapter`` (raw: string) =
    let name = ChapterName.tryCreate raw

    Assert.Equal(None, name)
