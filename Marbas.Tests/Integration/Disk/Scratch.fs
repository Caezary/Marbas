namespace Marbas.Tests.Integration.Disk

open System
open System.IO
open System.IO.Abstractions
open Marbas.FileSystem
open Marbas.Tests

type Scratch() =
    let root = Path.Combine(Path.GetTempPath(), $"marbas-disk-{Guid.NewGuid():N}")

    do Directory.CreateDirectory root |> ignore

    member _.At(relative: string) =
        Path.Combine(root, Root.relative relative)

    member this.Path(relative: string) =
        FileSystemPath.create (this.At relative)

    member this.Directory(relative: string) =
        Directory.CreateDirectory(this.At relative) |> ignore

    member this.File (relative: string) (content: string) =
        File.WriteAllText(this.At relative, content)

    interface IDisposable with
        member _.Dispose() =
            Operations.delete (FileSystem()) Operations.unattended (FileSystemPath.create root)
            |> ignore
