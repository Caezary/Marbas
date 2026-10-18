module Marbas.Tests.Programs.HandlerTests

open Xunit
open Marbas.Domain
open Marbas.Programs
open Marbas.Tests

let private program raw = ProgramPath.tryCreate raw

let private handler name raw =
    Handler.tryCreate name (program raw) |> Expect.some

[<Fact>]
let ``a handler carries the name it shows and the program behind it`` () =
    let known = handler "Firefox" "/usr/bin/firefox"

    Assert.Equal("Firefox", known.Name)
    Assert.Equal(program "/usr/bin/firefox", Some known.Program)

[<Theory>]
[<InlineData("/usr/bin/firefox", "firefox")>]
[<InlineData("C:\\Program Files\\App\\app.exe", "app.exe")>]
let ``a handler with no name of its own is named by its program file`` raw expected =
    let known = handler "   " raw

    Assert.Equal(expected, known.Name)

[<Fact>]
let ``a handler with no program at all is no handler`` () =
    Assert.Equal(None, Handler.tryCreate "Nothing" None)

[<Fact>]
let ``one program listed twice keeps the first name it was given`` () =
    let kept =
        Handler.distinct
            [ handler "Firefox" "/usr/bin/firefox"
              handler "Falkon" "/usr/bin/falkon"
              handler "Firefox Web Browser" "/usr/bin/firefox" ]

    Assert.Equal<string list>([ "Firefox"; "Falkon" ], kept |> List.map _.Name)
