namespace Marbas.Domain

open System

type CommandLine = private CommandLine of string

type MenuRun =
    | Directly
    | InShellClosing
    | InShellKeptOpen

module CommandLine =

    let private breaksALine (raw: string) = raw.IndexOfAny [| '\n'; '\r' |] >= 0

    let tryCreate (raw: string) =
        if String.IsNullOrWhiteSpace raw || breaksALine raw then
            None
        else
            Some(CommandLine(raw.Trim()))

    let value (CommandLine text) = text

module MenuRun =

    let text run =
        match run with
        | Directly -> "direct"
        | InShellClosing -> "shell-close"
        | InShellKeptOpen -> "shell-keep"

    let tryParse (text: string) =
        match text.Trim().ToLowerInvariant() with
        | "direct" -> Some Directly
        | "shell-close" -> Some InShellClosing
        | "shell-keep" -> Some InShellKeptOpen
        | _ -> None
