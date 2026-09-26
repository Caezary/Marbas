namespace Marbas.Domain

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

    let ofName (name: EntryName) =
        if extensions.Contains((EntryName.extension name).ToLowerInvariant()) then
            Executable
        else
            NotExecutable

    let classify name byMode =
        match ofName name with
        | Executable -> Executable
        | NotExecutable -> byMode
