module Marbas.Tests.Integration.Disk.DirectoryTimeTests

open System
open System.IO
open System.IO.Abstractions
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Tests

let private disk = FileSystem() :> IFileSystem

let private docsMoment = DateTime(2024, 3, 9, 13, 20, 30, DateTimeKind.Utc)

let private deepMoment = DateTime(2023, 11, 5, 7, 10, 12, DateTimeKind.Utc)

let private dated () =
    let scratch = new Scratch()
    scratch.Directory "work/docs/deep"
    scratch.Directory "out"
    scratch.File "work/docs/inner.txt" "inner"
    scratch.File "work/docs/deep/leaf.txt" "leaf"
    Directory.SetLastWriteTimeUtc(scratch.At "work/docs/deep", deepMoment)
    Directory.SetLastWriteTimeUtc(scratch.At "work/docs", docsMoment)
    scratch

let private formatFor path =
    FileSystemPath.name disk path
    |> ArchiveKind.recognise
    |> Option.bind ArchiveFormat.tryCreate
    |> Expect.some

let private packed (scratch: Scratch) extension =
    let archive = scratch.Path $"data.{extension}"

    Archive.write
        disk
        Operations.unattended
        { Path = archive
          Format = formatFor archive
          Compression = Normal
          Password = None }
        [ scratch.Path "work/docs" ]
    |> ignore

    archive

let private unpacked (scratch: Scratch) archive =
    Archive.extract
        disk
        Operations.unattended
        { Archive = archive
          Password = None
          Destination = scratch.Path "out" }
        [ ArchivePath.root ]
    |> Expect.ok

[<Theory>]
[<InlineData("zip")>]
[<InlineData("tar.gz")>]
[<InlineData("7z")>]
let ``a directory unpacked onto a disk keeps its time though files were written into it`` extension =
    use scratch = dated ()
    let archive = packed scratch extension

    unpacked scratch archive |> ignore

    Assert.Equal("inner", File.ReadAllText(scratch.At "out/docs/inner.txt"))
    Assert.Equal(docsMoment, Directory.GetLastWriteTimeUtc(scratch.At "out/docs"))

[<Theory>]
[<InlineData("zip")>]
[<InlineData("tar.gz")>]
[<InlineData("7z")>]
let ``a directory below it keeps its own time on a disk`` extension =
    use scratch = dated ()
    let archive = packed scratch extension

    unpacked scratch archive |> ignore

    Assert.Equal("leaf", File.ReadAllText(scratch.At "out/docs/deep/leaf.txt"))
    Assert.Equal(deepMoment, Directory.GetLastWriteTimeUtc(scratch.At "out/docs/deep"))
