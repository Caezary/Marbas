module Marbas.Tests.FileSystem.ArchiveNameTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Tests

let private fs = MockFileSystem()

let private en = EntryName.create

let private path raw = FileSystemPath.create (Root.at raw)

let private zip = ArchiveFormat.ofKind Zip |> Expect.some

let private tarGz = ArchiveFormat.ofKind TarGz |> Expect.some

let private suggested format directory targets =
    ArchiveName.suggest fs format (path directory) targets |> EntryName.value

[<Fact>]
let ``a single file is packed under its name without the old extension`` () =
    Assert.Equal("report.zip", suggested zip "work" [ en "report.txt" ])

[<Fact>]
let ``a single directory keeps the whole of its name`` () =
    Assert.Equal("docs.zip", suggested zip "work" [ en "docs" ])

[<Fact>]
let ``a compound archive extension is dropped whole`` () =
    Assert.Equal("release.zip", suggested zip "work" [ en "release.tar.gz" ])

[<Fact>]
let ``the extension of a kind that cannot be created goes just the same`` () =
    Assert.Equal("book.zip", suggested zip "work" [ en "book.rar" ])

[<Fact>]
let ``a name that is all extension keeps it`` () =
    Assert.Equal(".gitignore.zip", suggested zip "work" [ en ".gitignore" ])

[<Fact>]
let ``several targets are packed under the name of their directory`` () =
    Assert.Equal("src.tar.gz", suggested tarGz "work/src" [ en "App.fs"; en "Library.fs" ])

[<Fact>]
let ``nothing marked is still packed under the name of the directory`` () =
    Assert.Equal("src.zip", suggested zip "work/src" [])

[<WindowsFact>]
let ``a Windows root is packed under the letter of its drive`` () =
    Assert.Equal("diskCcontents.zip", suggested zip "" [ en "one"; en "two" ])

[<UnixFact>]
let ``a Unix root has no drive to name and is packed as the root`` () =
    Assert.Equal("rootcontents.zip", suggested zip "" [ en "one"; en "two" ])

[<Fact>]
let ``choosing another format swaps the extension`` () =
    Assert.Equal("docs.tar.gz", ArchiveName.inFormat tarGz (en "docs.zip") |> EntryName.value)
    Assert.Equal("docs.zip", ArchiveName.inFormat zip (en "docs.tar.gz") |> EntryName.value)

[<Fact>]
let ``a name that names no archive keeps every part of itself`` () =
    Assert.Equal("backup.2026.zip", ArchiveName.inFormat zip (en "backup.2026") |> EntryName.value)

[<Fact>]
let ``retargeting keeps the directory the name was typed into`` () =
    let retargeted = ArchiveName.retarget fs tarGz (Root.at "backup/src.zip")

    Assert.Equal(Root.at "backup/src.tar.gz", retargeted)

[<Fact>]
let ``retargeting a bare name leaves it bare`` () =
    Assert.Equal("src.tar.gz", ArchiveName.retarget fs tarGz "src.zip")

[<Fact>]
let ``retargeting text that names no file leaves it alone`` () =
    let directory = Root.at "backup/"

    Assert.Equal(directory, ArchiveName.retarget fs zip directory)

[<Fact>]
let ``a password is what was typed and nothing else is one`` () =
    Assert.Equal("secret ", ArchivePassword.tryCreate "secret " |> Expect.some |> ArchivePassword.value)
    Assert.Equal(None, ArchivePassword.tryCreate "")
