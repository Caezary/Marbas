module Marbas.FileSystem.Drives

open System
open System.IO.Abstractions

let list (fs: IFileSystem) =
    fs.DriveInfo.GetDrives()
    |> Seq.filter _.IsReady
    |> Seq.map _.Name
    |> Seq.sortWith (fun a b -> String.Compare(a, b, StringComparison.OrdinalIgnoreCase))
    |> Seq.map FileSystemPath.create
    |> List.ofSeq
