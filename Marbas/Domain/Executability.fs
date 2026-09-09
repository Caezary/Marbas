namespace Marbas.Domain

open System.IO

type Executability =
    | Executable
    | NotExecutable

module Executability =

    let private extensions =
        set
            [ "bat"
              "cmd"
              "com"
              "cpl"
              "exe"
              "msc"
              "msi"
              "scr"
              "vbe"
              "vbs"
              "wsf"
              "wsh" ]

    let private namedExecutable (name: EntryName) =
        extensions.Contains((EntryName.extension name).ToLowerInvariant())

    let private markedExecutable (mode: UnixFileMode) =
        mode.HasFlag UnixFileMode.UserExecute
        || mode.HasFlag UnixFileMode.GroupExecute
        || mode.HasFlag UnixFileMode.OtherExecute

    let ofMode (mode: UnixFileMode) =
        if markedExecutable mode then Executable else NotExecutable

    let classify (name: EntryName) (mode: UnixFileMode) =
        if namedExecutable name then Executable else ofMode mode
