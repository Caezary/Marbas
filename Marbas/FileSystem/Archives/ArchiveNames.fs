module Marbas.FileSystem.Archives.ArchiveNames

open System
open System.Globalization
open System.Text
open SharpCompress.Common

[<Literal>]
let private Utf8CodePage = 65001

let private strictUtf8 = UTF8Encoding(false, true)

let private codePages =
    lazy (Encoding.RegisterProvider CodePagesEncodingProvider.Instance)

let legacyCodePage (culture: CultureInfo) = culture.TextInfo.OEMCodePage

let legacyEncoding codePage =
    codePages.Force()

    match codePage with
    | 0
    | Utf8CodePage -> Encoding.Latin1
    | page ->
        try
            Encoding.GetEncoding page
        with _ ->
            Encoding.Latin1

let decode (legacy: Encoding) (bytes: byte array) index count claimed =
    match claimed with
    | EncodingType.UTF8 -> Encoding.UTF8.GetString(bytes, index, count)
    | _ ->
        try
            strictUtf8.GetString(bytes, index, count)
        with :? DecoderFallbackException ->
            legacy.GetString(bytes, index, count)

let legacy () =
    CultureInfo.CurrentCulture |> legacyCodePage |> legacyEncoding

let reading () =
    ArchiveEncoding(CustomDecoder = Func<byte array, int, int, EncodingType, string>(decode (legacy ())))
