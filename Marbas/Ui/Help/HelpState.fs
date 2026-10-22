namespace Marbas.Ui.Help

open System.Drawing
open Marbas.Domain
open Marbas.Guide
open Marbas.Viewing
open Marbas.Ui.Viewer

type HelpPage =
    { Chapter: Chapter
      Width: int
      Lines: LaidLine list
      Top: int
      Link: int option
      Found: FoundSpan option }

type HelpState =
    { Page: HelpPage
      Height: int
      Back: HelpPage list
      LastQuery: WindowSearch.Query option }

type private SearchPlace = { Row: int; Column: int }

module HelpState =

    let private pageOf chapter =
        { Chapter = chapter
          Width = 0
          Lines = []
          Top = 0
          Link = None
          Found = None }

    let start chapter =
        { Page = pageOf chapter
          Height = 1
          Back = []
          LastQuery = None }

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
                Top = topAfterRelaying page lines
                Found = None }

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
        | HelpKeys.SearchAsked
        | HelpKeys.SearchRepeated
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

    let private textOf (line: LaidLine) =
        line.Pieces |> List.map _.Text |> String.concat ""

    let private screenPlace height (page: HelpPage) direction : SearchPlace =
        match direction with
        | Forwards -> { Row = page.Top; Column = 0 }
        | Backwards ->
            let bottom = min (page.Top + height) (List.length page.Lines) - 1

            { Row = bottom
              Column =
                page.Lines
                |> List.tryItem bottom
                |> Option.map (textOf >> String.length)
                |> Option.defaultValue 0 }

    let private placeAfterMatch height (page: HelpPage) direction : SearchPlace =
        match page.Found, direction with
        | Some span, Forwards -> { Row = span.Line; Column = span.Until }
        | Some span, Backwards -> { Row = span.Line; Column = span.From }
        | None, _ -> screenPlace height page direction

    let private rowsFrom direction (place: SearchPlace) (page: HelpPage) =
        let texts = page.Lines |> List.map textOf |> List.indexed

        match direction with
        | Forwards -> texts |> List.filter (fun (row, _) -> row >= place.Row)
        | Backwards -> texts |> List.filter (fun (row, _) -> row <= place.Row) |> List.rev

    let private columnFrom direction (place: SearchPlace) (row, text: string) =
        match direction, row = place.Row with
        | _, true -> place.Column
        | Forwards, false -> 0
        | Backwards, false -> text.Length

    let private spanFrom (query: WindowSearch.Query) (place: SearchPlace) (page: HelpPage) =
        let occurrence = WindowSearch.occurrenceFrom query

        rowsFrom query.Direction place page
        |> List.tryPick (fun (row, text) ->
            occurrence text (columnFrom query.Direction place (row, text))
            |> Option.map (fun (column, length) ->
                ({ Line = row
                   From = column
                   Until = column + length }
                : FoundSpan)))

    let private showing height (span: FoundSpan) (page: HelpPage) =
        let marked = { page with Found = Some span }

        if onScreen height page span.Line then
            marked
        else
            scrolledTo height span.Line marked

    let private findFrom placeOf (query: WindowSearch.Query) (state: HelpState) =
        let page = state.Page

        spanFrom query (placeOf state.Height page query.Direction) page
        |> Option.map (fun span -> showing state.Height span page |> withPage state)

    let searchedFor (query: WindowSearch.Query) (state: HelpState) = { state with LastQuery = Some query }

    let findFromScreen query state = findFrom screenPlace query state

    let findAfterMatch query state = findFrom placeAfterMatch query state

    let foundOnScreen (state: HelpState) =
        state.Page.Found
        |> Option.filter (fun span -> onScreen state.Height state.Page span.Line)
        |> Option.map (fun span -> span.Line - state.Page.Top, span.From, span.Until - span.From)

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
