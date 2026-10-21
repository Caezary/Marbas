namespace Marbas.Guide

type Target =
    | SameChapter of Anchor
    | OtherChapter of ChapterName * Anchor option

type Span =
    | Text of string
    | Code of string
    | Strong of Phrase
    | Emphasis of Phrase
    | Link of Phrase * Target

and Phrase = Phrase of Span list

type Item = { Phrase: Phrase; Items: Item list }

type Prose =
    | Paragraph of Phrase
    | Bullets of Item list

type Cell = Cell of Phrase

type Row = Row of Cell list

type Table = private { Header: Row; Body: Row list }

module Table =

    let private emptyCell = Cell(Phrase [])

    let private fitted width (Row cells) =
        let padding = List.replicate (max 0 (width - List.length cells)) emptyCell

        Row(List.truncate width cells @ padding)

    let create (Row cells as header) body =
        { Header = header
          Body = body |> List.map (fitted cells.Length) }

    let header table = table.Header

    let body table = table.Body

type HeadingLevel =
    | ChapterTitle
    | Section
    | Subsection

type Heading =
    { Level: HeadingLevel
      Anchor: Anchor
      Title: Phrase }

type AlertKind =
    | Note
    | Warning

type Block =
    | Heading of Heading
    | Prose of Prose
    | Table of Table
    | Alert of AlertKind * Prose list
