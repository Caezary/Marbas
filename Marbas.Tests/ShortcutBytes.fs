module Marbas.Tests.ShortcutBytes

open System
open System.Text

[<Literal>]
let HasIdList = 0x01

[<Literal>]
let HasLinkInfo = 0x02

[<Literal>]
let HasName = 0x04

[<Literal>]
let HasRelativePath = 0x08

[<Literal>]
let IsUnicode = 0x80

[<Literal>]
let ShortInfoHeader = 0x1C

[<Literal>]
let LongInfoHeader = 0x24

[<Literal>]
let NetworkHeader = 0x14

let private shellLinkClass =
    [| 0x01uy; 0x14uy; 0x02uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy
       0xC0uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x46uy |]

let int32s (values: int list) =
    values |> List.map BitConverter.GetBytes |> Array.concat

let private header (flags: int) =
    Array.concat [ int32s [ 0x4C ]; shellLinkClass; int32s [ flags ]; Array.zeroCreate 52 ]

let ansi (text: string) =
    Array.append (Encoding.ASCII.GetBytes text) [| 0uy |]

let latin (text: string) =
    Array.append (Encoding.Latin1.GetBytes text) [| 0uy |]

let wide (text: string) =
    Array.append (Encoding.Unicode.GetBytes text) [| 0uy; 0uy |]

let localInfo (basePath: byte array) (suffix: byte array) =
    let suffixAt = ShortInfoHeader + basePath.Length
    let size = suffixAt + suffix.Length

    Array.concat [ int32s [ size; ShortInfoHeader; 1; 0; ShortInfoHeader; 0; suffixAt ]; basePath; suffix ]

let counted (text: string) =
    Array.append (BitConverter.GetBytes(uint16 text.Length)) (Encoding.Unicode.GetBytes text)

let shortcut flags (parts: byte array list) = Array.concat (header flags :: parts)

let toFullPath (target: string) =
    shortcut HasLinkInfo [ localInfo (ansi target) (ansi "") ]

let toRelativePath (relative: string) =
    shortcut (HasRelativePath ||| IsUnicode) [ counted relative ]
