module Marbas.Tests.FileSystem.OperationsTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private en = EntryName.create

let private sources (results: ItemResult list) = results |> List.map _.Source

let private failedPath error =
    match error with
    | OperationFailed(path, _) -> Some path
    | _ -> None

[<Fact>]
let ``makeDirectory creates a directory in the given parent`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "data")

    makeDirectory fs (fp (at "data")) (en "new") |> Expect.ok

    Assert.True(fs.Directory.Exists(at "data/new"))

[<Fact>]
let ``makeDirectory refuses an existing name`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "data/new")

    let failure = makeDirectory fs (fp (at "data")) (en "new") |> Expect.error

    Assert.Equal(TargetExists(fp (at "data/new")), failure)

[<Fact>]
let ``makeFile creates an empty file in the given parent`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "data")

    let path = makeFile fs (fp (at "data")) (en "notes.txt") |> Expect.ok

    Assert.Equal(at "data/notes.txt", FileSystemPath.value path)
    Assert.Equal("", fs.File.ReadAllText(at "data/notes.txt"))

[<Fact>]
let ``makeFile leaves an existing file as it stands`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/notes.txt", MockFileData "hello")

    let path = makeFile fs (fp (at "data")) (en "notes.txt") |> Expect.ok

    Assert.Equal(at "data/notes.txt", FileSystemPath.value path)
    Assert.Equal("hello", fs.File.ReadAllText(at "data/notes.txt"))

[<Fact>]
let ``makeFile refuses a name a directory already carries`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "data/notes.txt")

    let failure = makeFile fs (fp (at "data")) (en "notes.txt") |> Expect.error

    Assert.Equal(TargetExists(fp (at "data/notes.txt")), failure)

[<Fact>]
let ``delete removes a file`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/notes.txt", MockFileData "hello")

    delete fs (fp (at "data/notes.txt")) |> Expect.ok

    Assert.False(fs.File.Exists(at "data/notes.txt"))

[<Fact>]
let ``delete removes a directory with its contents`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/docs/deep/inner.txt", MockFileData "x")

    delete fs (fp (at "data/docs")) |> Expect.ok

    Assert.False(fs.Directory.Exists(at "data/docs"))

[<Fact>]
let ``delete reports a missing source`` () =
    let fs = MockFileSystem()

    let failure = delete fs (fp (at "data/gone.txt")) |> Expect.error

    Assert.Equal(SourceNotFound(fp (at "data/gone.txt")), failure)

[<Fact>]
let ``delete reports a file the file system will not let go`` () =
    let fs = MockFileSystem()
    let guarded = MockFileData "hello"
    guarded.Attributes <- IO.FileAttributes.ReadOnly
    fs.AddFile(at "data/notes.txt", guarded)

    let failure = delete fs (fp (at "data/notes.txt")) |> Expect.error

    Assert.Equal(AccessDenied(fp (at "data/notes.txt")), failure)
    Assert.True(fs.File.Exists(at "data/notes.txt"))

[<Fact>]
let ``delete reports a file that is in use as a failure of its own`` () =
    let fs = MockFileSystem()
    let held = MockFileData "hello"
    held.AllowedFileShare <- IO.FileShare.None
    fs.AddFile(at "data/notes.txt", held)

    let failure = delete fs (fp (at "data/notes.txt")) |> Expect.error

    Assert.Equal(Some(fp (at "data/notes.txt")), failedPath failure)

[<Fact>]
let ``copy places a file in the target directory`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/notes.txt", MockFileData "hello")
    fs.AddDirectory(at "backup")

    copy fs unattended (fp (at "backup")) (fp (at "data/notes.txt")) |> Expect.ok

    Assert.Equal("hello", fs.File.ReadAllText(at "backup/notes.txt"))
    Assert.True(fs.File.Exists(at "data/notes.txt"))

[<Fact>]
let ``copy replaces whatever is already there`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/notes.txt", MockFileData "new")
    fs.AddFile(at "backup/notes.txt", MockFileData "old")

    copy fs unattended (fp (at "backup")) (fp (at "data/notes.txt")) |> Expect.ok

    Assert.Equal("new", fs.File.ReadAllText(at "backup/notes.txt"))

[<Fact>]
let ``copy duplicates a whole directory tree`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/docs/deep/inner.txt", MockFileData "x")
    fs.AddFile(at "data/docs/top.txt", MockFileData "y")
    fs.AddDirectory(at "backup")

    copy fs unattended (fp (at "backup")) (fp (at "data/docs")) |> Expect.ok

    Assert.Equal("x", fs.File.ReadAllText(at "backup/docs/deep/inner.txt"))
    Assert.Equal("y", fs.File.ReadAllText(at "backup/docs/top.txt"))

