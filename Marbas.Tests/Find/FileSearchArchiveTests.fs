module Marbas.Tests.Find.FileSearchArchiveTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Find.FileSearch
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private inside raw =
    ArchivePath.tryCreate raw |> Expect.some

let private zip = ArchiveFormat.tryCreate Zip |> Expect.some

let private tarGz = ArchiveFormat.tryCreate TarGz |> Expect.some

let private sources = [ fp (at "work/notes.txt"); fp (at "work/docs") ]

let private request format path password =
    { Path = fp path
      Format = format
      Compression = Normal
      Password = password }

let private tree () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "backup")
    fs.AddFile(at "work/notes.txt", MockFileData "let x = 1")
    fs.AddFile(at "work/docs/inner.txt", MockFileData "mutable y = 2")
    fs.AddFile(at "work/docs/deep/leaf.txt", MockFileData "let z = 3")
    fs

let private packedInto format path password =
    let fs = tree ()

    Archive.write fs Operations.unattended (request format path password) sources
    |> ignore

    fs

let private queryFor masks =
    { What = FindWhat.create (NameMasks.read masks) None FilesOnly
      Scope = FromDirectory
      Hidden = Concealed
      Links = FollowLinks
      Archives = SearchArchives }

let private searchFor raw =
    { Text = FindText.tryCreate raw |> Expect.some
      Case = IgnoringCase
      Words = Anywhere
      Sense = Containing
      Binary = Skipped }

let private hitsOf (fs: IFileSystem) query ground =
    let hits = ResizeArray<FindHit>()

    let outcome =
        run fs Operations.unattended (fun report -> hits.Add report.Hit) query ground

    outcome,
    hits
    |> Seq.map (FindHit.spelled fs >> FileSystemPath.value)
    |> List.ofSeq
    |> List.sort

let private inBackup (fs: IFileSystem) query =
    hitsOf
        fs
        query
        { Roots = [ OnDisk(fp (at "backup")) ]
          OtherRoots = [] }
    |> snd

[<Fact>]
let ``a zip is searched at every level when archives are asked for`` () =
    let fs = packedInto zip (at "backup/data.zip") None

    let hits = inBackup fs (queryFor "*.txt")

    Assert.Equal<string list>(
        [ at "backup/data.zip/docs/deep/leaf.txt"
          at "backup/data.zip/docs/inner.txt"
          at "backup/data.zip/notes.txt" ],
        hits
    )

[<Fact>]
let ``a tar.gz is searched the same way`` () =
    let fs = packedInto tarGz (at "backup/data.tar.gz") None

    let hits = inBackup fs (queryFor "*.txt")

    Assert.Equal<string list>(
        [ at "backup/data.tar.gz/docs/deep/leaf.txt"
          at "backup/data.tar.gz/docs/inner.txt"
          at "backup/data.tar.gz/notes.txt" ],
        hits
    )

[<Fact>]
let ``an archive is left closed unless it is asked for`` () =
    let fs = packedInto zip (at "backup/data.zip") None

    let query =
        { queryFor "*.txt" with
            Archives = SkipArchives }

    let hits = inBackup fs query

    Assert.Empty hits

[<Fact>]
let ``the archive itself is a hit by its own name`` () =
    let fs = packedInto zip (at "backup/data.zip") None

    let hits = inBackup fs (queryFor "*.zip")

    Assert.Equal<string list>([ at "backup/data.zip" ], hits)

[<Fact>]
let ``text is matched inside the entries of an archive`` () =
    let fs = packedInto zip (at "backup/data.zip") None

    let query =
        { queryFor "*.txt" with
            What = FindWhat.create (NameMasks.read "*.txt") (Some(searchFor "let")) FilesOnly }

    let hits = inBackup fs query

    Assert.Equal<string list>([ at "backup/data.zip/docs/deep/leaf.txt"; at "backup/data.zip/notes.txt" ], hits)

