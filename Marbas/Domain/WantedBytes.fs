namespace Marbas.Domain

open System
open System.Globalization

type WantedBytes = private WantedBytes of byte array

type BytesError =
    | NoBytes
    | NotAByte of token: string

module WantedBytes =

    let private separators = [| ' '; '\t' |]

    let private isHex (token: string) = token |> Seq.forall Uri.IsHexDigit

    let private hexByte (digits: string) =
        Byte.Parse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture)

    let private bytesOf (token: string) =
        match token.Length with
        | 1 when isHex token -> Ok [ hexByte token ]
        | length when length % 2 = 0 && isHex token ->
            Ok [ for pair in 0 .. length / 2 - 1 -> hexByte (token.Substring(pair * 2, 2)) ]
        | _ -> Error(NotAByte token)

    let private gathered (tokens: string list) =
        let folding taken token =
            taken |> Result.bind (fun bytes -> bytesOf token |> Result.map (List.append bytes))

        tokens |> List.fold folding (Ok [])

    let tryParse (raw: string) =
        match raw.Split(separators, StringSplitOptions.RemoveEmptyEntries) |> List.ofArray with
        | [] -> Error NoBytes
        | tokens -> gathered tokens |> Result.map (Array.ofList >> WantedBytes)

    let value (WantedBytes bytes) = Array.copy bytes

    let length (WantedBytes bytes) = bytes.Length

    let spelled (WantedBytes bytes) =
        bytes |> Array.map (fun b -> b.ToString "X2") |> String.concat " "