[<Fact>]
let ``copy reports a missing source`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "backup")

    let failure = copy fs unattended (fp (at "backup")) (fp (at "data/gone.txt")) |> Expect.error

    Assert.Equal(SourceNotFound(fp (at "data/gone.txt")), failure)

[<Fact>]
let ``move takes a file to the target directory`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/notes.txt", MockFileData "hello")
    fs.AddDirectory(at "backup")

    move fs unattended (fp (at "backup")) (fp (at "data/notes.txt")) |> Expect.ok

    Assert.Equal("hello", fs.File.ReadAllText(at "backup/notes.txt"))
    Assert.False(fs.File.Exists(at "data/notes.txt"))

[<Fact>]
let ``move takes a directory tree to the target directory`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/docs/inner.txt", MockFileData "x")
    fs.AddDirectory(at "backup")

    move fs unattended (fp (at "backup")) (fp (at "data/docs")) |> Expect.ok

    Assert.Equal("x", fs.File.ReadAllText(at "backup/docs/inner.txt"))
    Assert.False(fs.Directory.Exists(at "data/docs"))

[<Fact>]
let ``copying a directory copies every file under it`` () =
    let fs = Fakes.CopyCounting()
    fs.AddFile(at "data/docs/inner.txt", MockFileData "x")
    fs.AddFile(at "data/docs/deep/leaf.txt", MockFileData "y")
    fs.AddDirectory(at "backup")

    let copied = copy fs unattended (fp (at "backup")) (fp (at "data/docs"))

    Expect.ok copied
    Assert.Equal(2, fs.Copies)

[<Fact>]
let ``moving a directory where the file system can rename it copies nothing`` () =
    let fs = Fakes.CopyCounting()
    fs.AddFile(at "data/docs/inner.txt", MockFileData "x")
    fs.AddDirectory(at "backup")

    let moved = move fs unattended (fp (at "backup")) (fp (at "data/docs"))

    Expect.ok moved
    Assert.Equal(0, fs.Copies)
    Assert.Equal("x", fs.File.ReadAllText(at "backup/docs/inner.txt"))
    Assert.False(fs.Directory.Exists(at "data/docs"))

[<Fact>]
let ``moving a directory onto one that already stands merges into it`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/docs/inner.txt", MockFileData "x")
    fs.AddFile(at "backup/docs/other.txt", MockFileData "y")

    let moved = move fs unattended (fp (at "backup")) (fp (at "data/docs"))

    Expect.ok moved
    Assert.Equal("x", fs.File.ReadAllText(at "backup/docs/inner.txt"))
    Assert.Equal("y", fs.File.ReadAllText(at "backup/docs/other.txt"))
    Assert.False(fs.Directory.Exists(at "data/docs"))

[<Fact>]
let ``a directory the file system refuses to rename is copied and then removed`` () =
    let fs = Fakes.DirectoryMoveRefused()
    fs.AddFile(at "data/docs/inner.txt", MockFileData "x")
    fs.AddDirectory(at "backup")

    let moved = move fs unattended (fp (at "backup")) (fp (at "data/docs"))

    Expect.ok moved
    Assert.Equal("x", fs.File.ReadAllText(at "backup/docs/inner.txt"))
    Assert.False(fs.Directory.Exists(at "data/docs"))

[<Fact>]
let ``move replaces whatever is already there`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/notes.txt", MockFileData "new")
    fs.AddFile(at "backup/notes.txt", MockFileData "old")

    move fs unattended (fp (at "backup")) (fp (at "data/notes.txt")) |> Expect.ok

    Assert.Equal("new", fs.File.ReadAllText(at "backup/notes.txt"))
    Assert.False(fs.File.Exists(at "data/notes.txt"))

[<Fact>]
let ``rename keeps the entry in its own directory`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/notes.txt", MockFileData "hello")

    rename fs (fp (at "data/notes.txt")) (en "readme.txt") |> Expect.ok

    Assert.Equal("hello", fs.File.ReadAllText(at "data/readme.txt"))
    Assert.False(fs.File.Exists(at "data/notes.txt"))

[<Fact>]
let ``rename refuses an existing name`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/notes.txt", MockFileData "hello")
    fs.AddFile(at "data/readme.txt", MockFileData "other")

    let failure = rename fs (fp (at "data/notes.txt")) (en "readme.txt") |> Expect.error

    Assert.Equal(TargetExists(fp (at "data/readme.txt")), failure)

[<Fact>]
let ``batch results carry the source and keep the input order`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/a.txt", MockFileData "a")
    fs.AddFile(at "data/b.txt", MockFileData "b")

    let results = deleteAll fs [ fp (at "data/a.txt"); fp (at "data/b.txt") ]

    Assert.Equal<ItemSource list>(
        [ ItemOnDisk(fp (at "data/a.txt")); ItemOnDisk(fp (at "data/b.txt")) ],
        sources results
    )
    Assert.All(results, fun r -> Assert.Equal(Ok(), r.Outcome))

