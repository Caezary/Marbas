module Marbas.Adapters.EditorProcess

open System.Diagnostics
open Marbas.FileSystem
open Marbas.Ui.App

let run (repaint: unit -> unit) (configured: string option) platform (chosen: ProgramPath option) path =
    let editorCommand =
        chosen
        |> Option.map ProgramPath.value
        |> Option.orElse configured
        |> fun editor -> EditorCommand.choose editor platform

    try
        let start = ProcessStartInfo(editorCommand, UseShellExecute = false)
        start.ArgumentList.Add(FileSystemPath.value path)

        match Process.Start start with
        | null -> ()
        | editor ->
            use editor = editor
            editor.WaitForExit()

        repaint ()
        Ok()
    with ex ->
        Error(Operations.mapException path ex)
