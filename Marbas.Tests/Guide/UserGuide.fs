module Marbas.Tests.Guide.UserGuide

open System
open System.IO
open Marbas.Guide
open Marbas.Tests

let private prefix = "user-guide/"

let names =
    [ "01-getting-started.md"
      "02-panels.md"
      "03-marking.md"
      "04-tabs.md"
      "05-file-operations.md"
      "06-directories-and-favourites.md"
      "07-archives.md"
      "08-clipboard.md"
      "09-finding-files.md"
      "10-running-files.md"
      "11-viewer.md"
      "12-settings.md"
      "13-history.md"
      "14-command-line-and-environment.md"
      "15-files-marbas-keeps.md"
      "16-platforms.md"
      "README.md" ]

let embeddedNames () =
    typeof<Chapter>.Assembly.GetManifestResourceNames()
    |> Array.filter _.StartsWith(prefix, StringComparison.Ordinal)
    |> Array.map _.Substring(prefix.Length)
    |> Array.sort
    |> List.ofArray

let textOf name =
    use stream =
        typeof<Chapter>.Assembly.GetManifestResourceStream(prefix + name)
        |> Expect.notNull

    use reader = new StreamReader(stream)
    reader.ReadToEnd()

let private chapterOf name : Chapter =
    { Name = ChapterName.tryCreate name |> Expect.some
      Contents = ChapterMarkdown.parse (textOf name) |> Expect.ok }

let chapters () = names |> List.map chapterOf
