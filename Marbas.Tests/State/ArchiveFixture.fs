module Marbas.Tests.State.ArchiveFixture

open System.Formats.Tar
open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Marbas.FileSystem
open Marbas.Domain
open Marbas.State.AppState
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let archive = fp (at "start/backup.zip")

let sources = [ fp (at "work/notes.txt"); fp (at "work/docs") ]

let request format path =
    { Path = fp path
      Format = ArchiveFormat.ofKind format |> Expect.some
      Compression = Normal
      Password = None }

let tree () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start")
    fs.AddDirectory(at "backup")
    fs.AddFile(at "work/notes.txt", MockFileData "notes")
    fs.AddFile(at "work/docs/inner.txt", MockFileData "inner")
    fs.AddFile(at "work/docs/deep/leaf.txt", MockFileData "leaf")
    fs

let packedAs format path =
    let fs = tree ()
    Archive.write fs Operations.unattended (request format path) sources |> ignore
    fs

let tarredAs (path: string) =
    let fs = tree ()
    use file = fs.File.Create path
    use tar = new TarWriter(file, TarEntryFormat.Pax, true)

    use notes = fs.File.OpenRead(at "work/notes.txt")
    tar.WriteEntry(PaxTarEntry(TarEntryType.RegularFile, "notes.txt", DataStream = notes))
    tar.WriteEntry(PaxTarEntry(TarEntryType.Directory, "docs/"))

    use inner = fs.File.OpenRead(at "work/docs/inner.txt")
    tar.WriteEntry(PaxTarEntry(TarEntryType.RegularFile, "docs/inner.txt", DataStream = inner))

    fs

let packed () = packedAs Zip (at "start/backup.zip")

let loaded (fs: IFileSystem) =
    create (fp (at "start"))
    |> loadPanel fs Left (fp (at "start"))
    |> Expect.ok
    |> loadPanel fs Right (fp (at "start"))
    |> Expect.ok

let standingInside (fs: IFileSystem) side =
    loaded fs
    |> activate side
    |> setCursor side 1
    |> enter fs
    |> Expect.ok
    |> activate Left
