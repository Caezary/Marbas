module Marbas.Tests.FileSystem.ArchiveTests

open System
open System.IO
open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open System.IO.Compression
open System.Formats.Tar
open ICSharpCode.SharpZipLib.Zip
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private zip = ArchiveFormat.tryCreate Zip |> Expect.some

let private tarGz = ArchiveFormat.tryCreate TarGz |> Expect.some

let private target = at "backup/data.zip"

let private tarTarget = at "backup/data.tar.gz"

let private requested format path =
    { Path = fp path
      Format = format
      Compression = Normal
      Password = None }

let private packed () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "backup")
    fs.AddFile(at "work/notes.txt", MockFileData "notes")
    fs.AddFile(at "work/docs/inner.txt", MockFileData "inner")
    fs.AddFile(at "work/docs/deep/leaf.txt", MockFileData "leaf")
    fs

let private opened (fs: IFileSystem) path = fs.File.OpenRead path :> Stream

let private zipEntries (fs: IFileSystem) path =
    use stream = opened fs path
    use archive = new ZipFile(stream)

    [ for entry in Seq.cast<ZipEntry> archive -> entry.Name ]

let private zipText (fs: IFileSystem) path name =
    use stream = opened fs path
    use archive = new ZipFile(stream)
    use content = archive.GetInputStream(archive.GetEntry(name: string))
    use reader = new StreamReader(content)
    reader.ReadToEnd()

let private zipEntry (fs: IFileSystem) path name =
    use stream = opened fs path
    use archive = new ZipFile(stream)
    let entry = archive.GetEntry(name: string)
    entry.DateTime

let private tarEntries (fs: IFileSystem) path =
    use stream = opened fs path
    use compressed = new GZipStream(stream, CompressionMode.Decompress)
    use reader = new TarReader(compressed)

    let rec walk found =
        match reader.GetNextEntry() with
        | null -> List.rev found
        | entry -> walk ((entry.Name, entry.EntryType) :: found)

    walk []

let private watching cancelAfter =
    let reported = ResizeArray<FileSystemPath>()

    let control =
        { Report = reported.Add
          IsCancelled = fun () -> reported.Count >= cancelAfter }

    reported, control

let private itemNames (fs: IFileSystem) (results: ItemResult list) =
    results
    |> List.map (fun item -> EntryName.value (ItemSource.name fs item.Source))

let private outcomes (fs: IFileSystem) (results: ItemResult list) =
    results
    |> List.map (fun result -> EntryName.value (ItemSource.name fs result.Source), result.Outcome)

[<Fact>]
let ``a file is packed under its own name`` () =
    let fs = packed ()

    let results =
        Archive.write fs unattended (requested zip target) [ fp (at "work/notes.txt") ]

    Assert.Equal<string list>([ "notes.txt" ], zipEntries fs target)
    Assert.Equal("notes", zipText fs target "notes.txt")
    Assert.Equal<(string * Result<unit, OperationError>) list>([ "notes.txt", Ok() ], outcomes fs results)

[<Fact>]
let ``a directory is packed with the whole tree below it`` () =
    let fs = packed ()

    Archive.write fs unattended (requested zip target) [ fp (at "work/docs") ]
    |> ignore

    Assert.Equal<string list>(
        [ "docs/"; "docs/deep/"; "docs/deep/leaf.txt"; "docs/inner.txt" ],
        zipEntries fs target |> List.sort
    )

    Assert.Equal("leaf", zipText fs target "docs/deep/leaf.txt")

[<Fact>]
let ``an empty directory is kept as an entry of its own`` () =
    let fs = packed ()
    fs.AddDirectory(at "work/empty")

    Archive.write fs unattended (requested zip target) [ fp (at "work/empty") ]
    |> ignore

    Assert.Equal<string list>([ "empty/" ], zipEntries fs target)

let private linkedWork () =
    let fs = Fakes.LinkFollowing()
    fs.AddDirectory(at "backup")
    fs.AddFile(at "work/docs/inner.txt", MockFileData "inner")
    fs.Directory.CreateSymbolicLink(at "work/twin", at "work/docs") |> ignore
    fs

[<Fact>]
let ``a directory link is packed as an empty directory rather than the tree behind it`` () =
    let fs = linkedWork ()

    let results =
        Archive.write fs unattended (requested zip target) [ fp (at "work/twin") ]

    Assert.Equal<(string * Result<unit, OperationError>) list>([ "twin", Ok() ], outcomes fs results)
    Assert.Equal<string list>([ "twin/" ], zipEntries fs target)

