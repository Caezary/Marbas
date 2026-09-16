namespace Marbas.FileSystem

open System.Text

type Charset =
    | Utf8
    | Utf16Le
    | Utf16Be
    | Latin1

type TextEncoding = { Charset: Charset; HasBom: bool }

type Content =
    | Textual
    | Binary

module TextEncoding =

    [<Literal>]
    let ProbeLength = 8192

    let private utf8Mark = [| 0xEFuy; 0xBBuy; 0xBFuy |]

    let private utf16LeMark = [| 0xFFuy; 0xFEuy |]

    let private utf16BeMark = [| 0xFEuy; 0xFFuy |]

    let private startsWith (mark: byte array) (probe: byte array) =
        probe.Length >= mark.Length && Array.forall2 (=) mark probe[.. mark.Length - 1]

    let private sequenceLength (lead: byte) =
        if lead < 0x80uy then Some 1
        elif lead >= 0xC2uy && lead <= 0xDFuy then Some 2
        elif lead >= 0xE0uy && lead <= 0xEFuy then Some 3
        elif lead >= 0xF0uy && lead <= 0xF4uy then Some 4
        else None

    let private isTrailing (byte: byte) = byte >= 0x80uy && byte <= 0xBFuy

    let private isUtf8 (probe: byte array) =
        let mutable index = 0
        let mutable valid = true

        while valid && index < probe.Length do
            match sequenceLength probe[index] with
            | None -> valid <- false
            | Some length when index + length > probe.Length -> index <- probe.Length
            | Some length ->
                if Array.forall isTrailing probe[index + 1 .. index + length - 1] then
                    index <- index + length
                else
                    valid <- false

        valid

    let private wideCharset (probe: byte array) =
        let pairs = probe.Length / 2
        let zeros side = Seq.init pairs (fun pair -> probe[pair * 2 + side]) |> Seq.filter ((=) 0uy) |> Seq.length

        let leading = zeros 0
        let trailing = zeros 1

        if pairs = 0 then None
        elif leading = 0 && trailing * 2 > pairs then Some Utf16Le
        elif trailing = 0 && leading * 2 > pairs then Some Utf16Be
        else None

    let private examine (probe: byte array) =
        let plain charset = { Charset = charset; HasBom = false }

        if startsWith utf8Mark probe then { Charset = Utf8; HasBom = true }, Textual
        elif startsWith utf16LeMark probe then { Charset = Utf16Le; HasBom = true }, Textual
        elif startsWith utf16BeMark probe then { Charset = Utf16Be; HasBom = true }, Textual
        else
            match wideCharset probe with
            | Some charset -> plain charset, Textual
            | None when Array.contains 0uy probe -> plain Latin1, Binary
            | None -> plain (if isUtf8 probe then Utf8 else Latin1), Textual

    let detect (probe: byte array) = examine probe |> fst

    let contentOf (probe: byte array) = examine probe |> snd

    let name encoding =
        match encoding.Charset with
        | Utf8 -> "UTF-8"
        | Utf16Le -> "UTF-16 LE"
        | Utf16Be -> "UTF-16 BE"
        | Latin1 -> "Latin-1"

    let unitSize encoding =
        match encoding.Charset with
        | Utf16Le
        | Utf16Be -> 2
        | Utf8
        | Latin1 -> 1

    let preambleLength encoding =
        if not encoding.HasBom then
            0
        else
            match encoding.Charset with
            | Utf8 -> utf8Mark.Length
            | Utf16Le
            | Utf16Be -> utf16LeMark.Length
            | Latin1 -> 0

    let lineFeed encoding =
        match encoding.Charset with
        | Utf16Le -> [| 0x0Auy; 0x00uy |]
        | Utf16Be -> [| 0x00uy; 0x0Auy |]
        | Utf8
        | Latin1 -> [| 0x0Auy |]

    let private charsetEncoding charset : Encoding =
        match charset with
        | Utf8 -> Encoding.UTF8
        | Utf16Le -> Encoding.Unicode
        | Utf16Be -> Encoding.BigEndianUnicode
        | Latin1 -> Encoding.Latin1

    let decode encoding (bytes: byte array) =
        (charsetEncoding encoding.Charset).GetString bytes

    let decoder encoding = (charsetEncoding encoding.Charset).GetDecoder()

    let maxChars encoding byteCount =
        (charsetEncoding encoding.Charset).GetMaxCharCount byteCount
