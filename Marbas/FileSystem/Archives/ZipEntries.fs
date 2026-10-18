module Marbas.FileSystem.Archives.ZipEntries

open System
open ICSharpCode.SharpZipLib.Zip
open Marbas.Domain

[<Literal>]
let private UnixHost = 3

[<Literal>]
let private ModeShift = 16

[<Literal>]
let private OrdinaryFile = 0o100644

[<Literal>]
let private OrdinaryDirectory = 0o040755

[<Literal>]
let private DirectoryAttribute = 16

[<Literal>]
let private Backslash = '\\'

let private dated (moment: DateTimeOffset option) (entry: ZipEntry) =
    moment |> Option.iter (fun at -> entry.DateTime <- at.LocalDateTime)

    moment
    |> Option.bind ZipMoments.fieldFor
    |> Option.iter (fun field -> entry.ExtraData <- field)

    entry

let private madeOn platform attributes (entry: ZipEntry) =
    match platform with
    | Windows -> entry
    | Unix ->
        entry.HostSystem <- UnixHost
        entry.ExternalFileAttributes <- attributes
        entry

let private asDirectory platform =
    madeOn platform ((OrdinaryDirectory <<< ModeShift) ||| DirectoryAttribute)

let private named platform (name: string) =
    match platform with
    | Windows -> ZipEntryFactory().MakeDirectoryEntry(name + "/", false)
    | Unix -> ZipEntry(name + "/")

let file platform moment (name: string) (size: int64) =
    ZipEntry(name, Size = size)
    |> dated moment
    |> madeOn platform (OrdinaryFile <<< ModeShift)

let directory platform moment (name: string) =
    ZipEntry(name + "/") |> dated moment |> asDirectory platform

let addedDirectory platform moment name =
    let entry = named platform name
    entry.Size <- 0L
    entry.CompressedSize <- 0L
    entry |> dated moment |> asDirectory platform

let takesFile (name: string) = not (name.EndsWith Backslash)
