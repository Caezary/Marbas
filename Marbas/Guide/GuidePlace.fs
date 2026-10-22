namespace Marbas.Guide

type GuidePlace =
    { Chapter: ChapterName
      Heading: Anchor option }

module GuidePlace =

    let contents =
        { Chapter = ChapterName.contents
          Heading = None }

    let private at raw heading =
        match ChapterName.tryCreate raw with
        | Some chapter ->
            { Chapter = chapter
              Heading = heading |> Option.map Anchor.create }
        | None -> contents

    let gettingStarted = at "01-getting-started.md"
    let panels = at "02-panels.md"
    let marking = at "03-marking.md"
    let tabs = at "04-tabs.md"
    let fileOperations = at "05-file-operations.md"
    let directories = at "06-directories-and-favourites.md"
    let archives = at "07-archives.md"
    let clipboard = at "08-clipboard.md"
    let finding = at "09-finding-files.md"
    let running = at "10-running-files.md"
    let viewing = at "11-viewer.md"
    let settings = at "12-settings.md"
    let history = at "13-history.md"
