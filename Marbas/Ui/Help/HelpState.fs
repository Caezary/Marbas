namespace Marbas.Ui.Help

open System.Drawing
open Marbas.Guide

type HelpPage =
    { Chapter: Chapter
      Width: int
      Lines: LaidLine list
      Top: int
      Link: int option }

type HelpState =
    { Page: HelpPage
      Height: int
      Back: HelpPage list }

module HelpState =

    let private pageOf chapter =
        { Chapter = chapter
          Width = 0
          Lines = []
          Top = 0
          Link = None }

    let start chapter =
        { Page = pageOf chapter
          Height = 1
          Back = [] }

    let private blockSpan (lines: LaidLine list) block =
        let start =
            lines
            |> List.tryFindIndex (fun line -> line.Block = block)
            |> Option.defaultValue 0

        let height = lines |> List.filter (fun line -> line.Block = block) |> List.length

        start, max 1 height

    let private topAfterRelaying (page: HelpPage) (lines: LaidLine list) =
        match List.tryItem page.Top page.Lines with
        | None -> 0
        | Some line ->
            let oldStart, oldHeight = blockSpan page.Lines line.Block
            let newStart, newHeight = blockSpan lines line.Block
            newStart + (page.Top - oldStart) * newHeight / oldHeight

    let private relaid width (page: HelpPage) =
        if page.Width = width then
            page
        else
            let lines = ChapterLayout.lay width page.Chapter.Contents

            { page with
                Width = width
                Lines = lines
                Top = topAfterRelaying page lines }

    let private holds index (line: LaidLine) =
        line.Pieces
        |> List.exists (fun piece -> piece.Link |> Option.exists (fun stop -> stop.Index = index))

    let private rowsOf (page: HelpPage) index =
        page.Lines |> List.indexed |> List.filter (snd >> holds index) |> List.map fst

    let private firstRow page index =
        rowsOf page index |> List.tryHead |> Option.defaultValue 0

    let private lastTop height (page: HelpPage) = max 0 (List.length page.Lines - height)

    let private clampedTop height page top =
        top |> min (lastTop height page) |> max 0

    let private onScreen height (page: HelpPage) row =
        row >= page.Top && row < page.Top + height

    let private visible height page index =
        rowsOf page index |> List.exists (onScreen height page)

    let private keptChoice height (page: HelpPage) =
        match page.Link with
        | Some index when not (visible height page index) -> { page with Link = None }
        | _ -> page

    let private scrolledTo height top (page: HelpPage) =
        { page with
            Top = clampedTop height page top }
        |> keptChoice height

    let private stops (page: HelpPage) =
        page.Lines
        |> List.collect _.Pieces
        |> List.choose _.Link
        |> List.map _.Index
        |> List.distinct

    let private nextStop height (page: HelpPage) =
        match page.Link with
        | Some current -> stops page |> List.tryFind (fun index -> index > current)
        | None ->
            stops page
            |> List.tryFind (fun index -> visible height page index || firstRow page index >= page.Top)

    let private previousStop height (page: HelpPage) =
        match page.Link with
        | Some current -> stops page |> List.filter (fun index -> index < current) |> List.tryLast
        | None ->
            stops page
            |> List.filter (fun index -> visible height page index || firstRow page index < page.Top)
            |> List.tryLast

    let private topShowing height (page: HelpPage) rows =
        match rows with
        | [] -> page.Top
        | _ when List.min rows < page.Top -> List.min rows
        | _ when List.max rows >= page.Top + height -> List.max rows - height + 1
        | _ -> page.Top

    let private chosen height (page: HelpPage) index =
        { page with
            Link = Some index
            Top = topShowing height page (rowsOf page index) |> clampedTop height page }

    let private stepped pick height (page: HelpPage) =
        match pick height page with
        | Some index -> chosen height page index
        | None -> page

    let private fittedPage width height page =
        let page = relaid width page

        scrolledTo height page.Top page

    let private withPage (state: HelpState) page = { state with Page = page }

    let private back (state: HelpState) =
        match state.Back with
        | [] -> state
        | page :: rest ->
            { state with
                Page = fittedPage state.Page.Width state.Height page
                Back = rest }

    let run (command: HelpKeys.Command) (state: HelpState) =
        let page = state.Page
        let height = state.Height

        match command with
        | HelpKeys.LineUp -> scrolledTo height (page.Top - 1) page |> withPage state
        | HelpKeys.LineDown -> scrolledTo height (page.Top + 1) page |> withPage state
        | HelpKeys.PageUp -> scrolledTo height (page.Top - height) page |> withPage state
        | HelpKeys.PageDown -> scrolledTo height (page.Top + height) page |> withPage state
        | HelpKeys.ToStart -> scrolledTo height 0 page |> withPage state
        | HelpKeys.ToEnd -> scrolledTo height (lastTop height page) page |> withPage state
        | HelpKeys.NextLink -> stepped nextStop height page |> withPage state
        | HelpKeys.PreviousLink -> stepped previousStop height page |> withPage state
        | HelpKeys.Back -> back state
        | HelpKeys.Follow
        | HelpKeys.Closed -> state

    let fitted (screen: Size) (state: HelpState) =
        let height = max 1 screen.Height

        { state with
            Page = fittedPage (max 1 screen.Width) height state.Page
            Height = height }

    let private anchorOf target =
        match target with
        | SameChapter anchor -> Some anchor
        | OtherChapter(_, anchor) -> anchor

    let private rowOfAnchor (page: HelpPage) anchor =
        page.Lines |> List.tryFindIndex (fun line -> line.Anchor = Some anchor)

    let private targetOf (page: HelpPage) index =
        page.Lines
        |> List.collect _.Pieces
        |> List.tryPick (fun piece -> piece.Link |> Option.filter (fun stop -> stop.Index = index))
        |> Option.map _.Target

    let private chapterFor read (page: HelpPage) target =
        match target with
        | SameChapter _ -> Ok page.Chapter
        | OtherChapter(name, _) -> read name

    let private opened (state: HelpState) target chapter =
        let page = pageOf chapter |> relaid state.Page.Width

        let top = anchorOf target |> Option.bind (rowOfAnchor page) |> Option.defaultValue 0

        { state with
            Page = scrolledTo state.Height top page
            Back = state.Page :: state.Back }

    let follow (read: ChapterName -> Result<Chapter, GuideFault>) (state: HelpState) =
        match state.Page.Link |> Option.bind (targetOf state.Page) with
        | None -> Ok state
        | Some target -> chapterFor read state.Page target |> Result.map (opened state target)

    let shown (state: HelpState) =
        state.Page.Lines
        |> List.skip (min state.Page.Top (List.length state.Page.Lines))
        |> List.truncate state.Height

    let rec private phraseText (Phrase spans) =
        spans |> List.map spanText |> String.concat ""

    and private spanText span =
        match span with
        | Text text
        | Code text -> text
        | Strong phrase
        | Emphasis phrase
        | Link(phrase, _) -> phraseText phrase

    let private chapterTitle block =
        match block with
        | Heading heading when heading.Level = ChapterTitle -> Some(phraseText heading.Title)
        | _ -> None

    let title (state: HelpState) =
        state.Page.Chapter.Contents
        |> List.tryPick chapterTitle
        |> Option.defaultValue (ChapterName.value state.Page.Chapter.Name)