[<Fact>]
let ``a directory inside an archive is a hit when directories are wanted`` () =
    let fs = packedInto zip (at "backup/data.zip") None

    let query =
        { queryFor "deep" with
            What = FindWhat.create (NameMasks.read "deep") None FilesAndDirectories }

    let hits = inBackup fs query

    Assert.Equal<string list>([ at "backup/data.zip/docs/deep" ], hits)

[<Fact>]
let ``an archive inside an archive is a hit but is not entered`` () =
    let fs = packedInto zip (at "backup/inner.zip") None
    fs.AddDirectory(at "outer")
    fs.File.Move(at "backup/inner.zip", at "outer/inner.zip")

    Archive.write fs Operations.unattended (request zip (at "backup/data.zip") None) [ fp (at "outer") ]
    |> ignore

    let hits = inBackup fs (queryFor "*.txt")

    Assert.Empty hits

[<Fact>]
let ``an archive that wants a password is counted as skipped`` () =
    let secret = ArchivePassword.tryCreate "opensesame"
    let fs = packedInto zip (at "backup/data.zip") secret

    let outcome =
        hitsOf
            fs
            (queryFor "*.txt")
            { Roots = [ OnDisk(fp (at "backup")) ]
              OtherRoots = [] }
        |> fst

    Assert.Equal<FindOutcome>(
        Finished
            { Files = 0
              Directories = 0
              Skipped = 1 },
        outcome
    )

[<Fact>]
let ``a panel standing inside an archive searches below its level`` () =
    let fs = packedInto zip (at "backup/data.zip") None

    let ground =
        { Roots = [ InArchive(fp (at "backup/data.zip"), inside "docs") ]
          OtherRoots = [] }

    let hits = hitsOf fs (queryFor "*.txt") ground |> snd

    Assert.Equal<string list>([ at "backup/data.zip/docs/deep/leaf.txt"; at "backup/data.zip/docs/inner.txt" ], hits)

[<Fact>]
let ``a panel inside an archive searches it even with archives switched off`` () =
    let fs = packedInto zip (at "backup/data.zip") None

    let query =
        { queryFor "*.txt" with
            Archives = SkipArchives }

    let ground =
        { Roots = [ InArchive(fp (at "backup/data.zip"), ArchivePath.root) ]
          OtherRoots = [] }

    let hits = hitsOf fs query ground |> snd

    Assert.Equal<string list>(
        [ at "backup/data.zip/docs/deep/leaf.txt"
          at "backup/data.zip/docs/inner.txt"
          at "backup/data.zip/notes.txt" ],
        hits
    )

[<Fact>]
let ``one level of an archive is read when only that level is asked for`` () =
    let fs = packedInto zip (at "backup/data.zip") None

    let query =
        { queryFor "*.txt" with
            Scope = DirectoryOnly }

    let ground =
        { Roots = [ InArchive(fp (at "backup/data.zip"), ArchivePath.root) ]
          OtherRoots = [] }

    let hits = hitsOf fs query ground |> snd

    Assert.Equal<string list>([ at "backup/data.zip/notes.txt" ], hits)

[<Literal>]
let private BigEntryLength = 4194304

[<Literal>]
let private ReadingBudget = 524288

let private bigArchive () =
    let fs = Fakes.ByteCounting()
    fs.AddDirectory(at "backup")
    fs.AddFile(at "work/big.txt", MockFileData("needle" + System.String('x', BigEntryLength)))

    Archive.write
        fs
        Operations.unattended
        { Path = fp (at "backup/data.zip")
          Format = zip
          Compression = Stored
          Password = None }
        [ fp (at "work/big.txt") ]
    |> ignore

    fs

[<Fact>]
let ``text found at the head of a large entry is matched without the entry being read whole`` () =
    let fs = bigArchive ()

    let query =
        { queryFor "*.txt" with
            What = FindWhat.create (NameMasks.read "*.txt") (Some(searchFor "needle")) FilesOnly }

    let hits = inBackup fs query

    Assert.Equal<string list>([ at "backup/data.zip/big.txt" ], hits)
    Assert.True(fs.BytesRead < ReadingBudget, $"read {fs.BytesRead} bytes of the archive")
