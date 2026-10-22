module Marbas.Ui.Help.HelpReport

open Marbas.Guide

[<Literal>]
let Title = "Help"

let construct refused =
    match refused with
    | DeepHeading -> "a heading below ###"
    | OrderedList -> "a numbered list"
    | CodeBlock -> "a code block"
    | Quote -> "a quote"
    | AlertOfOtherKind -> "an alert other than a note or a warning"
    | AlertContent -> "something other than paragraphs and lists inside an alert"
    | ItemContent -> "something other than a paragraph and one nested list inside an item"
    | ThematicBreak -> "a horizontal rule"
    | Html -> "HTML"
    | Image -> "an image"
    | HardLineBreak -> "a hard line break"
    | ColumnAlignment -> "an aligned table column"
    | LinkOutsideTheGuide -> "a link outside the guide"
    | LinkReference -> "a link reference definition"
    | Unrecognised name -> $"Markdown the help does not know ({name})"

let describe fault =
    match fault with
    | Absent name -> $"The guide built into Marbas has no chapter {ChapterName.value name}."
    | Refused(name, refusal) ->
        $"{ChapterName.value name} cannot be shown: line {refusal.Line} holds {construct refusal.Construct}, which the help does not draw."
