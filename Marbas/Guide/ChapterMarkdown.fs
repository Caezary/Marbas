module Marbas.Guide.ChapterMarkdown

open System
open FsToolkit.ErrorHandling
open Markdig
open Markdig.Extensions
open Markdig.Extensions.Alerts
open Markdig.Extensions.AutoIdentifiers
open Markdig.Renderers.Html
open Markdig.Syntax
open Markdig.Syntax.Inlines

let private pipeline =
    MarkdownPipelineBuilder().UsePipeTables().UseAlertBlocks().UseAutoIdentifiers(AutoIdentifierOptions.GitHub).Build()

let private refused (node: MarkdownObject) construct =
    Error
        { Line = node.Line + 1
          Construct = construct }

let private unrecognised (node: MarkdownObject) =
    refused node (Unrecognised(node.GetType().Name))

let private joined spans =
    List.foldBack
        (fun span after ->
            match span, after with
            | Text first, Text second :: rest -> Text(first + second) :: rest
            | _ -> span :: after)
        spans
        []

let private anchorAfter (hash: string) =
    if hash.Length > 0 then Some(Anchor.create hash) else None

let private targetOf (url: string) =
    match url.IndexOf '#' with
    | 0 -> anchorAfter (url.Substring 1) |> Option.map SameChapter
    | -1 ->
        ChapterName.tryCreate url
        |> Option.map (fun chapter -> OtherChapter(chapter, None))
    | at ->
        option {
            let! chapter = ChapterName.tryCreate (url.Substring(0, at))
            let! anchor = anchorAfter (url.Substring(at + 1))
            return OtherChapter(chapter, Some anchor)
        }

let rec private phraseOf (container: ContainerInline | null) =
    match container with
    | null -> Ok(Phrase [])
    | container ->
        container
        |> Seq.toList
        |> List.traverseResultM spansOf
        |> Result.map (List.concat >> joined >> Phrase)

and private spansOf (node: Inline) =
    match node with
    | :? LiteralInline as literal -> Ok [ Text(literal.Content.ToString()) ]
    | :? CodeInline as code -> Ok [ Code code.Content ]
    | :? HtmlEntityInline as entity -> Ok [ Text(entity.Transcoded.ToString()) ]
    | :? LineBreakInline as lineBreak when lineBreak.IsHard -> refused node HardLineBreak
    | :? LineBreakInline -> Ok [ Text " " ]
    | :? EmphasisInline as emphasis -> emphasisOf emphasis
    | :? LinkInline as link when link.IsImage -> refused node Image
    | :? LinkInline as link -> linkOf link
    | :? AutolinkInline -> refused node LinkOutsideTheGuide
    | :? HtmlInline -> refused node Html
    | _ -> unrecognised node

and private emphasisOf (emphasis: EmphasisInline) =
    match emphasis.DelimiterCount with
    | 1 -> phraseOf emphasis |> Result.map (fun phrase -> [ Emphasis phrase ])
    | 2 -> phraseOf emphasis |> Result.map (fun phrase -> [ Strong phrase ])
    | _ -> unrecognised emphasis

and private linkOf (link: LinkInline) =
    match link.Url |> Option.ofObj |> Option.bind targetOf with
    | Some target -> phraseOf link |> Result.map (fun phrase -> [ Link(phrase, target) ])
    | None -> refused link LinkOutsideTheGuide

let private leafPhrase (leaf: LeafBlock) = phraseOf leaf.Inline

let private childrenOf (container: ContainerBlock) = Seq.toList container

let rec private itemsOf (list: ListBlock) =
    if list.IsOrdered then
        refused list OrderedList
    else
        childrenOf list |> List.traverseResultM itemOf

and private itemOf (block: Block) =
    match block with
    | :? ListItemBlock as item -> itemContentOf item
    | _ -> unrecognised block

