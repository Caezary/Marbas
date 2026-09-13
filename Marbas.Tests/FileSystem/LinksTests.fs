module Marbas.Tests.FileSystem.LinksTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private data () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/docs/inner.txt", MockFileData "inner")
    fs.AddFile(at "data/notes.txt", MockFileData "notes")
    fs

let private requested kind target location =
    { Kind = kind
      Target = fp target
      Location = fp location }

let private symbolic = requested Symbolic

type private Native() =
    let made = ResizeArray<LinkKind * FileSystemPath * FileSystemPath>()

    member _.Made = List.ofSeq made

    member _.Links: NativeLinks =
        { MakeHard = fun target location -> made.Add((Hard, target, location))
          MakeJunction = fun target location -> made.Add((Junction, target, location)) }

let private refusing (ex: exn) : NativeLinks =
    { MakeHard = fun _ _ -> raise ex
      MakeJunction = fun _ _ -> raise ex }

let private noNative = Native().Links

[<Theory>]
[<InlineData("data/docs", "data/twin")>]
[<InlineData("data/notes.txt", "data/twin.txt")>]
let ``a symbolic link is made and resolves to what it points at`` target location =
    let fs = data ()

    Links.create noNative Windows fs (symbolic (at target) (at location)) |> Expect.ok

    Assert.Equal(Some(fp (at target)), FileSystemPath.linkTarget fs (fp (at location)))

[<Fact>]
let ``a symbolic link to a directory is a directory`` () =
    let fs = data ()

    Links.create noNative Unix fs (symbolic (at "data/docs") (at "data/twin")) |> Expect.ok

    Assert.Equal(DirectoryEntry, FileSystemPath.kind fs (fp (at "data/twin")))

[<Fact>]
let ``a target given relative is written absolute, from where the link stands`` () =
    let fs = data ()

    Links.create noNative Windows fs (symbolic "docs" (at "data/twin")) |> Expect.ok

    Assert.Equal(Some(fp (at "data/docs")), FileSystemPath.linkTarget fs (fp (at "data/twin")))

[<Fact>]
let ``a link is not made over an entry that is already there`` () =
    let fs = data ()

    let failure =
        Links.create noNative Windows fs (symbolic (at "data/docs") (at "data/notes.txt"))
        |> Expect.error

    Assert.Equal(TargetExists(fp (at "data/notes.txt")), failure)

[<Fact>]
let ``a link is not made to nothing`` () =
    let fs = data ()

    let failure =
        Links.create noNative Windows fs (symbolic (at "data/gone") (at "data/twin"))
        |> Expect.error

    Assert.Equal(SourceNotFound(fp (at "data/gone")), failure)

[<Theory>]
[<InlineData("data/docs", "data/twin")>]
[<InlineData("data/notes.txt", "data/twin.txt")>]
let ``a symbolic link the system refuses for want of the right says so`` target location =
    let fs = Fakes.SymbolicLinksRefused(at "data")
    fs.AddFile(at "data/docs/inner.txt", MockFileData "inner")
    fs.AddFile(at "data/notes.txt", MockFileData "notes")

    let failure =
        Links.create noNative Windows fs (symbolic (at target) (at location))
        |> Expect.error

    Assert.Equal(SymbolicLinkNotPermitted(fp (at location)), failure)

[<Fact>]
let ``a hard link to a file on the same volume is left to the system with both paths absolute`` () =
    let fs = data ()
    let native = Native()

    Links.create native.Links Unix fs (requested Hard "notes.txt" (at "data/twin.txt"))
    |> Expect.ok

    Assert.Equal<(LinkKind * FileSystemPath * FileSystemPath) list>(
        [ Hard, fp (at "data/notes.txt"), fp (at "data/twin.txt") ],
        native.Made
    )

[<Fact>]
let ``a junction to a directory on Windows is left to the system`` () =
    let fs = data ()
    let native = Native()

    Links.create native.Links Windows fs (requested Junction (at "data/docs") (at "data/twin"))
    |> Expect.ok

    Assert.Equal<(LinkKind * FileSystemPath * FileSystemPath) list>(
        [ Junction, fp (at "data/docs"), fp (at "data/twin") ],
        native.Made
    )

[<Fact>]
let ``a hard link to a directory is refused without asking the system`` () =
    let fs = data ()
    let native = Native()

    let failure =
        Links.create native.Links Windows fs (requested Hard (at "data/docs") (at "data/twin"))
        |> Expect.error

    Assert.Equal(LinkRefused(HardLinkToDirectory, fp (at "data/twin")), failure)
    Assert.Empty native.Made

[<WindowsFact>]
let ``a hard link to another drive is refused without asking the system`` () =
    let fs = data ()
    fs.AddDirectory(@"D:\backup")
    let native = Native()

    let failure =
        Links.create native.Links Windows fs (requested Hard (at "data/notes.txt") @"D:\backup\twin.txt")
        |> Expect.error

    Assert.Equal(LinkRefused(HardLinkAcrossVolumes, fp @"D:\backup\twin.txt"), failure)
    Assert.Empty native.Made

