module Marbas.Tests.FileSystem.ZipMomentsTests

open System
open System.Globalization
open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open ICSharpCode.SharpZipLib.Zip
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private inside raw =
    ArchivePath.tryCreate raw |> Expect.some

let private winter = DateTimeOffset(2001, 2, 3, 3, 5, 6, TimeSpan.Zero)

let private oddSecond = winter.AddSeconds 1.0

let private archive = fp (at "backup/data.zip")

let private zip = ArchiveFormat.tryCreate Zip |> Expect.some

let private dated (moment: DateTimeOffset) (content: string) =
    MockFileData(content, LastWriteTime = moment)

let private planted () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "backup")
    fs.AddDirectory(at "out")
    fs.AddFile(at "work/dated.txt", dated winter "dated")
    fs.AddFile(at "work/odd.txt", dated oddSecond "odd")
    fs.AddFile(at "work/docs/inner.txt", dated winter "inner")
    fs.Directory.SetLastWriteTimeUtc(at "work/docs", winter.UtcDateTime)
    fs

let private packingLocked (fs: IFileSystem) password sources =
    Archive.write
        fs
        unattended
        { Path = archive
          Format = zip
          Compression = Normal
          Password = password }
        sources
    |> ignore

let private packing fs sources = packingLocked fs None sources

let private adding (fs: IFileSystem) sources =
    Archive.addEntries fs unattended archive ArchivePath.root sources
    |> Expect.ok
    |> ignore

let private unpacking (fs: IFileSystem) targets =
    Archive.extract
        fs
        unattended
        { Archive = archive
          Password = None
          Destination = fp (at "out") }
        targets
    |> Expect.ok
    |> ignore

let private exactField (fs: IFileSystem) (name: string) =
    use stream = fs.File.OpenRead(FileSystemPath.value archive)
    use held = new ZipFile(stream)
    use extra = new ZipExtraData(held.GetEntry(name).ExtraData)

    extra.GetData<ExtendedUnixData>()
    |> Option.ofObj
    |> Option.map (fun unix -> DateTimeOffset unix.ModificationTime)

let private localField (fs: IFileSystem) (name: string) =
    use stream = fs.File.OpenRead(FileSystemPath.value archive)

    use held = new IO.Compression.ZipArchive(stream, IO.Compression.ZipArchiveMode.Read)

    held.GetEntry(name).LastWriteTime.DateTime

let private momentOf (fs: IFileSystem) (path: string) =
    DateTimeOffset(fs.File.GetLastWriteTimeUtc path, TimeSpan.Zero)

let private directoryMomentOf (fs: IFileSystem) (path: string) =
    DateTimeOffset(fs.Directory.GetLastWriteTimeUtc path, TimeSpan.Zero)

let private storedMoment (fs: IFileSystem) (raw: string) =
    let segments = ArchivePath.segments (inside raw)

    ArchiveIndex.read fs archive
    |> Expect.ok
    |> List.find (fun info -> info.Segments = segments)
    |> _.Modified

[<Fact>]
let ``a file packed into a zip carries its exact moment`` () =
    let fs = planted ()

    packing fs [ fp (at "work/odd.txt") ]

    Assert.Equal(Some oddSecond, exactField fs "odd.txt")

[<Fact>]
let ``a directory packed into a zip carries its exact moment`` () =
    let fs = planted ()

    packing fs [ fp (at "work/docs") ]

    Assert.Equal(Some winter, exactField fs "docs/")

[<Fact>]
let ``a file added to a zip carries its exact moment`` () =
    let fs = planted ()
    packing fs [ fp (at "work/dated.txt") ]

    adding fs [ fp (at "work/odd.txt") ]

    Assert.Equal(Some oddSecond, exactField fs "odd.txt")

[<Fact>]
let ``a directory added to a zip carries its exact moment`` () =
    let fs = planted ()
    packing fs [ fp (at "work/dated.txt") ]

    adding fs [ fp (at "work/docs") ]

    Assert.Equal(Some winter, exactField fs "docs/")

[<Fact>]
let ``a file locked with a password carries its exact moment as well`` () =
    let fs = planted ()

    packingLocked fs (ArchivePassword.tryCreate "secret") [ fp (at "work/odd.txt") ]

    Assert.Equal(Some oddSecond, exactField fs "odd.txt")

