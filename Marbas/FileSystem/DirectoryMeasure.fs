namespace Marbas.FileSystem

open System.IO.Abstractions
open Marbas.Domain
open Marbas.FileSystem.Operations

type MeasureTarget =
    { Name: EntryName
      Location: PanelLocation }

type Measurement =
    { Name: EntryName
      Size: DirectorySize }

module DirectoryMeasure =

    let private lengthOf (fs: IFileSystem) (path: FileSystemPath) =
        (fs.FileInfo.New(FileSystemPath.value path)).Length

    let private isLink (fs: IFileSystem) (path: FileSystemPath) =
        (fs.DirectoryInfo.New(FileSystemPath.value path)).LinkTarget |> isNull |> not

    let private entriesUnder (fs: IFileSystem) (path: FileSystemPath) =
        fs.Directory.EnumerateFileSystemEntries(FileSystemPath.value path)
        |> Seq.map FileSystemPath.create
        |> List.ofSeq

    let rec private walk (fs: IFileSystem) (control: BatchControl) (path: FileSystemPath) =
        if control.IsCancelled() then
            AtLeast 0L
        else
            control.Report path

            try
                entriesUnder fs path |> List.map (sized fs control) |> DirectorySize.total
            with _ ->
                AtLeast 0L

    and private sized (fs: IFileSystem) control (path: FileSystemPath) =
        match FileSystemPath.kind fs path with
        | FileEntry -> Exact(lengthOf fs path)
        | DirectoryEntry when isLink fs path -> DirectorySize.nothingCounted
        | DirectoryEntry -> walk fs control path
        | NoEntry -> DirectorySize.nothingCounted

    let private below (level: ArchivePath) (item: ArchiveItem) =
        let names = ArchivePath.segments level

        item.Segments.Length > names.Length
        && List.truncate names.Length item.Segments = names

    let private directoryOf (item: ArchiveItem) =
        if item.IsDirectory then
            item.Segments
        else
            item.Segments |> List.truncate (item.Segments.Length - 1)

    let private levelsBelow (level: ArchivePath) items =
        let depth = ArchivePath.segments level |> List.length

        items
        |> List.collect (fun item ->
            let holding = directoryOf item
            [ depth + 1 .. holding.Length ] |> List.map (fun taken -> List.truncate taken holding))
        |> List.distinct
        |> List.sortBy List.length
        |> List.map (List.fold ArchivePath.child ArchivePath.root)

    let private scan (fs: IFileSystem) (control: BatchControl) archive level kind =
        let items =
            ArchiveReader.items fs None kind archive
            |> Seq.filter (below level)
            |> List.ofSeq

        levelsBelow level items
        |> List.iter (ArchivePath.spelled fs archive >> control.Report)

        let counted = items |> List.sumBy _.Size

        if control.IsCancelled() then AtLeast counted else Exact counted

    let private measureArchive (fs: IFileSystem) (control: BatchControl) archive level =
        if control.IsCancelled() then
            AtLeast 0L
        else
            control.Report(ArchivePath.spelled fs archive level)

            match ArchiveReader.kindOf fs archive with
            | None -> AtLeast 0L
            | Some kind ->
                try
                    scan fs control archive level kind
                with _ ->
                    AtLeast 0L

    let measure (fs: IFileSystem) (control: BatchControl) location =
        match location with
        | OnDisk path -> walk fs control path
        | InArchive(archive, level) -> measureArchive fs control archive level

    let measureAll (fs: IFileSystem) (control: BatchControl) (targets: MeasureTarget list) =
        let measured =
            targets
            |> List.map (fun target ->
                { Name = target.Name
                  Size = measure fs control target.Location })

        if control.IsCancelled() then [] else measured
