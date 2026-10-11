namespace Marbas.FileSystem

open System.Text

type Charset =
    | Utf8
    | Utf16Le
    | Utf16Be
    | Latin1
    | Windows1250
    | Cp852
    | Iso88592
    | Windows1252
    | Cp437

type ByteOrderMark =
    | WithBom
    | WithoutBom

type TextEncoding =
    { Charset: Charset; Bom: ByteOrderMark }

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

    let rec private trailingBetween (bytes: byte array) from until =
        from >= until
        || (isTrailing bytes[from] && trailingBetween bytes (from + 1) until)

    let private isUtf8 (probe: byte array) =
        let mutable index = 0
        let mutable valid = true

        while valid && index < probe.Length do
            match sequenceLength probe[index] with
            | None -> valid <- false
            | Some length when index + length > probe.Length -> index <- probe.Length
            | Some length ->
                if trailingBetween probe (index + 1) (index + length) then
                    index <- index + length
                else
                    valid <- false

        valid

    let private wideCharset (probe: byte array) =
        let pairs = probe.Length / 2

        let rec zerosFrom pair side counted =
            if pair = pairs then
                counted
            elif probe[pair * 2 + side] = 0uy then
                zerosFrom (pair + 1) side (counted + 1)
            else
                zerosFrom (pair + 1) side counted

        let zeros side = zerosFrom 0 side 0
        let leading = zeros 0
        let trailing = zeros 1

        if pairs = 0 then None
        elif leading = 0 && trailing * 2 > pairs then Some Utf16Le
        elif trailing = 0 && leading * 2 > pairs then Some Utf16Be
        else None

    let examine (probe: byte array) =
        let plain charset = { Charset = charset; Bom = WithoutBom }

        if startsWith utf8Mark probe then
            { Charset = Utf8; Bom = WithBom }, Textual
        elif startsWith utf16LeMark probe then
            { Charset = Utf16Le; Bom = WithBom }, Textual
        elif startsWith utf16BeMark probe then
            { Charset = Utf16Be; Bom = WithBom }, Textual
        else
            match wideCharset probe with
            | Some charset -> plain charset, Textual
            | None when Array.contains 0uy probe -> plain Latin1, Binary
            | None -> plain (if isUtf8 probe then Utf8 else Latin1), Textual

    let detect (probe: byte array) = examine probe |> fst

    let charsets =
        [ Utf8
          Utf16Le
          Utf16Be
          Latin1
          Windows1250
          Cp852
          Iso88592
          Windows1252
          Cp437 ]

    let charsetName charset =
        match charset with
        | Utf8 -> "UTF-8"
        | Utf16Le -> "UTF-16 LE"
        | Utf16Be -> "UTF-16 BE"
        | Latin1 -> "Latin-1"
        | Windows1250 -> "Windows-1250"
        | Cp852 -> "CP852"
        | Iso88592 -> "ISO-8859-2"
        | Windows1252 -> "Windows-1252"
        | Cp437 -> "CP437"

    let name encoding = charsetName encoding.Charset

    let unitSize encoding =
        match encoding.Charset with
        | Utf16Le
        | Utf16Be -> 2
        | Utf8
        | Latin1
        | Windows1250
        | Cp852
        | Iso88592
        | Windows1252
        | Cp437 -> 1

    let private markOf charset =
        match charset with
        | Utf8 -> Some utf8Mark
        | Utf16Le -> Some utf16LeMark
        | Utf16Be -> Some utf16BeMark
        | Latin1
        | Windows1250
        | Cp852
        | Iso88592
        | Windows1252
        | Cp437 -> None

    let chosen charset (probe: byte array) =
        let marked = markOf charset |> Option.exists (fun mark -> startsWith mark probe)

        { Charset = charset
          Bom = if marked then WithBom else WithoutBom }

    let preambleLength encoding =
        match encoding.Bom, markOf encoding.Charset with
        | WithBom, Some mark -> mark.Length
        | WithBom, None
        | WithoutBom, _ -> 0

    let lineFeed encoding =
        match encoding.Charset with
        | Utf16Le -> [| 0x0Auy; 0x00uy |]
        | Utf16Be -> [| 0x00uy; 0x0Auy |]
        | Utf8
        | Latin1
        | Windows1250
        | Cp852
        | Iso88592
        | Windows1252
        | Cp437 -> [| 0x0Auy |]

    let private codePage (number: int) =
        lazy (CodePagesEncodingProvider.Instance.GetEncoding number)

    let private windows1250 = codePage 1250

    let private cp852 = codePage 852

    let private iso88592 = codePage 28592

    let private windows1252 = codePage 1252

    let private cp437 = codePage 437

    let private charsetEncoding charset : Encoding =
        match charset with
        | Utf8 -> Encoding.UTF8
        | Utf16Le -> Encoding.Unicode
        | Utf16Be -> Encoding.BigEndianUnicode
        | Latin1 -> Encoding.Latin1
        | Windows1250 -> windows1250.Value
        | Cp852 -> cp852.Value
        | Iso88592 -> iso88592.Value
        | Windows1252 -> windows1252.Value
        | Cp437 -> cp437.Value

    let decode encoding (bytes: byte array) =
        (charsetEncoding encoding.Charset).GetString bytes

    let decoder encoding =
        (charsetEncoding encoding.Charset).GetDecoder()

    let maxChars encoding byteCount =
        (charsetEncoding encoding.Charset).GetMaxCharCount byteCount

    let private utf8LengthAt (bytes: byte array) position =
        sequenceLength bytes[position]
        |> Option.filter (fun length -> position + length <= bytes.Length)
        |> Option.filter (fun length -> trailingBetween bytes (position + 1) (position + length))

    let private unitAt charset (bytes: byte array) position =
        match charset with
        | Utf16Be -> (int bytes[position] <<< 8) ||| int bytes[position + 1]
        | _ -> (int bytes[position + 1] <<< 8) ||| int bytes[position]

    let private isHighSurrogate unit = unit >= 0xD800 && unit <= 0xDBFF

    let private isLowSurrogate unit = unit >= 0xDC00 && unit <= 0xDFFF

    let private utf16LengthAt charset (bytes: byte array) position =
        let unitAt at =
            if at + 2 <= bytes.Length then
                Some(unitAt charset bytes at)
            else
                None

        match unitAt position, unitAt (position + 2) with
        | None, _ -> None
        | Some first, _ when isLowSurrogate first -> None
        | Some first, Some second when isHighSurrogate first && isLowSurrogate second -> Some 4
        | Some first, _ when isHighSurrogate first -> None
        | Some _, _ -> Some 2

    let private lengthWithin encoding (bytes: byte array) position =
        match encoding.Charset with
        | Utf8 -> utf8LengthAt bytes position
        | Utf16Le
        | Utf16Be -> utf16LengthAt encoding.Charset bytes position
        | Latin1
        | Windows1250
        | Cp852
        | Iso88592
        | Windows1252
        | Cp437 -> Some 1

    let characterLengthAt encoding (bytes: byte array) position =
        if position < 0 || position >= bytes.Length then
            None
        else
            lengthWithin encoding bytes position
