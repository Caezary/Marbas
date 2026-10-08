module Marbas.Tests.Domain.ClipboardTextTests

open Xunit
open Marbas.Domain
open Marbas.Tests

[<Literal>]
let private WindowsBreak = "\r\n"

[<Fact>]
let ``no lines are no text`` () =
    let text = ClipboardText.tryCreate []

    Assert.Equal(None, text)

[<Fact>]
let ``the lines are kept in their order`` () =
    let text = ClipboardText.tryCreate [ "b.txt"; "a.txt" ]

    Assert.Equal(Some [ "b.txt"; "a.txt" ], text |> Option.map ClipboardText.value)

[<Fact>]
let ``a single line is spelled without a line break after it`` () =
    let text = ClipboardText.tryCreate [ "notes.txt" ] |> Expect.some

    let spelled = ClipboardText.joined WindowsBreak text

    Assert.Equal("notes.txt", spelled)

[<Fact>]
let ``several lines are joined by the line break and not ended by one`` () =
    let text =
        ClipboardText.tryCreate [ "docs"; "notes.txt"; "readme.txt" ] |> Expect.some

    let spelled = ClipboardText.joined WindowsBreak text

    Assert.Equal("docs\r\nnotes.txt\r\nreadme.txt", spelled)
