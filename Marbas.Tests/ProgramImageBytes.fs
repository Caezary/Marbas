module Marbas.Tests.ProgramImageBytes

open System

[<Literal>]
let HeaderAt = 0x80

[<Literal>]
let WindowsGui = 2us

[<Literal>]
let WindowsConsole = 3us

let imageWith (headerAt: int) (subsystem: uint16) =
    let image = Array.zeroCreate<byte> 0x200
    "MZ"B.CopyTo(image, 0)
    BitConverter.GetBytes(headerAt).CopyTo(image, 0x3C)
    "PE\000\000"B.CopyTo(image, HeaderAt)
    BitConverter.GetBytes(subsystem).CopyTo(image, HeaderAt + 24 + 68)
    image

let windowImage = imageWith HeaderAt WindowsGui

let consoleImage = imageWith HeaderAt WindowsConsole
