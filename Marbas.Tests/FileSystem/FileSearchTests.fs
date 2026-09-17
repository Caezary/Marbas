module Marbas.Tests.FileSystem.FileSearchTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.FileSearch
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private tree () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes.txt", MockFileData "let x = 1")
    fs.AddFile(at "start/photo.jpg", MockFileData "not text")
    fs.AddFile(at "start/docs/notes.txt", MockFileData "mutable y = 2")
    fs.AddFile(at "start/docs/deep/notes.txt", MockFileData "let z = 3")
    fs.AddFile(at "start/docs/deep/photo.jpg", MockFileData "not text")
    fs.AddDirectory(at "start/notes")
    fs

let private queryFor masks scope =
    { What = FindWhat.create (NameMasks.read masks) None FilesOnly
      Scope = scope
      Hidden = Concealed
      Links = FollowLinks
      Archives = SkipArchives }

let private searchFor raw =
    { Text = FindText.tryCreate raw |> Expect.some
      Case = IgnoringCase
      Words = Anywhere
      Sense = Containing
      Binary = Skipped }

let private recording (reported: ResizeArray<FileSystemPath>) : Operations.BatchControl =
    { Report = reported.Add
      IsCancelled = fun () -> false }

let private stoppingAfter count (reported: ResizeArray<FileSystemPath>) : Operations.BatchControl =
    { Report = reported.Add
      IsCancelled = fun () -> reported.Count >= count }

let private spelled (fs: IFileSystem) (hits: ResizeArray<FindHit>) =
    hits
    |> Seq.map (FindHit.spelled fs >> FileSystemPath.value)
    |> List.ofSeq
    |> List.sort

let private hitsOf (fs: IFileSystem) control query roots =
    let hits = ResizeArray<FindHit>()

    let outcome =
        run fs control hits.Add query
            { Roots = roots |> List.map OnDisk
              OtherRoots = [] }

    outcome, spelled fs hits

let private hitsWithin (fs: IFileSystem) query ground =
    let hits = ResizeArray<FindHit>()
    run fs Operations.unattended hits.Add query ground |> ignore
    spelled fs hits

let private found fs query roots =
    hitsOf fs Operations.unattended query roots |> snd

let private outcomeOf fs query roots =
    hitsOf fs Operations.unattended query roots |> fst

let private paths (reported: ResizeArray<FileSystemPath>) =
    reported |> Seq.map FileSystemPath.value |> List.ofSeq

[<Fact>]
let ``a search from a directory walks the tree below it`` () =
    let fs = tree ()

    let hits = found fs (queryFor "*.txt" FromDirectory) [ fp (at "start") ]

    Assert.Equal<string list>(
        [ at "start/docs/deep/notes.txt"; at "start/docs/notes.txt"; at "start/notes.txt" ],
        hits
    )

[<Fact>]
let ``a search of one directory stops at its own entries`` () =
    let fs = tree ()

    let hits = found fs (queryFor "*.txt" DirectoryOnly) [ fp (at "start") ]

    Assert.Equal<string list>([ at "start/notes.txt" ], hits)

[<Fact>]
let ``a search of marked directories walks each of them`` () =
    let fs = tree ()
    let roots = [ fp (at "start/docs/deep"); fp (at "start/notes") ]

    let hits = found fs (queryFor "*.txt" MarkedDirectories) roots

    Assert.Equal<string list>([ at "start/docs/deep/notes.txt" ], hits)

[<Fact>]
let ``a directory whose name matches is a hit when directories are wanted`` () =
    let fs = tree ()

    let query =
        { queryFor "notes*" FromDirectory with
            What = FindWhat.create (NameMasks.read "notes*") None FilesAndDirectories }

    let hits = found fs query [ fp (at "start") ]

    Assert.Contains(at "start/notes", hits)

[<Fact>]
let ``a directory is no hit when only files are wanted`` () =
    let fs = tree ()

    let hits = found fs (queryFor "notes*" FromDirectory) [ fp (at "start") ]

    Assert.DoesNotContain(at "start/notes", hits)

[<Fact>]
let ``text narrows the hits to the files that carry it`` () =
    let fs = tree ()

    let query =
        { queryFor "*.txt" FromDirectory with
            What = FindWhat.create (NameMasks.read "*.txt") (Some(searchFor "let")) FilesOnly }

    let hits = found fs query [ fp (at "start") ]

    Assert.Equal<string list>([ at "start/docs/deep/notes.txt"; at "start/notes.txt" ], hits)

[<Fact>]
let ``a hidden entry is passed over while the query conceals`` () =
    let fs = tree ()
    fs.AddFile(at "start/.hidden.txt", MockFileData "let h = 1")

    let hits = found fs (queryFor "*.txt" FromDirectory) [ fp (at "start") ]

    Assert.DoesNotContain(at "start/.hidden.txt", hits)

[<Fact>]
let ``a hidden entry is found once the query shows them`` () =
    let fs = tree ()
    fs.AddFile(at "start/.hidden.txt", MockFileData "let h = 1")

    let query = { queryFor "*.txt" FromDirectory with Hidden = Shown }

    let hits = found fs query [ fp (at "start") ]

    Assert.Contains(at "start/.hidden.txt", hits)

