module Marbas.Tests.Programs.DesktopEntryTests

open Xunit
open Marbas.Domain
open Marbas.Programs
open Marbas.Tests

let private firefox =
    [ "[Desktop Entry]"
      "Version=1.0"
      "Type=Application"
      "Name=Firefox Web Browser"
      "Name[pl]=Przeglądarka Firefox"
      "Exec=/usr/bin/firefox %u"
      "Terminal=false" ]

let private parsed lines = DesktopEntry.parse lines

[<Fact>]
let ``an entry answers with the name it shows and the program it runs`` () =
    let handler = parsed firefox |> Expect.some

    Assert.Equal("Firefox Web Browser", handler.Name)
    Assert.Equal(ProgramPath.tryCreate "/usr/bin/firefox", Some handler.Program)

[<Fact>]
let ``a localised name is not the name`` () =
    let handler = parsed firefox |> Expect.some

    Assert.Equal("Firefox Web Browser", handler.Name)

[<Fact>]
let ``an entry with no name of its own falls back to the program file`` () =
    let handler =
        parsed [ "[Desktop Entry]"; "Type=Application"; "Exec=/usr/bin/gedit %U" ]
        |> Expect.some

    Assert.Equal("gedit", handler.Name)

[<Theory>]
[<InlineData("NoDisplay")>]
[<InlineData("Hidden")>]
let ``an entry the desktop conceals is no handler`` key =
    let lines =
        [ "[Desktop Entry]"
          "Type=Application"
          "Name=Hidden Helper"
          "Exec=/usr/bin/helper"
          $"{key}=true" ]

    Assert.Equal(None, parsed lines)

[<Fact>]
let ``an entry that is not an application is no handler`` () =
    let lines =
        [ "[Desktop Entry]"; "Type=Link"; "Name=Somewhere"; "URL=https://example.org" ]

    Assert.Equal(None, parsed lines)

[<Fact>]
let ``an entry with nothing to run is no handler`` () =
    let lines = [ "[Desktop Entry]"; "Type=Application"; "Name=Empty" ]

    Assert.Equal(None, parsed lines)

[<Fact>]
let ``keys of another group are not read`` () =
    let lines =
        [ "[Desktop Entry]"
          "Type=Application"
          "Name=Reader"
          "Exec=/usr/bin/reader %f"
          "[Desktop Action new-window]"
          "Name=New Window"
          "Exec=/usr/bin/reader --new-window" ]

    let handler = parsed lines |> Expect.some

    Assert.Equal("Reader", handler.Name)
    Assert.Equal(ProgramPath.tryCreate "/usr/bin/reader", Some handler.Program)

[<Fact>]
let ``text that is no desktop entry at all is no handler`` () =
    Assert.Equal(None, parsed [ "Exec=/usr/bin/firefox"; "Name=Firefox" ])
