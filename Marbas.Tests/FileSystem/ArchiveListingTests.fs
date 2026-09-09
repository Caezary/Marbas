module Marbas.Tests.FileSystem.ArchiveListingTests

open System
open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open ICSharpCode.SharpZipLib.Zip
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private en = EntryName.create

let private zip = ArchiveFormat.ofKind Zip |> Expect.some

let private tarGz = ArchiveFormat.ofKind TarGz |> Expect.some

let private target = at "backup/data.zip"

let private tarTarget = at "backup/data.tar.gz"

let private inside raw = ArchivePath.tryCreate raw |> Expect.some

let private request format path =
    { Path = fp path
      Format = format
      Compression = Normal
      Password = None }

let private locked format path secret =
    { request format path with
        Password = ArchivePassword.tryCreate secret }

let private sources = [ fp (at "work/notes.txt"); fp (at "work/docs") ]

let private tree () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "backup")
    fs.AddFile(at "work/notes.txt", MockFileData "notes")
    fs.AddFile(at "work/docs/inner.txt", MockFileData "inner")
    fs.AddFile(at "work/docs/deep/leaf.txt", MockFileData "leaf")
    fs.AddDirectory(at "work/docs/empty")
    fs

let private packed () =
    let fs = tree ()
    Archive.write fs Operations.unattended (request zip target) sources |> ignore
    fs

let private handWritten (fs: IFileSystem) path (entries: (string * DateTime) list) =
    use stream = fs.File.Create path
    use writer = new ZipOutputStream(stream)

    entries
    |> List.iter (fun (key, modified) ->
        writer.PutNextEntry(ZipEntry(key, DateTime = modified))
        writer.CloseEntry())

let private keyedByHand (fs: IFileSystem) path (key: string) =
    use stream = fs.File.Create path
    use archive = new IO.Compression.ZipArchive(stream, IO.Compression.ZipArchiveMode.Create, true)

    archive.CreateEntry key |> ignore

let private listed (fs: IFileSystem) path level =
    ArchiveListing.read fs (fp path) level |> Expect.ok

let private refused (fs: IFileSystem) path =
    ArchiveListing.read fs (fp path) ArchivePath.root |> Expect.error

let private names entries =
    entries |> List.map (_.Name >> EntryName.value) |> List.sort

let private named entries raw =
    entries |> List.find (fun entry -> entry.Name = en raw)

let private unreadable error =
    match error with
    | ArchiveUnreadable inner -> Some inner
    | _ -> None

let private damagedPath error =
    match error with
    | ArchiveDamaged(path, _) -> Some path
    | _ -> None

[<Fact>]
let ``the top of an archive lists what was packed into it`` () =
    let fs = packed ()

    let entries = listed fs target ArchivePath.root

    Assert.Equal<string list>([ "docs"; "notes.txt" ], names entries)

[<Fact>]
let ``a file inside an archive keeps its kind and its size`` () =
    let fs = packed ()

    let entries = listed fs target ArchivePath.root
    let notes = named entries "notes.txt"

    Assert.Equal(File, notes.Kind)
    Assert.Equal(Some 5L, notes.Size)

[<Fact>]
let ``a directory inside an archive has a kind and no size`` () =
    let fs = packed ()

    let entries = listed fs target ArchivePath.root
    let docs = named entries "docs"

    Assert.Equal(Directory, docs.Kind)
    Assert.Equal(None, docs.Size)

[<Fact>]
let ``a file keeps the modification time it was packed with`` () =
    let fs = tree ()
    let modified = DateTime(2026, 7, 1, 12, 30, 0)
    let file = MockFileData "dated"
    file.LastWriteTime <- DateTimeOffset modified
    fs.AddFile(at "work/dated.txt", file)
    Archive.write fs Operations.unattended (request zip target) [ fp (at "work/dated.txt") ] |> ignore

    let entries = listed fs target ArchivePath.root
    let dated = named entries "dated.txt"

    Assert.Equal(DateTimeOffset modified, dated.Modified)

[<Fact>]
let ``a level below the top lists its own entries`` () =
    let fs = packed ()

    let entries = listed fs target (inside "docs")

    Assert.Equal<string list>([ "deep"; "empty"; "inner.txt" ], names entries)

[<Fact>]
let ``a level deeper still lists what is left`` () =
    let fs = packed ()

    let entries = listed fs target (inside "docs/deep")

    Assert.Equal<string list>([ "leaf.txt" ], names entries)

[<Fact>]
let ``a directory the archive kept empty lists nothing`` () =
    let fs = packed ()

    let entries = listed fs target (inside "docs/empty")

    Assert.Equal<string list>([], names entries)

[<Fact>]
let ``a directory the archive never stored is listed all the same`` () =
    let fs = tree ()
    handWritten fs target [ "docs/deep/leaf.txt", DateTime(2026, 5, 5, 9, 0, 0) ]

    let entries = listed fs target ArchivePath.root
    let docs = named entries "docs"

    Assert.Equal(Directory, docs.Kind)

[<Fact>]
let ``a level of a directory the archive never stored lists what implied it`` () =
    let fs = tree ()
    handWritten fs target [ "docs/deep/leaf.txt", DateTime(2026, 5, 5, 9, 0, 0) ]

    let entries = listed fs target (inside "docs")

    Assert.Equal<string list>([ "deep" ], names entries)