[<Fact>]
let ``a hidden directory is not walked into while the query conceals`` () =
    let fs = tree ()
    fs.AddFile(at "start/.cache/notes.txt", MockFileData "let c = 1")

    let hits = found fs (queryFor "*.txt" FromDirectory) [ fp (at "start") ]

    Assert.DoesNotContain(at "start/.cache/notes.txt", hits)

[<Fact>]
let ``a directory that cannot be read is skipped and counted`` () =
    let fs = Fakes.UnreadableDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "let x = 1")
    fs.AddFile(at "start/docs/notes.txt", MockFileData "let y = 2")

    let outcome = outcomeOf fs (queryFor "*.txt" FromDirectory) [ fp (at "start") ]

    Assert.Equal<FindOutcome>(Finished { Files = 1; Directories = 0; Skipped = 1 }, outcome)

[<Fact>]
let ``a finished walk counts the files and the directories it found`` () =
    let fs = tree ()

    let query =
        { queryFor "notes*" FromDirectory with
            What = FindWhat.create (NameMasks.read "notes*") None FilesAndDirectories }

    let outcome = outcomeOf fs query [ fp (at "start") ]

    Assert.Equal<FindOutcome>(Finished { Files = 3; Directories = 1; Skipped = 0 }, outcome)

[<Fact>]
let ``every directory the walk enters is reported`` () =
    let fs = tree ()
    let reported = ResizeArray<FileSystemPath>()

    hitsOf fs (recording reported) (queryFor "*.txt" FromDirectory) [ fp (at "start") ] |> ignore

    Assert.Equal<string list>(
        [ at "start"; at "start/docs"; at "start/docs/deep"; at "start/notes" ],
        paths reported |> List.sort
    )

[<Fact>]
let ``a walk stopped at its first directory says so`` () =
    let fs = tree ()
    let reported = ResizeArray<FileSystemPath>()

    let outcome =
        hitsOf fs (stoppingAfter 1 reported) (queryFor "*.txt" FromDirectory) [ fp (at "start") ]
        |> fst

    Assert.Equal<FindOutcome>(Stopped { Files = 0; Directories = 0; Skipped = 0 }, outcome)

[<Fact>]
let ``a stopped walk reports no more directories`` () =
    let fs = tree ()
    let reported = ResizeArray<FileSystemPath>()

    hitsOf fs (stoppingAfter 1 reported) (queryFor "*.txt" FromDirectory) [ fp (at "start") ]
    |> ignore

    Assert.Equal<string list>([ at "start" ], paths reported)

[<Fact>]
let ``a walk that follows links goes round a cycle once`` () =
    let fs = Fakes.LinkFollowing()
    fs.AddDirectory(at "data/docs")
    fs.Directory.CreateSymbolicLink(at "data/docs/loop", at "data") |> ignore
    let reported = ResizeArray<FileSystemPath>()

    let outcome =
        hitsOf fs (recording reported) (queryFor "*.zzz" FromDirectory) [ fp (at "data") ] |> fst

    Assert.Equal<FindOutcome>(Finished { Files = 0; Directories = 0; Skipped = 0 }, outcome)
    Assert.Equal<string list>([ at "data"; at "data/docs" ], paths reported |> List.sort)

[<Fact>]
let ``a link leading out of the walked tree is entered`` () =
    let fs = Fakes.LinkFollowing()
    fs.AddDirectory(at "data/docs")
    fs.AddDirectory(at "elsewhere")
    fs.Directory.CreateSymbolicLink(at "data/docs/away", at "elsewhere") |> ignore
    let reported = ResizeArray<FileSystemPath>()

    let outcome =
        hitsOf fs (recording reported) (queryFor "*.zzz" FromDirectory) [ fp (at "data") ] |> fst

    Assert.Equal<FindOutcome>(Finished { Files = 0; Directories = 0; Skipped = 0 }, outcome)
    Assert.Equal<string list>([ at "data"; at "data/docs"; at "data/docs/away" ], paths reported |> List.sort)

[<Fact>]
let ``a walk that stops at links never enters one`` () =
    let fs = Fakes.LinkFollowing()
    fs.AddDirectory(at "data/docs")
    fs.Directory.CreateSymbolicLink(at "data/docs/loop", at "data") |> ignore
    let reported = ResizeArray<FileSystemPath>()
    let query = { queryFor "*.zzz" FromDirectory with Links = StopAtLinks }

    hitsOf fs (recording reported) query [ fp (at "data") ] |> ignore

    Assert.Equal<string list>([ at "data"; at "data/docs" ], paths reported |> List.sort)

[<Fact>]
let ``a walk from a volume root does not cross into another drive`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes.txt", MockFileData "let x = 1")
    fs.AddFile(at "media/stick/notes.txt", MockFileData "let y = 2")

    let ground =
        { Roots = [ OnDisk(fp (at "")) ]
          OtherRoots = [ fp (at "media/stick") ] }

    let hits = hitsWithin fs (queryFor "*.txt" FromVolumeRoot) ground

    Assert.Equal<string list>([ at "start/notes.txt" ], hits)

[<Fact>]
let ``a walk of the path directories does not descend`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "bin/tool.txt", MockFileData "let x = 1")
    fs.AddFile(at "bin/share/deep.txt", MockFileData "let y = 2")

    let ground =
        { Roots = [ OnDisk(fp (at "bin")) ]
          OtherRoots = [] }

    let hits = hitsWithin fs (queryFor "*.txt" PathDirectories) ground

    Assert.Equal<string list>([ at "bin/tool.txt" ], hits)
