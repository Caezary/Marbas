module Marbas.Tests.WindowPrograms

open Marbas.Domain
open Marbas.FileSystem

let none (_: FileSystemPath) (_: string) : FileSystemPath option = None

let knowing (name: string) (path: string) (_: FileSystemPath) (asked: string) =
    [ name, FileSystemPath.create path ] |> Map.ofList |> Map.tryFind asked
