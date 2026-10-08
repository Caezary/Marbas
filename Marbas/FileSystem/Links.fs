namespace Marbas.FileSystem

open System.IO.Abstractions
open FsToolkit.ErrorHandling
open Marbas.Domain
open Marbas.FileSystem.Operations

type LinkRequest =
    { Kind: LinkKind
      Target: FileSystemPath
      Location: FileSystemPath }

type LinkChoice = { Target: string; Kind: LinkKind }

type NativeLinks =
    { MakeHard: FileSystemPath -> FileSystemPath -> unit
      MakeJunction: (FileSystemPath -> FileSystemPath -> unit) option }

module Links =

    let available (native: NativeLinks) =
        match native.MakeJunction with
        | Some _ -> [ Symbolic; Hard; Junction ]
        | None -> [ Symbolic; Hard ]

    let private absolute (fs: IFileSystem) (request: LinkRequest) =
        let standing = FileSystemPath.directory fs request.Location |> FileSystemPath.value
        let target = fs.Path.Combine(standing, FileSystemPath.value request.Target)
        FileSystemPath.create (fs.Path.GetFullPath target)

    let private onOneVolume platform fs (target: FileSystemPath) (location: FileSystemPath) =
        FileSystemPath.sameEntry platform (Drives.volumeOf platform fs target) (Drives.volumeOf platform fs location)

    let private refusalForAbsolute platform fs (request: LinkRequest) target =
        match request.Kind, FileSystemPath.kind fs target with
        | Symbolic, _ -> None
        | Hard, DirectoryEntry -> Some HardLinkToDirectory
        | Hard, _ when not (onOneVolume platform fs target request.Location) -> Some HardLinkAcrossVolumes
        | Hard, _ -> None
        | Junction, FileEntry -> Some JunctionToFile
        | Junction, _ -> None

    let refusal (platform: Platform) (fs: IFileSystem) (request: LinkRequest) =
        refusalForAbsolute platform fs request (absolute fs request)

    let private makeSymbolic (fs: IFileSystem) (location: FileSystemPath) (target: FileSystemPath) targetKind =
        let rawLocation = FileSystemPath.value location
        let rawTarget = FileSystemPath.value target

        attempting location (fun () ->
            match targetKind with
            | DirectoryEntry -> fs.Directory.CreateSymbolicLink(rawLocation, rawTarget) |> ignore
            | _ -> fs.File.CreateSymbolicLink(rawLocation, rawTarget) |> ignore)

    let private refusing location refusal =
        match refusal with
        | Some refused -> Error(LinkRefused(refused, location))
        | None -> Ok()

    let private make (native: NativeLinks) fs (request: LinkRequest) target targetKind =
        let location = request.Location

        match request.Kind, native.MakeJunction with
        | Symbolic, _ -> makeSymbolic fs location target targetKind
        | Hard, _ -> attempting location (fun () -> native.MakeHard target location)
        | Junction, Some makeJunction -> attempting location (fun () -> makeJunction target location)
        | Junction, None -> Error(LinkRefused(KindUnavailable, location))

    let create (native: NativeLinks) (platform: Platform) (fs: IFileSystem) (request: LinkRequest) =
        let target = absolute fs request
        let location = request.Location
        let targetKind = FileSystemPath.kind fs target

        result {
            do! Result.requireTrue (TargetExists location) (FileSystemPath.kind fs location = NoEntry)
            do! Result.requireFalse (SourceNotFound target) (targetKind = NoEntry)
            do! refusalForAbsolute platform fs request target |> refusing location
            return! make native fs request target targetKind
        }

    let requests (fs: IFileSystem) kind (sources: FileSystemPath list) (directory: FileSystemPath) (text: string) =
        let typed = FileSystemPath.combine fs directory (text.Trim())

        let linking location source =
            { Kind = kind
              Target = source
              Location = location }

        let linkInDirectory source =
            let location =
                FileSystemPath.name fs source
                |> EntryName.value
                |> FileSystemPath.combine fs typed

            linking location source

        match text.Trim(), sources with
        | "", _
        | _, [] -> []
        | _, [ source ] when FileSystemPath.kind fs typed <> DirectoryEntry -> [ linking typed source ]
        | _ -> sources |> List.map linkInDirectory

    let sharedRefusal (refuse: LinkRequest -> LinkRefusal option) (requests: LinkRequest list) =
        match requests |> List.map refuse with
        | (Some _ as first) :: rest when List.forall Option.isSome rest -> first
        | _ -> None