[<WindowsFact>]
let ``a drive letter is the same volume whatever its case`` () =
    let fs = data ()
    let native = Native()

    Links.create native.Links Windows fs (requested Hard (at "data/notes.txt") @"c:\data\twin.txt")
    |> Expect.ok

    Assert.Single native.Made |> ignore

[<Fact>]
let ``a junction to a file is refused without asking the system`` () =
    let fs = data ()
    let native = Native()

    let failure =
        Links.create native.Links Windows fs (requested Junction (at "data/notes.txt") (at "data/twin"))
        |> Expect.error

    Assert.Equal(LinkRefused(JunctionToFile, fp (at "data/twin")), failure)
    Assert.Empty native.Made

[<Fact>]
let ``a junction off Windows is refused without asking the system`` () =
    let fs = data ()
    let native = Native()

    let failure =
        Links.create native.Links Unix fs (requested Junction (at "data/docs") (at "data/twin"))
        |> Expect.error

    Assert.Equal(LinkRefused(JunctionOffWindows, fp (at "data/twin")), failure)
    Assert.Empty native.Made

[<Fact>]
let ``a native link the system refuses is mapped the way every operation maps it`` () =
    let fs = data ()
    let native = refusing (UnauthorizedAccessException "denied")

    let failure =
        Links.create native Windows fs (requested Hard (at "data/notes.txt") (at "data/twin.txt"))
        |> Expect.error

    Assert.Equal(AccessDenied(fp (at "data/twin.txt")), failure)

[<Fact>]
let ``a symbolic link carries no refusal of its kind`` () =
    let fs = data ()

    let refusal = Links.refusal Unix fs (symbolic (at "data/notes.txt") (at "data/twin.txt"))

    Assert.Equal(None, refusal)

[<Fact>]
let ``the refusal of a kind can be asked before anything is made`` () =
    let fs = data ()

    let refusal = Links.refusal Unix fs (requested Junction (at "data/docs") (at "data/twin"))

    Assert.Equal(Some JunctionOffWindows, refusal)

let private sources = [ fp (at "data/notes.txt"); fp (at "data/docs") ]

[<Fact>]
let ``one entry is linked at the path that was typed`` () =
    let fs = data ()

    let requests = Links.requests fs Hard [ fp (at "data/notes.txt") ] (fp (at "data")) (at "backup/twin.txt")

    Assert.Equal<LinkRequest list>([ requested Hard (at "data/notes.txt") (at "backup/twin.txt") ], requests)

[<Fact>]
let ``one entry given a bare name is linked beside itself`` () =
    let fs = data ()

    let requests = Links.requests fs Symbolic [ fp (at "data/notes.txt") ] (fp (at "data")) " twin.txt "

    Assert.Equal<LinkRequest list>([ symbolic (at "data/notes.txt") (at "data/twin.txt") ], requests)

[<Fact>]
let ``one entry given a directory that exists is linked inside it under its own name`` () =
    let fs = data ()
    fs.AddDirectory(at "backup")

    let requests = Links.requests fs Symbolic [ fp (at "data/notes.txt") ] (fp (at "data")) (at "backup")

    Assert.Equal<LinkRequest list>([ symbolic (at "data/notes.txt") (at "backup/notes.txt") ], requests)

[<Fact>]
let ``several entries are linked one each inside the directory that was typed`` () =
    let fs = data ()

    let requests = Links.requests fs Symbolic sources (fp (at "data")) (at "backup")

    Assert.Equal<LinkRequest list>(
        [ symbolic (at "data/notes.txt") (at "backup/notes.txt")
          symbolic (at "data/docs") (at "backup/docs") ],
        requests
    )

[<Fact>]
let ``nothing typed asks for no link`` () =
    let fs = data ()

    let requests = Links.requests fs Symbolic sources (fp (at "data")) "  "

    Assert.Empty requests

[<Fact>]
let ``a kind is refused for several entries only when every one of them refuses it`` () =
    let fs = data ()
    let requests = Links.requests fs Hard sources (fp (at "data")) (at "backup")

    let refusal = Links.sharedRefusal (Links.refusal Windows fs) requests

    Assert.Equal(None, refusal)

[<Fact>]
let ``a kind every entry refuses is refused with the first reason`` () =
    let fs = data ()
    let requests = Links.requests fs Junction sources (fp (at "data")) (at "backup")

    let refusal = Links.sharedRefusal (Links.refusal Unix fs) requests

    Assert.Equal(Some JunctionOffWindows, refusal)

[<Fact>]
let ``no request carries no refusal`` () =
    let refusal = Links.sharedRefusal (fun _ -> Some JunctionOffWindows) []

    Assert.Equal(None, refusal)
