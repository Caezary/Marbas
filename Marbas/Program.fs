module Marbas.Program

open System.IO.Abstractions
open Marbas.Adapters

[<EntryPoint>]
let main arguments =
    Session.run (FileSystem()) (HostEnvironment.current arguments)
