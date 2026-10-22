module Marbas.Tests.Guide.UserGuide

open Marbas.Guide
open Marbas.Tests

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

let chapterOf name =
    ChapterName.tryCreate name |> Expect.some |> EmbeddedGuide.read |> Expect.ok

let chapters () = names |> List.map chapterOf
