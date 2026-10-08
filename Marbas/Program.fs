module Marbas.Program

open System.IO.Abstractions
open Marbas.Adapters

[<EntryPoint>]
let main _ =
    Session.run (FileSystem()) (HostEnvironment.current ())
