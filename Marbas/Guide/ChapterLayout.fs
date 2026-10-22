module Marbas.Guide.ChapterLayout

type private Word = Piece list

type private Context =
    { Look: Look
      Stress: Set<Stress>
      Link: LinkStop option }

let private body =
    { Look = Plain
      Stress = Set.empty
      Link = None }

let private plain text =
    { Text = text
      Look = Plain
      Stress = Set.empty
      Link = None }

let private blank count = plain (String.replicate count " ")

let private ruling text = { plain text with Look = Ruling }

let private widthOf (pieces: Piece list) = pieces |> List.sumBy _.Text.Length

let private pieceOf (context: Context) look text =
    { Text = text
      Look = look
      Stress = context.Stress
      Link = context.Link }

let rec private fragmentsOf context next (Phrase spans) =
    let fragments, next = spans |> List.mapFold (spanFragments context) next

    List.concat fragments, next

and private spanFragments context next span =
    match span with
    | Text text -> [ pieceOf context context.Look text ], next
    | Code text -> [ pieceOf context Literal text ], next
    | Strong phrase ->
        fragmentsOf
            { context with
                Stress = Set.add Bold context.Stress }
            next
            phrase
    | Emphasis phrase ->
        fragmentsOf
            { context with
                Stress = Set.add Italic context.Stress }
            next
            phrase
    | Link(phrase, target) ->
        fragmentsOf
            { context with
                Link = Some { Index = next; Target = target } }
            (next + 1)
            phrase

let private appended (part: string) (fragment: Piece) (current: Piece list) =
    if part = "" then
        current
    else
        { fragment with Text = part } :: current

let private closed (finished: Word list, current: Piece list) =
    match current with
    | [] -> finished
    | _ -> List.rev current :: finished

let private startedAfterSpace fragment (finished, current) part =
    closed (finished, current), appended part fragment []

let private splitAtSpaces (finished, current) (fragment: Piece) =
    match fragment.Look, List.ofArray (fragment.Text.Split ' ') with
    | Literal, _ -> finished, appended fragment.Text fragment current
    | _, first :: rest ->
        rest
        |> List.fold (startedAfterSpace fragment) (finished, appended first fragment current)
    | _, [] -> finished, current

let private wordsOf fragments : Word list =
    fragments |> List.fold splitAtSpaces ([], []) |> closed |> List.rev

let rec private cut count (word: Word) =
    match word with
    | [] -> [], []
    | piece :: rest when piece.Text.Length <= count ->
        let front, back = cut (count - piece.Text.Length) rest
        piece :: front, back
    | piece :: rest ->
        let front =
            if count > 0 then
                [ { piece with
                      Text = piece.Text.Substring(0, count) } ]
            else
                []

        front,
        { piece with
            Text = piece.Text.Substring count }
        :: rest

let rec private chunksOf width (word: Word) =
    if widthOf word <= width then
        [ word ]
    else
        let front, back = cut width word
        front :: chunksOf width back

let private space (before: Word) (after: Word) =
    let left = List.last before
    let right = List.head after

    let linked =
        { plain " " with
            Link = if left.Link = right.Link then left.Link else None }

    if left.Look = right.Look && left.Stress = right.Stress then
        { linked with
            Look = left.Look
            Stress = left.Stress }
    else
        linked

let private joined (words: Word list) =
    match words with
    | [] -> []
    | first :: _ ->
        first
        @ (List.pairwise words
           |> List.collect (fun (before, after) -> space before after :: after))

let private filled width (lines, current: Word list, used) (word: Word) =
    let size = widthOf word

    match current with
    | [] -> lines, [ word ], size
    | _ when used + 1 + size <= width -> lines, current @ [ word ], used + 1 + size
    | _ -> current :: lines, [ word ], size

let private wrap width (words: Word list) =
    let width = max 1 width

    let lines, current, _ =
        words |> List.collect (chunksOf width) |> List.fold (filled width) ([], [], 0)

    (if List.isEmpty current then lines else current :: lines)
    |> List.rev
    |> List.map joined

let private phraseLines context width next phrase =
    let fragments, next = fragmentsOf context next phrase

    wrap width (wordsOf fragments), next

let private unanchored (lines: Piece list list) =
    lines
    |> List.map (fun pieces ->
        { Pieces = pieces
          Anchor = None
          Block = 0 })

let private blankLine =
    { Pieces = []
      Anchor = None
      Block = 0 }

let private parted (groups: LaidLine list list) =
    groups
    |> List.mapi (fun index lines -> if index = 0 then lines else blankLine :: lines)
    |> List.concat

let private underline level width =
    match level with
    | ChapterTitle ->
        [ [ { plain (String.replicate width "═") with
                Look = Title level } ] ]
    | Section ->
        [ [ { plain (String.replicate width "─") with
                Look = Title level } ] ]
    | Subsection -> []

let private headingLines width next (heading: Heading) =
    let title, next =
        phraseLines { body with Look = Title heading.Level } width next heading.Title

    let title = if List.isEmpty title then [ [] ] else title
    let rule = underline heading.Level (title |> List.map widthOf |> List.max)

    let anchored =
        title
        |> List.mapi (fun index pieces ->
            { Pieces = pieces
              Anchor = if index = 0 then Some heading.Anchor else None
              Block = 0 })

    anchored @ unanchored rule, next

let private bulletOf depth = String.replicate (2 * depth) " " + "• "

let private prefixed depth (lines: Piece list list) =
    match lines with
    | [] -> [ [ plain (bulletOf depth) ] ]
    | first :: rest ->
        (plain (bulletOf depth) :: first)
        :: (rest |> List.map (fun line -> blank (2 * depth + 2) :: line))