[<Fact>]
let ``a directory that was never stored is as new as the newest below it`` () =
    let fs = tree ()
    let newest = DateTime(2026, 5, 5, 9, 0, 0)

    handWritten
        fs
        target
        [ "docs/old.txt", DateTime(2024, 1, 1, 8, 0, 0)
          "docs/deep/new.txt", newest ]

    let entries = listed fs target ArchivePath.root
    let docs = named entries "docs"

    Assert.Equal(DateTimeOffset newest, docs.Modified)

[<Fact>]
let ``a backslash in a name is part of the name and not a level of its own`` () =
    let fs = tree ()
    keyedByHand fs target "odd\\name.txt"

    let entries = listed fs target ArchivePath.root

    Assert.Equal<string list>([ "odd\\name.txt" ], names entries)

[<Fact>]
let ``a tar gz lists level by level too`` () =
    let fs = tree ()
    Archive.write fs Operations.unattended (request tarGz tarTarget) sources |> ignore

    let top = listed fs tarTarget ArchivePath.root
    let below = listed fs tarTarget (inside "docs")

    Assert.Equal<string list>([ "docs"; "notes.txt" ], names top)
    Assert.Equal<string list>([ "deep"; "empty"; "inner.txt" ], names below)

[<Fact>]
let ``a protected zip still says what is in it`` () =
    let fs = tree ()
    Archive.write fs Operations.unattended (locked zip target "letmein") sources |> ignore

    let entries = listed fs target ArchivePath.root

    Assert.Equal<string list>([ "docs"; "notes.txt" ], names entries)

[<Fact>]
let ``a dotted name is hidden inside an archive as well`` () =
    let fs = tree ()
    fs.AddFile(at "work/.secret", MockFileData "hush")
    fs.AddFile(at "work/tool.exe", MockFileData "run")

    Archive.write fs Operations.unattended (request zip target) [ fp (at "work/.secret"); fp (at "work/tool.exe") ]
    |> ignore

    let entries = listed fs target ArchivePath.root
    let secret = named entries ".secret"
    let tool = named entries "tool.exe"

    Assert.Equal(Hidden, secret.Visibility)
    Assert.Equal(Executable, tool.Executability)

[<Fact>]
let ``a name that is no archive is refused before it is opened`` () =
    let fs = tree ()
    fs.AddFile(at "backup/data.bin", MockFileData "whatever")

    let error = refused fs (at "backup/data.bin")

    Assert.Equal(KindCannotBeRead(fp (at "backup/data.bin")), error)

[<Fact>]
let ``a name that no kind owns is no archive to read`` () =
    let fs = tree ()

    let error = refused fs (at "work/notes.txt")

    Assert.Equal(KindCannotBeRead(fp (at "work/notes.txt")), error)

[<Fact>]
let ``an archive that is not there says so`` () =
    let fs = tree ()

    let error = refused fs (at "backup/missing.zip")

    Assert.Equal(ArchiveNotFound(fp (at "backup/missing.zip")), error)

[<Fact>]
let ``a file that only looks like an archive is damaged`` () =
    let fs = tree ()
    fs.AddFile(at "backup/broken.zip", MockFileData "not an archive at all")

    let error = refused fs (at "backup/broken.zip")

    Assert.Equal(Some(fp (at "backup/broken.zip")), damagedPath error)

let private displayed (fs: IFileSystem) hidden path level =
    ArchiveListing.listForDisplay fs SortOrder.byName hidden (fp path) level |> Expect.ok

[<Fact>]
let ``a level shown in a panel opens with the way out`` () =
    let fs = packed ()

    let entries = displayed fs Concealed target ArchivePath.root

    Assert.Equal<string list>([ ".."; "docs"; "notes.txt" ], entries |> List.map (_.Name >> EntryName.value))

[<Fact>]
let ``a level below the top is shown with a way out of its own`` () =
    let fs = packed ()

    let entries = displayed fs Concealed target (inside "docs")

    Assert.Equal<string list>(
        [ ".."; "deep"; "empty"; "inner.txt" ],
        entries |> List.map (_.Name >> EntryName.value)
    )

[<Fact>]
let ``the way out of an archive is a directory carrying the archive's own time`` () =
    let fs = packed ()

    let parent = displayed fs Concealed target ArchivePath.root |> List.head

    Assert.Equal(Directory, parent.Kind)
    Assert.Equal(DateTimeOffset(fs.FileInfo.New(target).LastWriteTime), parent.Modified)

[<Fact>]
let ``a hidden entry inside an archive is concealed until it is asked for`` () =
    let fs = tree ()
    fs.AddFile(at "work/.secret", MockFileData "hidden")
    Archive.write fs Operations.unattended (request zip target) [ fp (at "work/.secret") ] |> ignore

    let concealed = displayed fs Concealed target ArchivePath.root
    let shown = displayed fs Shown target ArchivePath.root

    Assert.Equal<string list>([ ".." ], concealed |> List.map (_.Name >> EntryName.value))
    Assert.Equal<string list>([ ".."; ".secret" ], shown |> List.map (_.Name >> EntryName.value))

[<Fact>]
let ``a level of an archive that cannot be read fails the listing`` () =
    let fs = tree ()
    fs.AddFile(at "backup/broken.zip", MockFileData "not an archive at all")

    let error =
        ArchiveListing.listForDisplay fs SortOrder.byName Concealed (fp (at "backup/broken.zip")) ArchivePath.root
        |> Expect.error

    Assert.Equal(Some(fp (at "backup/broken.zip")), unreadable error |> Option.bind damagedPath)
