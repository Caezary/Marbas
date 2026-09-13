namespace Marbas.FileSystem

open System
open System.IO.Abstractions
open Marbas
open Marbas.Domain
open Marbas.FileSystem.Operations

type LinkRequest =
    { Kind: LinkKind
      Target: FileSystemPath
      Location: FileSystemPath }

type LinkChoice = { Target: string; Kind: LinkKind }

type NativeLinks =
    { MakeHard: FileSystemPath -> FileSystemPath -> unit
      MakeJunction: FileSystemPath -> FileSystemPath -> unit }

module Links =

    let private absolute (fs: IFileSystem) (request: LinkRequest) =
        let standing = FileSystemPath.directory fs request.Location |> FileSystemPath.value
        let target = fs.Path.Combine(standing, FileSystemPath.value request.Target)
        FileSystemPath.create (fs.Path.GetFullPath target)

    let private comparison platform =
        match platform with
        | Windows -> StringComparison.OrdinalIgnoreCase
        | Unix -> StringComparison.Ordinal

    let private volumeOf (platform: Platform) (fs: IFileSystem) (path: FileSystemPath) =
        let raw = FileSystemPath.value path
        let separator = string fs.Path.DirectorySeparatorChar

        let holds (root: string) =
            let bare = root.TrimEnd(fs.Path.DirectorySeparatorChar, fs.Path.AltDirectorySeparatorChar)

            raw.Equals(bare, comparison platform)
            || raw.StartsWith(bare + separator, comparison platform)

        fs.DriveInfo.GetDrives()
        |> Seq.map _.Name
        |> Seq.filter holds
        |> Seq.sortByDescending String.length
        |> Seq.tryHead
        |> Option.defaultWith (fun () -> fs.Path.GetPathRoot raw)

    let private onOneVolume platform fs (target: FileSystemPath) (location: FileSystemPath) =
        String.Equals(volumeOf platform fs target, volumeOf platform fs location, comparison platform)

    let private refusalFor platform fs (request: LinkRequest) target =
        match request.Kind, FileSystemPath.kind fs target with
        | Symbolic, _ -> None
        | Hard, DirectoryEntry -> Some HardLinkToDirectory
        | Hard, _ when not (onOneVolume platform fs target request.Location) -> Some HardLinkAcrossVolumes
        | Hard, _ -> None
        | Junction, _ when platform = Unix -> Some JunctionOffWindows
        | Junction, FileEntry -> Some JunctionToFile
        | Junction, _ -> None

    let refusal (platform: Platform) (fs: IFileSystem) (request: LinkRequest) =
        refusalFor platform fs request (absolute fs request)

    let private makeSymbolic (fs: IFileSystem) (location: FileSystemPath) (target: FileSystemPath) targetKind =
        let rawLocation = FileSystemPath.value location
        let rawTarget = FileSystemPath.value target

        attempting location (fun () ->
            match targetKind with
            | DirectoryEntry -> fs.Directory.CreateSymbolicLink(rawLocation, rawTarget) |> ignore
            | _ -> fs.File.CreateSymbolicLink(rawLocation, rawTarget) |> ignore)

    let create (native: NativeLinks) (platform: Platform) (fs: IFileSystem) (request: LinkRequest) =
        let target = absolute fs request
        let location = request.Location

        match FileSystemPath.kind fs location, FileSystemPath.kind fs target with
        | FileEntry, _
        | DirectoryEntry, _ -> Error(TargetExists location)
        | NoEntry, NoEntry -> Error(SourceNotFound target)
        | NoEntry, targetKind ->
            match refusalFor platform fs request target, request.Kind with
            | Some refused, _ -> Error(LinkRefused(refused, location))
            | None, Symbolic -> makeSymbolic fs location target targetKind
            | None, Hard -> attempting location (fun () -> native.MakeHard target location)
            | None, Junction -> attempting location (fun () -> native.MakeJunction target location)

    let requests (fs: IFileSystem) kind (sources: FileSystemPath list) (directory: FileSystemPath) (text: string) =
        let typed = FileSystemPath.combine fs directory (text.Trim())

        let linking location source =
            { Kind = kind
              Target = source
              Location = location }

        let inside source =
            let location = FileSystemPath.name fs source |> EntryName.value |> FileSystemPath.combine fs typed
            linking location source

        match text.Trim(), sources with
        | "", _
        | _, [] -> []
        | _, [ source ] when FileSystemPath.kind fs typed <> DirectoryEntry -> [ linking typed source ]
        | _ -> sources |> List.map inside

    let sharedRefusal (refuse: LinkRequest -> LinkRefusal option) (requests: LinkRequest list) =
        match requests |> List.map refuse with
        | [] -> None
        | refusals when List.forall Option.isSome refusals -> List.head refusals
        | _ -> None