let rec private itemLines width depth next (item: Item) =
    let own, next = phraseLines body (width - 2 * depth - 2) next item.Phrase
    let nested, next = item.Items |> List.mapFold (itemLines width (depth + 1)) next

    prefixed depth own @ List.concat nested, next

let private proseLines width next prose =
    match prose with
    | Paragraph phrase -> phraseLines body width next phrase
    | Bullets items ->
        let lines, next = items |> List.mapFold (itemLines width 0) next
        List.concat lines, next

let private cellWords context next (Cell phrase) =
    let fragments, next = fragmentsOf context next phrase

    wordsOf fragments, next

let private rowWords context next (Row cells) =
    cells |> List.mapFold (cellWords context) next

let private naturalWidth (words: Word list) =
    match words with
    | [] -> 0
    | _ -> (words |> List.sumBy widthOf) + List.length words - 1

let private longestWord (words: Word list) =
    words |> List.map widthOf |> List.fold max 0

let private holdsCode (word: Word) =
    word |> List.exists (fun piece -> piece.Look = Literal)

let private longestCode (words: Word list) =
    words |> List.filter holdsCode |> longestWord

let private columnsBy measure (rows: Word list list list) =
    rows |> List.transpose |> List.map (List.map measure >> List.fold max 0)

let private widestAbove (floors: int list) (widths: int list) =
    List.zip widths floors
    |> List.indexed
    |> List.filter (fun (_, (width, floor)) -> width > floor)
    |> List.sortBy (fun (index, (width, _)) -> width, index)
    |> List.tryLast
    |> Option.map fst

let rec private narrowed floors available (widths: int list) =
    match widestAbove floors widths with
    | Some column when List.sum widths > available ->
        widths
        |> List.mapi (fun index width -> if index = column then width - 1 else width)
        |> narrowed floors available
    | _ -> widths

let private separator = " │ "

let private padded width (pieces: Piece list) =
    match width - widthOf pieces with
    | missing when missing > 0 -> pieces @ [ blank missing ]
    | _ -> pieces

let private cellLine line width (lines: Piece list list) =
    List.tryItem line lines |> Option.defaultValue [] |> padded width

let private rowLines (widths: int list) (cells: Word list list) =
    let wrapped = List.map2 wrap widths cells
    let height = wrapped |> List.map List.length |> List.fold max 1

    [ for line in 0 .. height - 1 ->
          List.map2 (cellLine line) widths wrapped
          |> List.reduce (fun left right -> left @ [ ruling separator ] @ right) ]

let private ruleOf (widths: int list) =
    [ ruling (
          widths
          |> List.map (fun width -> String.replicate width "─")
          |> String.concat "─┼─"
      ) ]

let private tableLines width next table =
    let header, next =
        rowWords { body with Stress = set [ Bold ] } next (Table.header table)

    let rows, next = Table.body table |> List.mapFold (rowWords body) next
    let all = header :: rows
    let available = width - separator.Length * (List.length header - 1)

    let floors =
        [ columnsBy longestWord all
          columnsBy longestCode all
          List.replicate header.Length 1 ]

    let widths =
        floors
        |> List.fold (fun widths floor -> narrowed floor available widths) (columnsBy naturalWidth all)

    rowLines widths header @ [ ruleOf widths ] @ List.collect (rowLines widths) rows, next

let private labelOf kind =
    match kind with
    | Note -> "Note"
    | Warning -> "Warning"

let private alertLines width next kind proses =
    let inner = max 1 (width - 4)
    let frame text = { plain text with Look = Framing kind }
    let label = labelOf kind
    let groups, next = proses |> List.mapFold (proseLines inner) next

    let content =
        groups
        |> List.map unanchored
        |> parted
        |> List.map (fun line -> frame "│ " :: padded inner line.Pieces @ [ frame " │" ])

    let top =
        frame (
            "┌ "
            + label
            + " "
            + String.replicate (max 0 (width - label.Length - 4)) "─"
            + "┐"
        )

    let bottom = frame ("└" + String.replicate (max 0 (width - 2)) "─" + "┘")

    [ top ] :: content @ [ [ bottom ] ], next

let private withoutAnchors (lines, next) = unanchored lines, next

let private blockLines width next block =
    match block with
    | Heading heading -> headingLines width next heading
    | Prose prose -> proseLines width next prose |> withoutAnchors
    | Table table -> tableLines width next table |> withoutAnchors
    | Alert(kind, proses) -> alertLines width next kind proses |> withoutAnchors

let private sameStyle (left: Piece) (right: Piece) =
    left.Look = right.Look && left.Stress = right.Stress && left.Link = right.Link

let private mergedInto earlier (piece: Piece) =
    match earlier with
    | last :: rest when sameStyle last piece ->
        { last with
            Text = last.Text + piece.Text }
        :: rest
    | _ -> piece :: earlier

let private merged (pieces: Piece list) =
    pieces |> List.fold mergedInto [] |> List.rev

let private laidBlock width next (index, block) =
    let lines, next = blockLines width next block

    lines |> List.map (fun line -> { line with Block = index }), next

let private partedBlock index (lines: LaidLine list) =
    if index = 0 then
        lines
    else
        { blankLine with Block = index } :: lines

let lay width (blocks: Block list) =
    blocks
    |> List.indexed
    |> List.mapFold (laidBlock width) 0
    |> fst
    |> List.mapi partedBlock
    |> List.concat
    |> List.map (fun line ->
        { line with
            Pieces = merged line.Pieces })
