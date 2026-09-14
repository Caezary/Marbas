namespace Marbas.FileSystem

open System
open System.IO
open System.IO.Abstractions
open Marbas.Domain

type Drive =
    { Root: FileSystemPath
      Label: string option
      Kind: DriveKind
      Size: int64
      Free: int64 }

type DriveRow =
    | MountedDrive of Drive
    | HomeDirectory of FileSystemPath

module DriveRow =

    let root row =
        match row with
        | MountedDrive drive -> drive.Root
        | HomeDirectory home -> home

    let kind row =
        match row with
        | MountedDrive drive -> Some drive.Kind
        | HomeDirectory _ -> None

module Drives =

    let private notStorage =
        set
            [ "autofs"
              "binfmt_misc"
              "bpf"
              "cgroup"
              "cgroup2"
              "configfs"
              "debugfs"
              "devpts"
              "devtmpfs"
              "efivarfs"
              "fuse.gvfsd-fuse"
              "fuse.portal"
              "fusectl"
              "hugetlbfs"
              "mqueue"
              "nsfs"
              "overlay"
              "proc"
              "pstore"
              "ramfs"
              "rpc_pipefs"
              "securityfs"
              "selinuxfs"
              "squashfs"
              "sysfs"
              "tmpfs"
              "tracefs" ]

    let private kindOf driveType =
        match driveType with
        | DriveType.Removable -> Removable
        | DriveType.Network -> Network
        | DriveType.CDRom -> Optical
        | _ -> Fixed

    let private labelOf (root: string) (label: string) =
        if String.IsNullOrWhiteSpace label || label = root then
            None
        else
            Some label

    let private read (info: IDriveInfo) =
        try
            if info.IsReady && not (notStorage.Contains info.DriveFormat) then
                Some
                    { Root = FileSystemPath.create info.Name
                      Label = labelOf info.Name info.VolumeLabel
                      Kind = kindOf info.DriveType
                      Size = info.TotalSize
                      Free = info.AvailableFreeSpace }
            else
                None
        with
        | :? IOException
        | :? UnauthorizedAccessException -> None

    let private mounted (fs: IFileSystem) =
        fs.DriveInfo.GetDrives()
        |> Seq.choose read
        |> Seq.sortWith (fun a b ->
            String.Compare(FileSystemPath.value a.Root, FileSystemPath.value b.Root, StringComparison.OrdinalIgnoreCase))
        |> Seq.map MountedDrive
        |> List.ofSeq

    let holding (fs: IFileSystem) (path: FileSystemPath) =
        let raw = FileSystemPath.value path
        let separators = [| fs.Path.DirectorySeparatorChar; fs.Path.AltDirectorySeparatorChar |]

        let holds (root: string) =
            let bare = root.TrimEnd separators

            raw.Equals(bare, StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith(bare, StringComparison.OrdinalIgnoreCase)
               && raw.Length > bare.Length
               && Array.contains raw[bare.Length] separators

        mounted fs
        |> List.map DriveRow.root
        |> List.filter (FileSystemPath.value >> holds)
        |> List.sortByDescending (FileSystemPath.value >> String.length)
        |> List.tryHead

    let list (fs: IFileSystem) (home: FileSystemPath option) =
        let drives = mounted fs
        let homeRows = home |> Option.map HomeDirectory |> Option.toList

        drives @ homeRows |> List.distinctBy DriveRow.root
