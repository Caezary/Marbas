namespace Marbas.Locations

open System.IO.Abstractions
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.Archives
open Marbas.FileSystem.Operations
open Marbas.FileSystem.DirectoryListing

type MeasureTarget =
    { Name: EntryName
      Location: PanelLocation }

type Measurement =
    { Name: EntryName; Size: DirectorySize }

module DirectoryMeasure =

    let rec private walk (fs: IFileSystem) (control: BatchControl) (path: FileSystemPath) =
        if control.IsCancelled() then
            AtLeast 0L
        else
            control.Report path

            try
                children fs path |> List.map (sized fs control) |> DirectorySize.total
            with _ ->
                AtLeast 0L

    and private sized (fs: IFileSystem) control (child: Child) =
        match child.Entry.Kind, child.Entry.LinkTarget with
        | File, _ -> Exact(child.Entry.Size |> Option.defaultValue 0L)
        | Directory, Some _ -> DirectorySize.nothingCounted
        | Directory, None -> walk fs control child.Path

    let private directoryOf (info: ArchiveEntryInfo) =
        if ArchiveEntryInfo.isDirectory info then
            info.Segments
        else
            info.Segments |> List.truncate (info.Segments.Length - 1)

    let private levelsBelow (level: ArchivePath) items =
        let depth = ArchivePath.depth level

        items
        |> List.collect (fun info ->
            let holding = directoryOf info

            [ depth + 1 .. holding.Length ]
            |> List.map (fun taken -> List.truncate taken holding))
        |> List.distinct
        |> List.sortBy List.length
        |> List.map (List.fold ArchivePath.child ArchivePath.root)

    let private scan (fs: IFileSystem) (control: BatchControl) archive level indexed =
        let items =
            indexed
            |> List.filter (fun info -> ArchivePath.strictlyUnder level info.Segments)

        levelsBelow level items
        |> List.iter (ArchivePath.spelled fs archive >> control.Report)

        let counted = items |> List.sumBy _.Size

        if control.IsCancelled() then
            AtLeast counted
        else
            Exact counted

    let private measureArchive (fs: IFileSystem) (control: BatchControl) archive level =
        if control.IsCancelled() then
            AtLeast 0L
        else
            control.Report(ArchivePath.spelled fs archive level)

            match ArchiveIndex.read fs archive with
            | Error _ -> AtLeast 0L
            | Ok indexed -> scan fs control archive level indexed

    let measure (fs: IFileSystem) (control: BatchControl) location =
        match location with
        | OnDisk path -> walk fs control path
        | InArchive(archive, level) -> measureArchive fs control archive level
        | SearchResults _ -> AtLeast 0L

    let measureAll (fs: IFileSystem) (control: BatchControl) (targets: MeasureTarget list) =
        let measured =
            targets
            |> List.map (fun target ->
                { Name = target.Name
                  Size = measure fs control target.Location })

        if control.IsCancelled() then [] else measured