[<Fact>]
let ``a failing item does not stop the rest of the batch`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/b.txt", MockFileData "b")

    let results = deleteAll fs [ fp (at "data/gone.txt"); fp (at "data/b.txt") ]

    Assert.Equal(Error(SourceNotFound(fp (at "data/gone.txt"))), results[0].Outcome)
    Assert.Equal(Ok(), results[1].Outcome)
    Assert.False(fs.File.Exists(at "data/b.txt"))

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
    fs.AddFile(at "data/a.txt", MockFileData "new a")
    fs.AddFile(at "data/b.txt", MockFileData "new b")
    fs

let private bothSources = [ fp (at "data/a.txt"); fp (at "data/b.txt") ]

let private watching cancelAfter =
    let reported = ResizeArray<FileSystemPath>()

    let control =
        { Report = reported.Add
          IsCancelled = fun () -> reported.Count >= cancelAfter }

    reported, control

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

    let approved = approve fs ask (bothSources @ [ fp (at "data/c.txt") ]) (fp (at "backup"))

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
let ``a directory holding entries is non-empty`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/docs/inner.txt", MockFileData "x")

    let nonEmpty = isNonEmptyDirectory fs (fp (at "data/docs"))

    Assert.True nonEmpty

[<Fact>]
let ``an empty directory and a file are not non-empty directories`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "data/empty")
    fs.AddFile(at "data/notes.txt", MockFileData "x")

    let emptyDirectory = isNonEmptyDirectory fs (fp (at "data/empty"))
    let file = isNonEmptyDirectory fs (fp (at "data/notes.txt"))

    Assert.False emptyDirectory
    Assert.False file

[<Fact>]
let ``a directory link is not a non-empty directory`` () =
    let fs = Fakes.LinkFollowing()
    fs.AddFile(at "data/docs/inner.txt", MockFileData "x")
    fs.Directory.CreateSymbolicLink(at "data/twin", at "data/docs") |> ignore

    let nonEmpty = isNonEmptyDirectory fs (fp (at "data/twin"))

    Assert.False nonEmpty

let private linked () =
    let fs = Fakes.LinkFollowing()
    fs.AddFile(at "data/docs/inner.txt", MockFileData "inner")
    fs.Directory.CreateSymbolicLink(at "data/twin", at "data/docs") |> ignore
    fs.AddDirectory(at "backup")
    fs

let private linkTargetOf (fs: IO.Abstractions.IFileSystem) path =
    fs.DirectoryInfo.New(path).LinkTarget

[<Fact>]
let ``copy takes a directory link as a link rather than the tree behind it`` () =
    let fs = linked ()

    copy fs unattended (fp (at "backup")) (fp (at "data/twin")) |> Expect.ok

    Assert.Equal(at "data/docs", linkTargetOf fs (at "backup/twin"))

[<Fact>]
let ``copy of a tree holding a link to its own ancestor terminates`` () =
    let fs = linked ()
    fs.Directory.CreateSymbolicLink(at "data/docs/loop", at "data") |> ignore

    copy fs unattended (fp (at "backup")) (fp (at "data")) |> Expect.ok

    Assert.Equal("inner", fs.File.ReadAllText(at "backup/data/docs/inner.txt"))
    Assert.Equal(at "data", linkTargetOf fs (at "backup/data/docs/loop"))

[<Fact>]
let ``move takes a directory link as a link and leaves what it pointed at`` () =
    let fs = linked ()

    move fs unattended (fp (at "backup")) (fp (at "data/twin")) |> Expect.ok

    Assert.Equal(at "data/docs", linkTargetOf fs (at "backup/twin"))
    Assert.Null(linkTargetOf fs (at "data/twin"))
    Assert.Equal("inner", fs.File.ReadAllText(at "data/docs/inner.txt"))

[<Fact>]
let ``delete of a directory link leaves what it points at`` () =
    let fs = linked ()

    delete fs (fp (at "data/twin")) |> Expect.ok

    Assert.False(fs.Directory.Exists(at "data/twin"))
    Assert.Equal("inner", fs.File.ReadAllText(at "data/docs/inner.txt"))

[<Fact>]
let ``delete of a tree holding a link leaves what the link points at`` () =
    let fs = linked ()
    fs.Directory.CreateSymbolicLink(at "backup/twin", at "data/docs") |> ignore

    delete fs (fp (at "backup")) |> Expect.ok

    Assert.False(fs.Directory.Exists(at "backup"))
    Assert.Equal("inner", fs.File.ReadAllText(at "data/docs/inner.txt"))

[<Fact>]
let ``failures keeps only the items that went wrong`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/b.txt", MockFileData "b")

    let failed = deleteAll fs [ fp (at "data/gone.txt"); fp (at "data/b.txt") ] |> failures

    Assert.Equal<ItemSource list>([ ItemOnDisk(fp (at "data/gone.txt")) ], sources failed)