[<Fact>]
let ``a tree holding a link to its own ancestor packs and terminates`` () =
    let fs = linkedWork ()
    fs.Directory.CreateSymbolicLink(at "work/docs/loop", at "work") |> ignore

    let results =
        Archive.write fs unattended (requested zip target) [ fp (at "work/docs") ]

    Assert.Equal<(string * Result<unit, OperationError>) list>([ "docs", Ok() ], outcomes fs results)
    Assert.Equal<string list>([ "docs/"; "docs/inner.txt"; "docs/loop/" ], zipEntries fs target |> List.sort)

[<Fact>]
let ``the modification time of a file survives the packing`` () =
    let fs = packed ()
    let modified = DateTime(2026, 7, 1, 12, 30, 0)
    let file = MockFileData "dated"
    file.LastWriteTime <- DateTimeOffset modified
    fs.AddFile(at "work/dated.txt", file)

    Archive.write fs unattended (requested zip target) [ fp (at "work/dated.txt") ]
    |> ignore

    Assert.Equal(modified, zipEntry fs target "dated.txt")

[<Fact>]
let ``several sources land side by side in one archive`` () =
    let fs = packed ()

    let results =
        Archive.write fs unattended (requested zip target) [ fp (at "work/notes.txt"); fp (at "work/docs") ]

    Assert.Equal<string list>(
        [ "docs/"; "docs/deep/"; "docs/deep/leaf.txt"; "docs/inner.txt"; "notes.txt" ],
        zipEntries fs target |> List.sort
    )

    Assert.Equal<string list>([ "notes.txt"; "docs" ], itemNames fs results)

[<Fact>]
let ``a tar gz keeps the same tree with directories of its own`` () =
    let fs = packed ()

    Archive.write fs unattended (requested tarGz tarTarget) [ fp (at "work/docs") ]
    |> ignore

    Assert.Equal<(string * TarEntryType) list>(
        [ "docs/", TarEntryType.Directory
          "docs/deep/", TarEntryType.Directory
          "docs/deep/leaf.txt", TarEntryType.RegularFile
          "docs/inner.txt", TarEntryType.RegularFile ],
        tarEntries fs tarTarget |> List.sortBy fst
    )

[<Fact>]
let ``a source that is gone fails alone and the rest is packed`` () =
    let fs = packed ()

    let results =
        Archive.write fs unattended (requested zip target) [ fp (at "work/missing.txt"); fp (at "work/notes.txt") ]

    Assert.Equal<(string * Result<unit, OperationError>) list>(
        [ "missing.txt", Error(SourceNotFound(fp (at "work/missing.txt")))
          "notes.txt", Ok() ],
        outcomes fs results
    )

    Assert.Equal<string list>([ "notes.txt" ], zipEntries fs target)

[<Fact>]
let ``every packed file is reported as it goes in`` () =
    let fs = packed ()
    let reported, control = watching Int32.MaxValue

    Archive.write fs control (requested zip target) [ fp (at "work/docs") ]
    |> ignore

    Assert.Equal<string list>(
        [ at "work/docs/deep/leaf.txt"; at "work/docs/inner.txt" ],
        reported |> Seq.map FileSystemPath.value |> Seq.sort |> List.ofSeq
    )

[<Fact>]
let ``a cancelled packing leaves no archive behind and nothing packed`` () =
    let fs = packed ()
    let _, control = watching 1

    let results =
        Archive.write fs control (requested zip target) [ fp (at "work/notes.txt"); fp (at "work/docs") ]

    Assert.False(fs.File.Exists target)

    Assert.Equal<(string * Result<unit, OperationError>) list>(
        [ "notes.txt", Error(Cancelled(fp (at "work/notes.txt")))
          "docs", Error(Cancelled(fp (at "work/docs"))) ],
        outcomes fs results
    )

[<Fact>]
let ``the zip is one that any reader can open`` () =
    let fs = packed ()

    Archive.write fs unattended (requested zip target) [ fp (at "work/docs") ]
    |> ignore

    use stream = opened fs target
    use archive = new ZipArchive(stream, ZipArchiveMode.Read)
    use content = (archive.GetEntry "docs/inner.txt").Open()
    use reader = new StreamReader(content)

    Assert.Equal<string list>(
        [ "docs/"; "docs/deep/"; "docs/deep/leaf.txt"; "docs/inner.txt" ],
        archive.Entries |> Seq.map _.FullName |> Seq.sort |> List.ofSeq
    )

    Assert.Equal("inner", reader.ReadToEnd())

[<Fact>]
let ``an archive beside what it packs is allowed`` () =
    let fs = packed ()

    Assert.False(Archive.wouldPackItself fs (fp (at "work/data.zip")) [ fp (at "work/docs") ])

[<Fact>]
let ``an archive inside a directory it packs would pack itself`` () =
    let fs = packed ()

    Assert.True(Archive.wouldPackItself fs (fp (at "work/docs/data.zip")) [ fp (at "work/docs") ])
    Assert.True(Archive.wouldPackItself fs (fp (at "work/docs/deep/data.zip")) [ fp (at "work/docs") ])

