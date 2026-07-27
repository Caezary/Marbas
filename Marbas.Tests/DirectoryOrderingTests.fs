module Marbas.Tests.DirectoryOrderingTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing

let private fp = FileSystemPath.create

let private entry name kind =
    { Name = name
      Kind = kind
      Size = (match kind with File -> Some 1L | Directory -> None)
      Modified = DateTime(2026, 1, 1) }

[<Fact>]
let ``directories come before files`` () =
    let ordered = order [ entry "a.txt" File; entry "zeta" Directory ]

    Assert.Equal<string list>([ "zeta"; "a.txt" ], ordered |> List.map (fun e -> e.Name))

[<Fact>]
let ``names are sorted alphabetically ignoring case`` () =
    let ordered =
        order
            [ entry "delta.txt" File
              entry "Alpha.txt" File
              entry "zulu" Directory
              entry "Bravo" Directory ]

    Assert.Equal<string list>(
        [ "Bravo"; "zulu"; "Alpha.txt"; "delta.txt" ],
        ordered |> List.map (fun e -> e.Name)
    )

[<Fact>]
let ``non-root listing starts with the parent entry`` () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start\sub"
    fs.AddFile(@"C:\start\sub\a.txt", MockFileData "x")

    match listForDisplay fs (fp @"C:\start\sub") with
    | Ok(first :: _) ->
        Assert.Equal("..", first.Name)
        Assert.Equal(Directory, first.Kind)
    | other -> failwith $"unexpected result %A{other}"

[<Fact>]
let ``root listing has no parent entry`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\a.txt", MockFileData "x")

    match listForDisplay fs (fp @"C:\") with
    | Ok entries -> Assert.DoesNotContain(entries, fun e -> e.Name = "..")
    | Error err -> failwith $"unexpected error %A{err}"

[<Fact>]
let ``parent entry is followed by ordered content`` () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start\sub\inner"
    fs.AddFile(@"C:\start\sub\a.txt", MockFileData "x")

    match listForDisplay fs (fp @"C:\start\sub") with
    | Ok entries ->
        Assert.Equal<string list>([ ".."; "inner"; "a.txt" ], entries |> List.map (fun e -> e.Name))
    | Error err -> failwith $"unexpected error %A{err}"
