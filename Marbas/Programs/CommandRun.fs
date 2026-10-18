namespace Marbas.Programs

open Marbas.Domain
open Marbas.FileSystem

type WordQuoting =
    | Bare
    | Quoted

type CommandWord = { Text: string; Quoting: WordQuoting }

type ListFile =
    { FilePath: FileSystemPath
      Listed: FileSystemPath list }

type ExpandedCommand =
    { Program: CommandWord
      Arguments: CommandWord list
      ListFile: ListFile option }

type CommandRun =
    { Label: MenuLabel
      Command: ExpandedCommand
      Run: MenuRun
      Elevation: Elevation
      WorkingDirectory: FileSystemPath }

module ExpandedCommand =

    let private written (word: CommandWord) =
        match word.Quoting with
        | Bare -> word.Text
        | Quoted -> "\"" + word.Text + "\""

    let words (command: ExpandedCommand) = command.Program :: command.Arguments

    let spelled (command: ExpandedCommand) =
        words command |> List.map written |> String.concat " "