and private itemContentOf (item: ListItemBlock) =
    match childrenOf item with
    | [ :? ParagraphBlock as paragraph ] ->
        leafPhrase paragraph
        |> Result.map (fun phrase -> { Phrase = phrase; Items = [] })
    | [ :? ParagraphBlock as paragraph; :? ListBlock as nested ] ->
        result {
            let! phrase = leafPhrase paragraph
            let! items = itemsOf nested
            return { Phrase = phrase; Items = items }
        }
    | _ -> refused item ItemContent

let private levelOf (heading: HeadingBlock) =
    match heading.Level with
    | 1 -> Some ChapterTitle
    | 2 -> Some Section
    | 3 -> Some Subsection
    | _ -> None

let private headingOf (heading: HeadingBlock) =
    match levelOf heading, Option.ofObj (heading.GetAttributes().Id) with
    | None, _ -> refused heading DeepHeading
    | Some _, None -> unrecognised heading
    | Some level, Some anchor ->
        leafPhrase heading
        |> Result.map (fun title ->
            Heading
                { Level = level
                  Anchor = Anchor.create anchor
                  Title = title })

let private cellContentOf (cell: Tables.TableCell) =
    match childrenOf cell with
    | [] -> Ok(Cell(Phrase []))
    | [ :? ParagraphBlock as paragraph ] -> leafPhrase paragraph |> Result.map Cell
    | _ -> unrecognised cell

let private cellOf (block: Block) =
    match block with
    | :? Tables.TableCell as cell -> cellContentOf cell
    | _ -> unrecognised block

let private rowOf (block: Block) =
    match block with
    | :? Tables.TableRow as row -> childrenOf row |> List.traverseResultM cellOf |> Result.map Row
    | _ -> unrecognised block

let private isAligned (table: Tables.Table) =
    table.ColumnDefinitions |> Seq.exists _.Alignment.HasValue

let private tableOf (table: Tables.Table) =
    let columns = table.ColumnDefinitions.Count

    match isAligned table, childrenOf table |> List.traverseResultM rowOf with
    | true, _ -> refused table ColumnAlignment
    | false, Error refusal -> Error refusal
    | false, Ok(Row header :: body) -> Ok(Table(Table.create (Row(List.truncate columns header)) body))
    | false, Ok [] -> unrecognised table

let private proseOf (block: Block) =
    match block with
    | :? ParagraphBlock as paragraph -> leafPhrase paragraph |> Result.map Paragraph
    | :? ListBlock as list -> itemsOf list |> Result.map Bullets
    | _ -> refused block AlertContent

let private kindOf (alert: AlertBlock) =
    match alert.Kind.ToString() with
    | kind when kind.Equals("NOTE", StringComparison.OrdinalIgnoreCase) -> Some Note
    | kind when kind.Equals("WARNING", StringComparison.OrdinalIgnoreCase) -> Some Warning
    | _ -> None

let private alertOf (alert: AlertBlock) =
    match kindOf alert with
    | Some kind ->
        childrenOf alert
        |> List.traverseResultM proseOf
        |> Result.map (fun prose -> Alert(kind, prose))
    | None -> refused alert AlertOfOtherKind

let private blockOf (block: Block) =
    match block with
    | :? HeadingBlock as heading -> headingOf heading
    | :? ParagraphBlock as paragraph -> leafPhrase paragraph |> Result.map (Paragraph >> Prose)
    | :? ListBlock as list -> itemsOf list |> Result.map (Bullets >> Prose)
    | :? Tables.Table as table -> tableOf table
    | :? AlertBlock as alert -> alertOf alert
    | :? QuoteBlock -> refused block Quote
    | :? CodeBlock -> refused block Construct.CodeBlock
    | :? HtmlBlock -> refused block Html
    | :? ThematicBreakBlock -> refused block ThematicBreak
    | :? LinkReferenceDefinitionGroup -> refused block LinkReference
    | _ -> unrecognised block

let parse (text: string) =
    Markdown.Parse(text, pipeline) |> childrenOf |> List.traverseResultM blockOf