[<Fact>]
let ``access denied maps to an error value`` () =
    let denied = UnauthorizedAccessException()

    let operationError = mapException (fp (at "locked")) denied

    Assert.Equal(AccessDenied(fp (at "locked")), operationError)

[<Fact>]
let ``unexpected failures map to an error value`` () =
    let failure = IO.IOException "boom"

    let operationError = mapException (fp (at "bad")) failure

    Assert.Equal(OperationFailed(fp (at "bad"), "boom"), operationError)

[<Fact>]
let ``a name Windows has no application for maps to an error value`` () =
    let refusal = ComponentModel.Win32Exception 1155

    let operationError = mapException (fp (at "notes.odd")) refusal

    Assert.Equal(NoProgramAssociated(fp (at "notes.odd")), operationError)

[<Fact>]
let ``an elevation prompt the user turned down maps to an error value`` () =
    let refusal = ComponentModel.Win32Exception 1223

    let operationError = mapException (fp (at "tool.exe")) refusal

    Assert.Equal(StartDeclined(fp (at "tool.exe")), operationError)

[<Fact>]
let ``a symbolic link refused for want of a privilege maps to an error value`` () =
    let refusal = IO.IOException("A required privilege is not held by the client.", Fakes.PrivilegeNotHeld)

    let operationError = mapException (fp (at "data/twin")) refusal

    Assert.Equal(SymbolicLinkNotPermitted(fp (at "data/twin")), operationError)

[<Fact>]
let ``copying a directory link the system will not let this user make says so`` () =
    let fs = Fakes.SymbolicLinksRefused(at "backup")
    fs.AddFile(at "data/docs/inner.txt", MockFileData "inner")
    fs.Directory.CreateSymbolicLink(at "data/twin", at "data/docs") |> ignore
    fs.AddDirectory(at "backup")

    let failure = copy fs unattended (fp (at "backup")) (fp (at "data/twin")) |> Expect.error

    Assert.Equal(SymbolicLinkNotPermitted(fp (at "data/twin")), failure)

[<Fact>]
let ``a Windows failure of any other code keeps the words the system gave`` () =
    let refusal = ComponentModel.Win32Exception 2

    let operationError = mapException (fp (at "gone.exe")) refusal

    Assert.Equal(OperationFailed(fp (at "gone.exe"), refusal.Message), operationError)

[<Fact>]
let ``copy reports a directory it may not read`` () =
    let fs = Fakes.UnreadableDirectory(at "data/docs")
    fs.AddFile(at "data/docs/inner.txt", MockFileData "inner")
    fs.AddDirectory(at "backup")

    let failure = copy fs unattended (fp (at "backup")) (fp (at "data/docs")) |> Expect.error

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
    let work _ : ItemResult list = raise (InvalidOperationException "no writer for tar")

    let results = guarded "Copying" work unattended

    let failure = List.exactlyOne results
    Assert.Equal<ItemSource>(WholeBatch "Copying", failure.Source)
    Assert.Equal(BatchFailed "no writer for tar", Expect.error failure.Outcome)

let private watched () =
    let fs = Fakes.WalkWitness()
    fs.AddDirectory(at "out")
    fs.AddFile(at "work/notes.txt", MockFileData "notes")
    fs.AddFile(at "work/docs/inner.txt", MockFileData "inner")
    fs.AddFile(at "work/docs/deep/leaf.txt", MockFileData "leaf")
    fs

[<Fact>]
let ``deleting a tree enumerates each directory once`` () =
    let fs = watched ()

    delete fs (fp (at "work")) |> Expect.ok

    Assert.Equal(3, fs.Enumerations)

[<Fact>]
let ``deleting a tree asks nothing about an entry it enumerated`` () =
    let fs = watched ()

    delete fs (fp (at "work")) |> Expect.ok

    Assert.Equal(0, fs.AskedAbout(at "work/docs/inner.txt"))

[<Fact>]
let ``copying a tree enumerates each directory once`` () =
    let fs = watched ()

    copy fs unattended (fp (at "out")) (fp (at "work")) |> Expect.ok

    Assert.Equal(3, fs.Enumerations)

[<Fact>]
let ``copying a tree asks nothing about an entry it enumerated`` () =
    let fs = watched ()

    copy fs unattended (fp (at "out")) (fp (at "work")) |> Expect.ok

    Assert.Equal(0, fs.AskedAbout(at "work/docs/inner.txt"))
