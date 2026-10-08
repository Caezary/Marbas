module Marbas.Tests.FileSystem.ForeignArchiveTests

open System.Globalization
open System.IO
open System.IO.Compression
open System.Text
open System.IO.Abstractions.TestingHelpers
open ICSharpCode.SharpZipLib.Tar
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private polish = "zażółć.txt"

let private legacy = ArchiveNames.legacyEncoding 852

let private inCulture (culture: string) work =
    let previous = CultureInfo.CurrentCulture
    CultureInfo.CurrentCulture <- CultureInfo culture

    try
        work ()
    finally
        CultureInfo.CurrentCulture <- previous

let private zipNamed (naming: Encoding) =
    use buffer = new MemoryStream()

    (use zip = new ZipArchive(buffer, ZipArchiveMode.Create, true, naming)

     use writer =
         new StreamWriter(zip.CreateEntry(polish, CompressionLevel.NoCompression).Open())

     writer.Write "notes")

    buffer.ToArray()

let private tarNamed (naming: Encoding) =
    use buffer = new MemoryStream()

    (use tar = new TarOutputStream(buffer, naming)
     tar.IsStreamOwner <- false
     let content = Encoding.ASCII.GetBytes "notes"
     let entry = TarEntry.CreateTarEntry polish
     entry.Size <- int64 content.Length
     tar.PutNextEntry entry
     tar.Write(content, 0, content.Length)
     tar.CloseEntry())

    buffer.ToArray()

let private holding name raw =
    let fs = MockFileSystem()
    fs.AddDirectory(at "out")
    fs.AddFile(at ("backup/" + name), MockFileData(raw: byte array))
    fs

let private listedNames fs name =
    ArchiveListing.read fs (fp (at ("backup/" + name))) ArchivePath.root
    |> Expect.ok
    |> List.map (_.Name >> EntryName.value)

let private packed format name =
    let fs = MockFileSystem()
    fs.AddDirectory(at "backup")
    fs.AddDirectory(at "out")
    fs.AddFile(at ("work/" + polish), MockFileData "notes")

    let request =
        { Path = fp (at ("backup/" + name))
          Format = ArchiveFormat.tryCreate format |> Expect.some
          Compression = Normal
          Password = None }

    Archive.write fs Operations.unattended request [ fp (at ("work/" + polish)) ]
    |> ignore

    fs

[<WindowsFact "The OEM code page of a culture is a Windows concept">]
let ``a zip named in the legacy code page lists under those names`` () =
    let fs = holding "foreign.zip" (zipNamed legacy)

    let listed = inCulture "pl-PL" (fun () -> listedNames fs "foreign.zip")

    Assert.Equal<string list>([ polish ], listed)

[<WindowsFact "The OEM code page of a culture is a Windows concept">]
let ``a tar named in the legacy code page lists under those names`` () =
    let fs = holding "foreign.tar" (tarNamed legacy)

    let listed = inCulture "pl-PL" (fun () -> listedNames fs "foreign.tar")

    Assert.Equal<string list>([ polish ], listed)

[<WindowsFact "The OEM code page of a culture is a Windows concept">]
let ``an entry named in the legacy code page lands under that name`` () =
    let fs = holding "foreign.zip" (zipNamed legacy)

    let request =
        { Archive = fp (at "backup/foreign.zip")
          Password = None
          Destination = fp (at "out") }

    inCulture "pl-PL" (fun () ->
        Archive.extract
            fs
            Operations.unattended
            request
            [ ArchivePath.child ArchivePath.root (EntryName.create polish) ]
        |> Expect.ok)
    |> ignore

    Assert.True(fs.File.Exists(at ("out/" + polish)))

[<Fact>]
let ``a tar named in utf-8 without saying so keeps its names`` () =
    let fs = holding "unix.tar" (tarNamed Encoding.UTF8)

    let listed = inCulture "pl-PL" (fun () -> listedNames fs "unix.tar")

    Assert.Equal<string list>([ polish ], listed)

[<Fact>]
let ``a zip Marbas wrote keeps its names whatever the legacy code page is`` () =
    let fs = packed Zip "own.zip"

    let listed = inCulture "pl-PL" (fun () -> listedNames fs "own.zip")

    Assert.Equal<string list>([ polish ], listed)

[<Fact>]
let ``a tar Marbas wrote keeps its names although a tar claims no encoding`` () =
    let fs = packed TarGz "own.tar.gz"

    let listed = inCulture "pl-PL" (fun () -> listedNames fs "own.tar.gz")

    Assert.Equal<string list>([ polish ], listed)

[<WindowsFact "The OEM code page of a culture is a Windows concept">]
let ``an entry named in the legacy code page can be removed`` () =
    let fs = holding "foreign.zip" (zipNamed legacy)

    let removed =
        inCulture "pl-PL" (fun () ->
            Archive.removeEntries
                fs
                Operations.unattended
                (fp (at "backup/foreign.zip"))
                [ ArchivePath.child ArchivePath.root (EntryName.create polish) ]
            |> Expect.ok)

    Assert.Equal<Operations.ItemResult list>([], Operations.failures removed)
    Assert.Equal<string list>([], inCulture "pl-PL" (fun () -> listedNames fs "foreign.zip"))

[<Fact>]
let ``an entry added under a non-ascii name says it is utf-8`` () =
    let fs = packed Zip "own.zip"
    fs.AddFile(at "work/dodane ćwiczenie.txt", MockFileData "added")

    inCulture "pl-PL" (fun () ->
        Archive.addEntries
            fs
            Operations.unattended
            (fp (at "backup/own.zip"))
            ArchivePath.root
            [ fp (at "work/dodane ćwiczenie.txt") ]
        |> Expect.ok)
    |> ignore

    use stream = fs.File.OpenRead(at "backup/own.zip")
    use archive = new ICSharpCode.SharpZipLib.Zip.ZipFile(stream)
    let added = archive.GetEntry "dodane ćwiczenie.txt"

    Assert.True(added.IsUnicodeText)

    Assert.Equal<string list>(
        [ "dodane ćwiczenie.txt"; polish ],
        inCulture "pl-PL" (fun () -> listedNames fs "own.zip") |> List.sort
    )
