module Marbas.Tests.FileSystem.ArchiveNameTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Tests

let private fs = MockFileSystem()

let private en = EntryName.create

let private path raw = FileSystemPath.create (Root.at raw)

let private suggested format directory targets =
    ArchiveName.suggest fs format (path directory) targets |> EntryName.value

[<Fact>]
let ``a single file is packed under its name without the old extension`` () =
    Assert.Equal("report.zip", suggested Zip "work" [ en "report.txt" ])

[<Fact>]
let ``a single directory keeps the whole of its name`` () =
    Assert.Equal("docs.zip", suggested Zip "work" [ en "docs" ])

[<Fact>]
let ``a compound archive extension is dropped whole`` () =
    Assert.Equal("release.zip", suggested Zip "work" [ en "release.tar.gz" ])

[<Fact>]
let ``a name that is all extension keeps it`` () =
    Assert.Equal(".gitignore.zip", suggested Zip "work" [ en ".gitignore" ])

[<Fact>]
let ``several targets are packed under the name of their directory`` () =
    Assert.Equal("src.tar.gz", suggested TarGz "work/src" [ en "App.fs"; en "Library.fs" ])

[<Fact>]
let ``nothing marked is still packed under the name of the directory`` () =
    Assert.Equal("src.zip", suggested Zip "work/src" [])

[<WindowsFact>]
let ``a Windows root is packed under the letter of its drive`` () =
    Assert.Equal("diskCcontents.zip", suggested Zip "" [ en "one"; en "two" ])

[<UnixFact>]
let ``a Unix root has no drive to name and is packed as the root`` () =
    Assert.Equal("rootcontents.zip", suggested Zip "" [ en "one"; en "two" ])

[<Fact>]
let ``choosing another format swaps the extension`` () =
    Assert.Equal("docs.tar.gz", ArchiveName.inFormat TarGz (en "docs.zip") |> EntryName.value)
    Assert.Equal("docs.zip", ArchiveName.inFormat Zip (en "docs.tar.gz") |> EntryName.value)

[<Fact>]
let ``a name that names no archive keeps every part of itself`` () =
    Assert.Equal("backup.2026.zip", ArchiveName.inFormat Zip (en "backup.2026") |> EntryName.value)

[<Fact>]
let ``each format owns an extension`` () =
    Assert.Equal("zip", ArchiveFormat.extension Zip)
    Assert.Equal("tar.gz", ArchiveFormat.extension TarGz)
    Assert.Equal<ArchiveFormat list>([ Zip; TarGz ], ArchiveFormat.all)

[<Fact>]
let ``only zip carries a password`` () =
    Assert.True(ArchiveFormat.carriesPassword Zip)
    Assert.False(ArchiveFormat.carriesPassword TarGz)

[<Fact>]
let ``retargeting keeps the directory the name was typed into`` () =
    let retargeted = ArchiveName.retarget fs TarGz (Root.at "backup/src.zip")

    Assert.Equal(Root.at "backup/src.tar.gz", retargeted)

[<Fact>]
let ``retargeting a bare name leaves it bare`` () =
    Assert.Equal("src.tar.gz", ArchiveName.retarget fs TarGz "src.zip")

[<Fact>]
let ``retargeting text that names no file leaves it alone`` () =
    let directory = Root.at "backup/"

    Assert.Equal(directory, ArchiveName.retarget fs Zip directory)

[<Fact>]
let ``a password is what was typed and nothing else is one`` () =
    Assert.Equal("secret ", ArchivePassword.tryCreate "secret " |> Expect.some |> ArchivePassword.value)
    Assert.Equal(None, ArchivePassword.tryCreate "")
