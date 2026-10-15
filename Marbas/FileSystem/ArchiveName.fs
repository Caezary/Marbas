module Marbas.FileSystem.ArchiveName

open System
open System.IO.Abstractions
open Marbas.Domain

[<Literal>]
let private RootStem = "rootcontents"

let private withoutArchiveExtension (raw: string) =
    EntryName.create raw
    |> ArchiveKind.recognise
    |> Option.map (fun kind -> raw.Substring(0, raw.Length - ArchiveKind.extension(kind).Length - 1))

let private stem (name: EntryName) =
    let raw = EntryName.value name

    match withoutArchiveExtension raw with
    | Some stripped -> stripped
    | None ->
        match raw.LastIndexOf '.' with
        | dot when dot <= 0 -> raw
        | dot -> raw.Substring(0, dot)

let private diskStem (fs: IFileSystem) (directory: FileSystemPath) =
    let letter =
        fs.Path.GetPathRoot(FileSystemPath.value directory)
        |> Option.ofObj
        |> Option.bind (Seq.tryFind Char.IsLetter)

    match letter with
    | Some letter -> $"disk{letter}contents"
    | None -> RootStem

let private directoryStem (fs: IFileSystem) (directory: FileSystemPath) =
    match FileSystemPath.name fs directory |> EntryName.value with
    | "" -> diskStem fs directory
    | named -> named

let inFormat (format: ArchiveFormat) (name: EntryName) =
    let raw = EntryName.value name
    let bare = withoutArchiveExtension raw |> Option.defaultValue raw

    EntryName.create $"{bare}.{ArchiveFormat.extension format}"

let suggest (fs: IFileSystem) (format: ArchiveFormat) (directory: FileSystemPath) (targets: EntryName list) =
    match targets with
    | [ single ] -> stem single
    | _ -> directoryStem fs directory
    |> EntryName.create
    |> inFormat format

let retarget (fs: IFileSystem) (format: ArchiveFormat) (text: string) =
    match fs.Path.GetFileName text with
    | null
    | "" -> text
    | fileName ->
        let renamed = EntryName.create fileName |> inFormat format |> EntryName.value

        match fs.Path.GetDirectoryName text with
        | null
        | "" -> renamed
        | directory -> fs.Path.Combine(directory, renamed)
