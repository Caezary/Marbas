module Marbas.Tests.FileSystem.ArchiveKindTests

open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Tests

let private recognised raw = ArchiveKind.recognise (EntryName.create raw)

let private format kind = ArchiveFormat.ofKind kind |> Expect.some

let private readWriteChange =
    { CanRead = true
      CanCreate = true
      CanChange = true }

let private readAlone =
    { CanRead = true
      CanCreate = false
      CanChange = false }

[<Theory>]
[<InlineData("backup.zip", "zip")>]
[<InlineData("backup.tar", "tar")>]
[<InlineData("release.tar.gz", "tar.gz")>]
[<InlineData("release.tar.bz2", "tar.bz2")>]
[<InlineData("release.tar.xz", "tar.xz")>]
[<InlineData("backup.7z", "7z")>]
[<InlineData("backup.rar", "rar")>]
let ``a name is recognised by the suffix its kind owns`` name extension =
    Assert.Equal(Some extension, recognised name |> Option.map ArchiveKind.extension)

[<Theory>]
[<InlineData("BACKUP.ZIP", "zip")>]
[<InlineData("Release.Tar.Gz", "tar.gz")>]
let ``a suffix is recognised whatever its case`` name extension =
    Assert.Equal(Some extension, recognised name |> Option.map ArchiveKind.extension)

[<Theory>]
[<InlineData("notes.txt")>]
[<InlineData("backup")>]
[<InlineData("release.gz")>]
[<InlineData("unzip")>]
[<InlineData(".zip")>]
[<InlineData(".tar.gz")>]
let ``a name no kind owns is no archive`` name = Assert.Equal(None, recognised name)

[<Fact>]
let ``zip is read, created and changed`` () =
    Assert.Equal(readWriteChange, ArchiveKind.capabilities Zip)

[<Fact>]
let ``tar gz is read, created and changed by being written again`` () =
    Assert.Equal(readWriteChange, ArchiveKind.capabilities TarGz)

[<Fact>]
let ``a kind that can be created can be changed`` () =
    let creatable =
        ArchiveKind.all
        |> List.filter (ArchiveKind.capabilities >> _.CanCreate)

    let changeable =
        ArchiveKind.all
        |> List.filter (ArchiveKind.capabilities >> _.CanChange)

    Assert.Equal<ArchiveKind list>(creatable, changeable)

[<Fact>]
let ``7z is read, created and changed, like zip`` () =
    Assert.Equal(readWriteChange, ArchiveKind.capabilities SevenZip)

[<Fact>]
let ``rar is read and nothing more, ever`` () =
    Assert.Equal(readAlone, ArchiveKind.capabilities Rar)

[<Fact>]
let ``the rest of the tar family is read and nothing more`` () =
    Assert.Equal(readAlone, ArchiveKind.capabilities Tar)
    Assert.Equal(readAlone, ArchiveKind.capabilities TarBz2)
    Assert.Equal(readAlone, ArchiveKind.capabilities TarXz)

[<Fact>]
let ``the kinds are the seven Marbas has a name for`` () =
    Assert.Equal<ArchiveKind list>([ Zip; Tar; TarGz; TarBz2; TarXz; SevenZip; Rar ], ArchiveKind.all)

[<Fact>]
let ``each kind owns an extension`` () =
    Assert.Equal("zip", ArchiveKind.extension Zip)
    Assert.Equal("tar", ArchiveKind.extension Tar)
    Assert.Equal("tar.gz", ArchiveKind.extension TarGz)
    Assert.Equal("tar.bz2", ArchiveKind.extension TarBz2)
    Assert.Equal("tar.xz", ArchiveKind.extension TarXz)
    Assert.Equal("7z", ArchiveKind.extension SevenZip)
    Assert.Equal("rar", ArchiveKind.extension Rar)

[<Fact>]
let ``only zip carries a password`` () =
    Assert.True(ArchiveKind.carriesPassword Zip)
    Assert.False(ArchiveKind.carriesPassword Tar)
    Assert.False(ArchiveKind.carriesPassword TarGz)
    Assert.False(ArchiveKind.carriesPassword TarBz2)
    Assert.False(ArchiveKind.carriesPassword TarXz)
    Assert.False(ArchiveKind.carriesPassword SevenZip)
    Assert.False(ArchiveKind.carriesPassword Rar)

[<Fact>]
let ``the formats are the kinds that claim they can be created`` () =
    Assert.Equal<ArchiveKind list>([ Zip; TarGz; SevenZip ], ArchiveFormat.all |> List.map ArchiveFormat.kind)

[<Fact>]
let ``a kind that cannot be created is no format`` () =
    Assert.Equal(None, ArchiveFormat.ofKind Tar)
    Assert.Equal(None, ArchiveFormat.ofKind TarBz2)
    Assert.Equal(None, ArchiveFormat.ofKind TarXz)
    Assert.Equal(None, ArchiveFormat.ofKind Rar)

[<Fact>]
let ``a format remembers the kind it was made of`` () =
    Assert.Equal(Zip, ArchiveFormat.kind (format Zip))
    Assert.Equal(TarGz, ArchiveFormat.kind (format TarGz))

[<Fact>]
let ``a fresh archive is a zip`` () =
    Assert.Equal(Zip, ArchiveFormat.kind ArchiveFormat.initial)

[<Fact>]
let ``a format extends a name the way its kind does`` () =
    Assert.Equal("zip", ArchiveFormat.extension (format Zip))
    Assert.Equal("tar.gz", ArchiveFormat.extension (format TarGz))

[<Fact>]
let ``a format carries a password when its kind does`` () =
    Assert.True(ArchiveFormat.carriesPassword (format Zip))
    Assert.False(ArchiveFormat.carriesPassword (format TarGz))
