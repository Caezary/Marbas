module Marbas.Tests.Ui.Panels.EntryColorTests

open System
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.Ui.Panels.EntryColor

let private modified = DateTimeOffset(DateTime(2026, 7, 1, 12, 30, 0))

let private file name executability =
    { Name = EntryName.create name
      Kind = File
      Size = Some 1L
      Modified = modified
      Visibility = Plain
      Executability = executability }

let private directory name =
    { Name = EntryName.create name
      Kind = Directory
      Size = None
      Modified = modified
      Visibility = Plain
      Executability = NotExecutable }

[<Theory>]
[<InlineData("docs")>]
[<InlineData("..")>]
let ``directories and the parent entry share one colour`` name =
    let color = classify (directory name)

    Assert.Equal(DirectoryColor, color)

[<Theory>]
[<InlineData("sources.zip")>]
[<InlineData("sources.ZIP")>]
[<InlineData("sources.7z")>]
[<InlineData("sources.tar")>]
[<InlineData("sources.tar.gz")>]
[<InlineData("sources.tgz")>]
[<InlineData("Marbas.1.0.0.nupkg")>]
[<InlineData("disk.iso")>]
let ``archives have a colour of their own`` name =
    let color = classify (file name NotExecutable)

    Assert.Equal(ArchiveColor, color)

[<Fact>]
let ``an executable file has a colour of its own`` () =
    let color = classify (file "setup.exe" Executable)

    Assert.Equal(ExecutableColor, color)

[<Fact>]
let ``an ordinary file has the plain colour`` () =
    let color = classify (file "readme.txt" NotExecutable)

    Assert.Equal(PlainColor, color)

[<Fact>]
let ``an archive stays an archive even when it is marked executable`` () =
    let color = classify (file "sources.zip" Executable)

    Assert.Equal(ArchiveColor, color)

[<Fact>]
let ``every colour is a distinct attribute of the classic palette`` () =
    let attributes =
        [ DirectoryColor; PlainColor; ArchiveColor; ExecutableColor ] |> List.map attribute

    Assert.Equal(4, attributes |> List.distinct |> List.length)
