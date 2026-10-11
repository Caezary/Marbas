module Marbas.Tests.FileSystem.OperationsApprovalTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Tests
open Marbas.Tests.FileSystem.OperationsFixture

let private at = Root.at

let private fp = FileSystemPath.create

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

let private never (path: FileSystemPath) =
    failwith $"unexpected question about %A{path}"

[<Fact>]
let ``approve keeps every source and asks nothing when nothing collides`` () =
    let fs = twoSources ()
    fs.AddDirectory(at "backup")

    let approved = approve fs never bothSources (fp (at "backup"))

    Assert.Equal<FileSystemPath list>(bothSources, approved)

[<Fact>]
let ``a collision is asked about and yes keeps that source`` () =
    let fs = twoSources ()
    fs.AddFile(at "backup/a.txt", MockFileData "old a")
    let asked, ask = answering [ Yes ]

    let approved = approve fs ask bothSources (fp (at "backup"))

    Assert.Equal<FileSystemPath list>([ fp (at "backup/a.txt") ], List.ofSeq asked)
    Assert.Equal<FileSystemPath list>(bothSources, approved)

[<Fact>]
let ``no drops the colliding source and keeps the rest`` () =
    let fs = twoSources ()
    fs.AddFile(at "backup/a.txt", MockFileData "old a")
    let _, ask = answering [ No ]

    let approved = approve fs ask bothSources (fp (at "backup"))

    Assert.Equal<FileSystemPath list>([ fp (at "data/b.txt") ], approved)

[<Fact>]
let ``yes to all answers the remaining collisions too`` () =
    let fs = twoSources ()
    fs.AddFile(at "backup/a.txt", MockFileData "old a")
    fs.AddFile(at "backup/b.txt", MockFileData "old b")
    let asked, ask = answering [ YesToAll ]

    let approved = approve fs ask bothSources (fp (at "backup"))

    Assert.Single asked |> ignore
    Assert.Equal<FileSystemPath list>(bothSources, approved)

[<Fact>]
let ``no to all drops the remaining collisions without asking again`` () =
    let fs = twoSources ()
    fs.AddFile(at "backup/a.txt", MockFileData "old a")
    fs.AddFile(at "backup/b.txt", MockFileData "old b")
    let asked, ask = answering [ NoToAll ]

    let approved = approve fs ask bothSources (fp (at "backup"))

    Assert.Single asked |> ignore
    Assert.Empty approved

[<Fact>]
let ``no to all keeps the sources that do not collide`` () =
    let fs = twoSources ()
    fs.AddFile(at "data/c.txt", MockFileData "new c")
    fs.AddFile(at "backup/a.txt", MockFileData "old a")
    let asked, ask = answering [ NoToAll ]

    let approved =
        approve fs ask (bothSources @ [ fp (at "data/c.txt") ]) (fp (at "backup"))

    Assert.Single asked |> ignore
    Assert.Equal<FileSystemPath list>([ fp (at "data/b.txt"); fp (at "data/c.txt") ], approved)

[<Fact>]
let ``cancel abandons the whole batch`` () =
    let fs = twoSources ()
    fs.AddFile(at "backup/b.txt", MockFileData "old b")
    let _, ask = answering [ Cancel ]

    let approved = approve fs ask bothSources (fp (at "backup"))

    Assert.Empty approved

[<Fact>]
let ``a colliding directory is asked about once, by its own name`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/docs/a.txt", MockFileData "new a")
    fs.AddFile(at "backup/docs/a.txt", MockFileData "old a")
    let asked, ask = answering [ Yes ]

    let approved = approve fs ask [ fp (at "data/docs") ] (fp (at "backup"))

    Assert.Equal<FileSystemPath list>([ fp (at "backup/docs") ], List.ofSeq asked)
    Assert.Equal<FileSystemPath list>([ fp (at "data/docs") ], approved)
