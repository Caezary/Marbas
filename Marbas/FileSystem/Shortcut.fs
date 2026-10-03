namespace Marbas.FileSystem

open System
open System.Buffers.Binary
open System.IO.Abstractions
open System.Text
open FsToolkit.ErrorHandling
open Marbas.Domain

type ShortcutTarget =
    | FullPath of FileSystemPath
    | RelativePath of string

module Shortcut =

    [<Literal>]
    let private HeaderSize = 0x4C

    [<Literal>]
    let private FlagsAt = 0x14

    [<Literal>]
    let private HasIdList = 0x01

    [<Literal>]
    let private HasLinkInfo = 0x02

    [<Literal>]
    let private HasName = 0x04

    [<Literal>]
    let private HasRelativePath = 0x08

    [<Literal>]
    let private IsUnicode = 0x80

    [<Literal>]
    let private LocalPathHeld = 0x01

    [<Literal>]
    let private NetworkPathHeld = 0x02

    [<Literal>]
    let private WideOffsetsHeaderSize = 0x24

    [<Literal>]
    let private WideNetworkHeaderSize = 0x14

    [<Literal>]
    let private LongestShortcut = 1048576L

    let private shellLinkClass =
        [| 0x01uy; 0x14uy; 0x02uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy
           0xC0uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x46uy |]

    let isShortcut (name: EntryName) =
        FileExtension.ofName name |> Option.map FileExtension.value = Some "lnk"

    let private fits (bytes: byte array) offset length =
        offset >= 0 && length >= 0 && offset + length <= bytes.Length

    let private int32At (bytes: byte array) offset =
        if fits bytes offset 4 then
            Some(BinaryPrimitives.ReadInt32LittleEndian(ReadOnlySpan(bytes, offset, 4)))
        else
            None

    let private uint16At (bytes: byte array) offset =
        if fits bytes offset 2 then
            Some(int (BinaryPrimitives.ReadUInt16LittleEndian(ReadOnlySpan(bytes, offset, 2))))
        else
            None

    let private holds flag (flags: int) = flags &&& flag = flag

    let private isAscii (bytes: byte array) = bytes |> Array.forall (fun one -> one < 0x80uy)

    let private asciiAt (bytes: byte array) offset =
        let terminator =
            if fits bytes offset 1 then
                Array.IndexOf(bytes, 0uy, offset)
            else
                -1

        match terminator with
        | -1 -> None
        | ending ->
            let text = bytes[offset .. ending - 1]
            if isAscii text then Some(Encoding.ASCII.GetString text) else None

    let rec private wideEnding (bytes: byte array) offset =
        match uint16At bytes offset with
        | None -> None
        | Some 0 -> Some offset
        | Some _ -> wideEnding bytes (offset + 2)

    let private wideAt (bytes: byte array) offset =
        if fits bytes offset 0 then
            wideEnding bytes offset
            |> Option.map (fun ending -> Encoding.Unicode.GetString(bytes, offset, ending - offset))
        else
            None

    let private isShellLink (bytes: byte array) =
        int32At bytes 0 = Some HeaderSize
        && fits bytes 4 shellLinkClass.Length
        && bytes[4 .. 3 + shellLinkClass.Length] = shellLinkClass

    let private localPath (bytes: byte array) start headerSize =
        if headerSize >= WideOffsetsHeaderSize then
            option {
                let! baseAt = int32At bytes (start + 28)
                let! suffixAt = int32At bytes (start + 32)
                let! basePath = wideAt bytes (start + baseAt)
                let! suffix = wideAt bytes (start + suffixAt)
                return basePath + suffix
            }
        else
            option {
                let! baseAt = int32At bytes (start + 16)
                let! suffixAt = int32At bytes (start + 24)
                let! basePath = asciiAt bytes (start + baseAt)
                let! suffix = asciiAt bytes (start + suffixAt)
                return basePath + suffix
            }

    let private shareName (bytes: byte array) network =
        int32At bytes (network + 8)
        |> Option.bind (fun nameAt ->
            if nameAt > WideNetworkHeaderSize then
                int32At bytes (network + 20) |> Option.bind (fun wideNameAt -> wideAt bytes (network + wideNameAt))
            else
                asciiAt bytes (network + nameAt))

    let private underShare (share: string) (suffix: string) =
        if suffix = "" then share else share.TrimEnd('\\') + "\\" + suffix

    let private networkPath (bytes: byte array) start =
        option {
            let! networkAt = int32At bytes (start + 20)
            let! suffixAt = int32At bytes (start + 24)
            let! share = shareName bytes (start + networkAt)
            let! suffix = asciiAt bytes (start + suffixAt)
            return underShare share suffix
        }

    let private infoPath (bytes: byte array) start =
        option {
            let! headerSize = int32At bytes (start + 4)
            let! held = int32At bytes (start + 8)

            let! path =
                if holds LocalPathHeld held then localPath bytes start headerSize
                elif holds NetworkPathHeld held then networkPath bytes start
                else None

            return! if path = "" then None else Some path
        }

    let private infoStart (bytes: byte array) flags =
        if holds HasIdList flags then
            uint16At bytes HeaderSize |> Option.map (fun size -> HeaderSize + 2 + size)
        else
            Some HeaderSize

    let private stringsStart (bytes: byte array) flags start =
        if holds HasLinkInfo flags then int32At bytes start |> Option.map ((+) start) else Some start

    let private countedAt (bytes: byte array) wide offset =
        let width = if wide then 2 else 1

        uint16At bytes offset
        |> Option.filter (fun count -> fits bytes (offset + 2) (count * width))
        |> Option.bind (fun count ->
            let text = bytes[offset + 2 .. offset + 1 + count * width]

            match wide, isAscii text with
            | true, _ -> Some(Encoding.Unicode.GetString text, offset + 2 + count * width)
            | false, true -> Some(Encoding.ASCII.GetString text, offset + 2 + count * width)
            | false, false -> None)

    let private relativePath (bytes: byte array) flags start =
        let wide = holds IsUnicode flags

        let afterName =
            if holds HasName flags then
                countedAt bytes wide start |> Option.map snd
            else
                Some start

        afterName
        |> Option.filter (fun _ -> holds HasRelativePath flags)
        |> Option.bind (countedAt bytes wide)
        |> Option.map fst
        |> Option.filter ((<>) "")

    let private linkTarget (bytes: byte array) =
        option {
            let! flags = int32At bytes FlagsAt
            let! info = infoStart bytes flags

            let held =
                if holds HasLinkInfo flags then infoPath bytes info else None

            let relative () =
                stringsStart bytes flags info
                |> Option.bind (relativePath bytes flags)
                |> Option.map RelativePath

            return!
                held
                |> Option.map (FileSystemPath.create >> FullPath)
                |> Option.orElseWith relative
        }

    let parse (bytes: byte array) =
        if isShellLink bytes then linkTarget bytes else None

    let private bytesOf (fs: IFileSystem) (shortcut: FileSystemPath) =
        let path = FileSystemPath.value shortcut

        try
            if fs.FileInfo.New(path).Length <= LongestShortcut then
                Ok(Some(fs.File.ReadAllBytes path))
            else
                Ok None
        with ex ->
            Error(ContentError.mapException shortcut ex)

    let target (fs: IFileSystem) (shortcut: FileSystemPath) =
        let resolved found =
            match found with
            | FullPath path -> path
            | RelativePath relative -> FileSystemPath.resolvedFrom fs (FileSystemPath.directory fs shortcut) relative

        bytesOf fs shortcut
        |> Result.toOption
        |> Option.flatten
        |> Option.bind parse
        |> Option.map resolved
