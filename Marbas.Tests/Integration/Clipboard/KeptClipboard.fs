namespace Marbas.Tests.Integration.Clipboard

open System
open Marbas.Domain
open Marbas.Adapters

type KeptClipboard() =
    let clipboard = SystemClipboard.forPlatform Windows
    let held = clipboard.GetText()

    let restored text =
        ClipboardText.tryCreate [ text ] |> Option.map clipboard.SetText

    interface IDisposable with
        member _.Dispose() =
            match held with
            | Ok(Some text) -> restored text |> ignore
            | _ -> clipboard.Empty() |> ignore
