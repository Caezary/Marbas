namespace Marbas.Ui.Viewer

type ShownRow = { Line: int; From: int; Text: string }

type FoundSpan = { Line: int; From: int; Until: int }

module FoundSpan =

    let private shownWidth (raw: string) length =
        (PrintableText.describe (raw.Substring(0, min length raw.Length))).Length

    let ofMatch line (raw: string) column length : FoundSpan =
        { Line = line
          From = shownWidth raw column
          Until = shownWidth raw (column + length) }

    let onRow (row: ShownRow) (span: FoundSpan) =
        let start = max span.From row.From
        let stop = min span.Until (row.From + row.Text.Length)

        if row.Line = span.Line && start < stop then
            Some(start - row.From, stop - start)
        else
            None

    let startsOn (row: ShownRow) (span: FoundSpan) =
        row.Line = span.Line
        && row.From <= span.From
        && span.From < row.From + row.Text.Length

    let leftShowing left width (span: FoundSpan) =
        if span.From < left then
            span.From
        elif span.Until > left + width then
            min span.From (span.Until - width)
        else
            left
