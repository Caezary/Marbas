namespace Marbas.FileSystem

open System.IO

type Executability =
    | Executable
    | NotExecutable

module Executability =

    let private extensions =
        set [ "exe"; "com"; "bat"; "cmd"; "msi" ]

    let private namedExecutable (name: EntryName) =
        extensions.Contains((EntryName.extension name).ToLowerInvariant())

    let private markedExecutable (mode: UnixFileMode) =
        mode.HasFlag UnixFileMode.UserExecute
        || mode.HasFlag UnixFileMode.GroupExecute
        || mode.HasFlag UnixFileMode.OtherExecute

    let classify (name: EntryName) (mode: UnixFileMode) =
        if namedExecutable name || markedExecutable mode then
            Executable
        else
            NotExecutable