[<Fact>]
let ``an archive that is one of the sources would pack itself`` () =
    let fs = packed ()

    Assert.True(Archive.wouldPackItself fs (fp (at "work/notes.txt")) [ fp (at "work/notes.txt") ])

[<UnixFact>]
let ``an archive in a differently cased sibling is allowed where case tells them apart`` () =
    let fs = packed ()

    let refused =
        Archive.wouldPackItself fs (fp (at "work/DOCS/data.zip")) [ fp (at "work/docs") ]

    Assert.False refused

[<WindowsFact>]
let ``an archive in a differently cased sibling would pack itself where case is ignored`` () =
    let fs = packed ()

    let refused =
        Archive.wouldPackItself fs (fp (at "work/DOCS/data.zip")) [ fp (at "work/docs") ]

    Assert.True refused

let private secured format path secret =
    { requested format path with
        Password = ArchivePassword.tryCreate secret }

[<Fact>]
let ``a zip given a password locks every entry with AES-256`` () =
    let fs = packed ()

    Archive.write fs unattended (secured zip target "letmein") [ fp (at "work/docs") ]
    |> ignore

    use stream = opened fs target
    use archive = new ZipFile(stream)
    let entry = archive.GetEntry "docs/inner.txt"

    Assert.True entry.IsCrypted
    Assert.Equal(256, entry.AESKeySize)

[<Fact>]
let ``a protected zip refuses to open without the password`` () =
    let fs = packed ()

    Archive.write fs unattended (secured zip target "letmein") [ fp (at "work/notes.txt") ]
    |> ignore

    use stream = opened fs target
    use archive = new ZipFile(stream)

    Assert.Throws<ZipException>(fun () -> archive.GetInputStream(archive.GetEntry "notes.txt") |> ignore)
    |> ignore

[<Fact>]
let ``a protected zip opens with the password it was given`` () =
    let fs = packed ()

    Archive.write fs unattended (secured zip target "letmein") [ fp (at "work/notes.txt") ]
    |> ignore

    use stream = opened fs target
    use archive = new ZipFile(stream, false, StringCodec.Default)
    archive.Password <- "letmein"
    use content = archive.GetInputStream(archive.GetEntry "notes.txt")
    use reader = new StreamReader(content)

    Assert.Equal("notes", reader.ReadToEnd())

[<Fact>]
let ``a zip without a password is not encrypted`` () =
    let fs = packed ()

    Archive.write fs unattended (requested zip target) [ fp (at "work/notes.txt") ]
    |> ignore

    use stream = opened fs target
    use archive = new ZipFile(stream)

    Assert.False (archive.GetEntry "notes.txt").IsCrypted

[<Fact>]
let ``a disk that refuses every write fails each source instead of throwing`` () =
    let fs = Fakes.FullDisk()
    fs.AddDirectory(at "backup")
    fs.AddFile(at "work/notes.txt", MockFileData "notes")

    let written =
        Archive.write fs unattended (requested zip target) [ fp (at "work/notes.txt") ]

    let failure = List.exactlyOne written
    Assert.Equal<ItemSource>(ItemOnDisk(fp (at "work/notes.txt")), failure.Source)
    Assert.Equal(OperationFailed(fp target, Fakes.DiskFullReason), Expect.error failure.Outcome)

[<Fact>]
let ``an archive that cannot be finished is not left behind`` () =
    let fs = Fakes.FullDisk()
    fs.AddDirectory(at "backup")
    fs.AddFile(at "work/notes.txt", MockFileData "notes")

    Archive.write fs unattended (requested zip target) [ fp (at "work/notes.txt") ]
    |> ignore

    Assert.False(fs.File.Exists target)

let private watched () =
    let fs = Fakes.WalkWitness()
    fs.AddDirectory(at "backup")
    fs.AddFile(at "work/notes.txt", MockFileData "notes")
    fs.AddFile(at "work/docs/inner.txt", MockFileData "inner")
    fs.AddFile(at "work/docs/deep/leaf.txt", MockFileData "leaf")
    fs

[<Fact>]
let ``packing a tree enumerates each directory once`` () =
    let fs = watched ()

    Archive.write fs unattended (requested zip target) [ fp (at "work") ] |> ignore

    Assert.Equal(3, fs.Enumerations)

[<Fact>]
let ``packing a tree asks nothing about an entry it enumerated`` () =
    let fs = watched ()

    Archive.write fs unattended (requested zip target) [ fp (at "work") ] |> ignore

    Assert.Equal(0, fs.AskedAbout(at "work/docs/inner.txt"))
