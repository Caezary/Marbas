namespace Marbas.FileSystem

open System
open System.IO.Abstractions

type RootChoice =
    | MountedDrives
    | RootAndHome of home: FileSystemPath

module Roots =

    let mounted (fs: IFileSystem) =
        fs.DriveInfo.GetDrives()
        |> Seq.filter _.IsReady
        |> Seq.map _.Name
        |> Seq.sortWith (fun a b -> String.Compare(a, b, StringComparison.OrdinalIgnoreCase))
        |> Seq.map FileSystemPath.create
        |> List.ofSeq

    let private rootAndHome (fs: IFileSystem) (home: FileSystemPath) =
        let root = fs.Path.GetPathRoot(FileSystemPath.value home) |> FileSystemPath.create

        [ root; home ] |> List.distinct

    let list (fs: IFileSystem) choice =
        match choice with
        | MountedDrives -> mounted fs
        | RootAndHome home -> rootAndHome fs home
