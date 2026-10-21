namespace Marbas.Guide

type Construct =
    | DeepHeading
    | OrderedList
    | CodeBlock
    | Quote
    | AlertOfOtherKind
    | AlertContent
    | ItemContent
    | ThematicBreak
    | Html
    | Image
    | HardLineBreak
    | ColumnAlignment
    | LinkOutsideTheGuide
    | LinkReference
    | Unrecognised of string

type Refusal = { Line: int; Construct: Construct }
