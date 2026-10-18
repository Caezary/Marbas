module Marbas.Tests.DrainedPrograms

open Marbas.FileSystem
open Marbas.Adapters.LaunchProcess

let none (_: FileSystemPath) (_: string) : DrainedProgram option = None

let private knowing (name: string) (program: DrainedProgram) (_: FileSystemPath) (asked: string) =
    [ name, program ] |> Map.ofList |> Map.tryFind asked

let windowImage (name: string) (path: string) =
    knowing name (WindowImage(FileSystemPath.create path))

let batchScript (name: string) (path: string) =
    knowing name (BatchScript(FileSystemPath.create path))
