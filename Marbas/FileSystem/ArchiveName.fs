namespace Marbas.FileSystem

open System
open System.IO.Abstractions
open Marbas.Domain

module ArchiveName =

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
        match fs.Path.GetPathRoot(FileSystemPath.value directory) |> Seq.tryFind Char.IsLetter with
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
        | "" -> text
        | fileName ->
            let renamed = EntryName.create fileName |> inFormat format |> EntryName.value

            match fs.Path.GetDirectoryName text with
            | directory when String.IsNullOrEmpty directory -> renamed
            | directory -> fs.Path.Combine(directory, renamed)
