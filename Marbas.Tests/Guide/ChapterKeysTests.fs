module Marbas.Tests.Guide.ChapterKeysTests

open Xunit
open Marbas.Guide
open Marbas.Tests

let private keyTable rows =
    "# Panels\n\n| Key | Alternative | Action |\n|-----|-------------|--------|\n"
    + rows

let private chapterOf (name, text) : Chapter =
    { Name = ChapterName.tryCreate name |> Expect.some
      Contents = ChapterMarkdown.parse text |> Expect.ok }

let private namedIn chapters =
    chapters |> List.map chapterOf |> ChapterKeys.named |> Set.map KeyName.value

[<Fact>]
let ``the key and the alternative of a row are named`` () =
    let chapters =
        [ "02-panels.md", keyTable "| `Alt+H` | `Ctrl+H` | Show the hidden entries |\n" ]

    let named = namedIn chapters

    Assert.Equal<Set<string>>(set [ "Alt+H"; "Ctrl+H" ], named)

[<Fact>]
let ``a key written in the action is not named`` () =
    let chapters =
        [ "02-panels.md", keyTable "| `F3` | | Measure, then `Esc` stops |\n" ]

    let named = namedIn chapters

    Assert.Equal<Set<string>>(set [ "F3" ], named)

[<Fact>]
let ``a table under other headers names no key`` () =
    let chapters =
        [ "16-platforms.md",
          "# Platforms\n\n| Key | Kept by | Press instead |\n|-----|---------|---------------|\n| `F11` | full screen | `Alt+T` |\n" ]

    let named = namedIn chapters

    Assert.Empty named

[<Fact>]
let ``every code of a cell is a key of its own`` () =
    let chapters =
        [ "11-viewer.md", keyTable "| `Esc` | `F3` `F10` | Close the viewer |\n" ]

    let named = namedIn chapters

    Assert.Equal<Set<string>>(set [ "Esc"; "F3"; "F10" ], named)

[<Fact>]
let ``a run names every key from its first to its last`` () =
    let chapters =
        [ "06-directories.md", keyTable "| `Alt+0` … `Alt+3` | | Go to the favourite |\n" ]

    let named = namedIn chapters

    Assert.Equal<Set<string>>(set [ "Alt+0"; "Alt+1"; "Alt+2"; "Alt+3" ], named)

[<Fact>]
let ``a run of bare letters names every letter between`` () =
    let chapters =
        [ "10-running-files.md", keyTable "| `A` … `D` | `0` … `2` | Run the entry |\n" ]

    let named = namedIn chapters

    Assert.Equal<Set<string>>(set [ "A"; "B"; "C"; "D"; "0"; "1"; "2" ], named)

[<Fact>]
let ``the ends of a run under different modifiers are two keys`` () =
    let chapters =
        [ "06-directories.md", keyTable "| `Alt+0` … `Ctrl+9` | | Go to the favourite |\n" ]

    let named = namedIn chapters

    Assert.Equal<Set<string>>(set [ "Alt+0"; "Ctrl+9" ], named)

[<Fact>]
let ``the keys of every chapter are named together`` () =
    let chapters =
        [ "02-panels.md", keyTable "| `Ctrl+R` | | Read the panel again |\n"
          "04-tabs.md", keyTable "| `F11` | `Alt+T` | List the tabs |\n" ]

    let named = namedIn chapters

    Assert.Equal<Set<string>>(set [ "Ctrl+R"; "F11"; "Alt+T" ], named)
