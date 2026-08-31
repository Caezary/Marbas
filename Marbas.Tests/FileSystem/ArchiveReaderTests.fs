module Marbas.Tests.FileSystem.ArchiveReaderTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private archive = at "backup/data.zip"

let private fs = MockFileSystem()

[<Fact>]
let ``a missing file maps to an archive that is not there`` () =
    let failure = IO.FileNotFoundException "gone"

    let error = ArchiveReader.mapException (fp archive) failure

    Assert.Equal(ArchiveNotFound(fp archive), error)

[<Fact>]
let ``a refused password maps to a password that is needed`` () =
    let failure = SharpCompress.Common.CryptographicException "wrong password"

    let error = ArchiveReader.mapException (fp archive) failure

    Assert.Equal(PasswordNeeded(fp archive), error)

[<Fact>]
let ``every other failure maps to a damaged archive`` () =
    let failure = IO.IOException "boom"

    let error = ArchiveReader.mapException (fp archive) failure

    Assert.Equal(ArchiveDamaged(fp archive, "boom"), error)

[<Fact>]
let ``a kind that claims it can be read is the kind of the file`` () =
    let kind = ArchiveReader.kindOf fs (fp archive)

    Assert.Equal(Some Zip, kind)

[<Theory>]
[<InlineData("backup/data.bin")>]
[<InlineData("backup/data.rar.txt")>]
[<InlineData("work/notes.txt")>]
let ``a name that is no archive is no kind to open`` raw =
    let kind = ArchiveReader.kindOf fs (fp (at raw))

    Assert.Equal(None, kind)

[<Theory>]
[<InlineData("docs/inner.txt", "docs,inner.txt")>]
[<InlineData("docs/", "docs")>]
[<InlineData("./docs/inner.txt", "docs,inner.txt")>]
[<InlineData("/docs/inner.txt", "docs,inner.txt")>]
[<InlineData("docs/../evil.txt", "docs,..,evil.txt")>]
let ``a key is read as the names it is made of`` key expected =
    let segments = ArchiveReader.segmentsOf key

    Assert.Equal(expected, segments |> List.map EntryName.value |> String.concat ",")

[<Fact>]
let ``a refused file maps to an archive that may not be touched`` () =
    let failure = UnauthorizedAccessException "denied"

    let error = ArchiveReader.mapException (fp archive) failure

    Assert.Equal(ArchiveAccessDenied(fp archive), error)