[<Fact>]
let ``the local time is still written beside the exact moment`` () =
    let fs = planted ()

    packing fs [ fp (at "work/dated.txt") ]

    Assert.Equal(winter.LocalDateTime, localField fs "dated.txt")

[<Theory>]
[<InlineData(1965)>]
[<InlineData(2040)>]
let ``a moment the exact field cannot hold is packed with the local time alone`` (year: int) =
    let fs = planted ()
    fs.AddFile(at "work/far.txt", dated (DateTimeOffset(year, 6, 7, 8, 9, 10, TimeSpan.Zero)) "far")

    packing fs [ fp (at "work/far.txt") ]

    Assert.Equal(None, exactField fs "far.txt")

[<Fact>]
let ``adding to a zip leaves the local time of what it already held`` () =
    let fs = planted ()
    packing fs [ fp (at "work/dated.txt") ]

    adding fs [ fp (at "work/odd.txt") ]

    Assert.Equal(winter.LocalDateTime, localField fs "dated.txt")

[<Fact>]
let ``adding to a zip leaves the exact moment of what it already held`` () =
    let fs = planted ()
    packing fs [ fp (at "work/dated.txt") ]

    adding fs [ fp (at "work/odd.txt") ]

    Assert.Equal(Some winter, exactField fs "dated.txt")

[<Fact>]
let ``a file packed and unpacked keeps a second the local time cannot hold`` () =
    let fs = planted ()
    packing fs [ fp (at "work/odd.txt") ]

    unpacking fs [ inside "odd.txt" ]

    Assert.Equal(oddSecond, momentOf fs (at "out/odd.txt"))

type private Written =
    { Name: string
      Local: DateTimeOffset
      Fields: ITaggedData list }

let private unixField (moment: DateTimeOffset) =
    ExtendedUnixData(ModificationTime = moment.UtcDateTime) :> ITaggedData

let private ntfsField (moment: DateTimeOffset) =
    NTTaggedData(
        LastModificationTime = moment.UtcDateTime,
        LastAccessTime = moment.UtcDateTime,
        CreateTime = moment.UtcDateTime
    )
    :> ITaggedData

let private fieldOfKind kind moment =
    match kind with
    | "unix" -> unixField moment
    | _ -> ntfsField moment

let private extraOf (fields: ITaggedData list) =
    use extra = new ZipExtraData()
    fields |> List.iter extra.AddEntry
    extra.GetEntryData()

let private put (writer: ZipOutputStream) (written: Written) =
    ZipEntry(written.Name, DateTime = written.Local.LocalDateTime, Size = 0L, ExtraData = extraOf written.Fields)
    |> writer.PutNextEntry

    writer.CloseEntry()

let private commented (comment: string) (entries: Written list) =
    let fs = MockFileSystem()
    fs.AddDirectory(at "backup")
    fs.AddDirectory(at "out")
    use file = fs.File.Create(FileSystemPath.value archive)
    use writer = new ZipOutputStream(file)
    writer.SetComment comment
    entries |> List.iter (put writer)
    fs

let private holding entries = commented "" entries

let private late kind =
    { Name = "late.txt"
      Local = winter.AddHours 1.0
      Fields = [ fieldOfKind kind winter ] }

[<Theory>]
[<InlineData("unix")>]
[<InlineData("ntfs")>]
let ``an entry whose local time and exact moment disagree is unpacked at the exact one`` kind =
    let fs = holding [ late kind ]

    unpacking fs [ inside "late.txt" ]

    Assert.Equal(winter, momentOf fs (at "out/late.txt"))

[<Theory>]
[<InlineData("unix")>]
[<InlineData("ntfs")>]
let ``an entry whose local time and exact moment disagree is listed at the exact one`` kind =
    let fs = holding [ late kind ]

    let stored = storedMoment fs "late.txt"

    Assert.Equal(winter, stored)

[<Fact>]
let ``an entry carrying both exact fields is read at the finer one`` () =
    let finer = winter.AddMilliseconds 500.0

    let fs =
        holding
            [ { Name = "both.txt"
                Local = winter.AddHours 1.0
                Fields = [ unixField winter; ntfsField finer ] } ]

    let stored = storedMoment fs "both.txt"

    Assert.Equal(finer, stored)

[<Fact>]
let ``each entry of a zip is read at the moment its own record carries`` () =
    let later = winter.AddDays 40.0
    let plain = winter.AddDays 1.0

    let fs =
        holding
            [ { Name = "first.txt"
                Local = winter.AddHours 1.0
                Fields = [ ntfsField winter ] }
              { Name = "plain.txt"
                Local = plain
                Fields = [] }
              { Name = "last.txt"
                Local = later.AddHours 1.0
                Fields = [ ntfsField later ] } ]

    let stored = [ "first.txt"; "plain.txt"; "last.txt" ] |> List.map (storedMoment fs)

    Assert.Equal<DateTimeOffset list>([ winter; plain; later ], stored)

[<Fact>]
let ``a zip that ends in a comment is still read at its exact moments`` () =
    let fs = commented "packed by another tool" [ late "ntfs" ]

    let stored = storedMoment fs "late.txt"

    Assert.Equal(winter, stored)

[<Fact>]
let ``a zip of more entries than the short count holds is still read at its exact moments`` () =
    let fs =
        List.init 0x10001 (fun ordinal ->
            { Name = $"file{ordinal}.txt"
              Local = winter.AddHours 1.0
              Fields = [ ntfsField winter ] })
        |> holding

    let stored = storedMoment fs "file65536.txt"

    Assert.Equal(winter, stored)

let private madeBy fixture =
    let fs = MockFileSystem()
    fs.AddDirectory(at "out")
    fs.AddFile(FileSystemPath.value archive, MockFileData(EmbeddedFixture.bytes fixture))
    fs

[<Theory>]
[<InlineData("made-by-7zip.zip", "2001-02-03T03:05:06.5Z")>]
[<InlineData("made-by-zip.zip", "2001-02-03T03:05:06Z")>]
let ``a zip another tool wrote is unpacked at the moment it recorded`` fixture (recorded: string) =
    let fs = madeBy fixture
    let moment = DateTimeOffset.Parse(recorded, CultureInfo.InvariantCulture)

    unpacking fs [ inside "tree" ]

    Assert.Equal(moment, momentOf fs (at "out/tree/a.txt"))
    Assert.Equal(moment, momentOf fs (at "out/tree/sub/b.txt"))
    Assert.Equal(moment, directoryMomentOf fs (at "out/tree/sub"))

let private endRecordPointingAt (start: int) (length: int) =
    Array.concat
        [ [| 0x50uy; 0x4Buy; 0x05uy; 0x06uy |]
          Array.zeroCreate 8
          BitConverter.GetBytes length
          BitConverter.GetBytes start
          Array.zeroCreate 2 ]

let private firstMoment (bytes: byte array) =
    use stream = new IO.MemoryStream(bytes)
    ZipMoments.read stream |> ZipMoments.at 0

[<Theory>]
[<InlineData(0)>]
[<InlineData(10)>]
[<InlineData(100)>]
let ``bytes that hold no zip answer no moment`` (length: int) =
    let bytes = Array.zeroCreate<byte> length

    let moment = firstMoment bytes

    Assert.Equal(ValueNone, moment)

[<Theory>]
[<InlineData(1000, 100)>]
[<InlineData(0, 1000)>]
[<InlineData(-1, 10)>]
[<InlineData(0, -1)>]
let ``an end record that points outside the file answers no moment`` (start: int) (length: int) =
    let bytes = endRecordPointingAt start length

    let moment = firstMoment bytes

    Assert.Equal(ValueNone, moment)

[<Fact>]
let ``an end record that points at something else answers no moment`` () =
    let bytes = Array.append (Array.create 64 0xFFuy) (endRecordPointingAt 0 64)

    let moment = firstMoment bytes

    Assert.Equal(ValueNone, moment)

[<Fact>]
let ``an ordinal past the last record answers no moment`` () =
    let fs = holding [ late "ntfs" ]
    use stream = fs.File.OpenRead(FileSystemPath.value archive)

    let moment = ZipMoments.read stream |> ZipMoments.at 1

    Assert.Equal(ValueNone, moment)

[<Fact>]
let ``reading the moments leaves the stream where it stood`` () =
    let fs = holding [ late "ntfs" ]
    use stream = fs.File.OpenRead(FileSystemPath.value archive)
    stream.Position <- 7L

    ZipMoments.read stream |> ignore

    Assert.Equal(7L, stream.Position)
