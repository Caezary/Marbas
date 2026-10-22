namespace Marbas.Guide

type Stress =
    | Bold
    | Italic

type Look =
    | Plain
    | Literal
    | Title of HeadingLevel
    | Ruling
    | Framing of AlertKind

type LinkStop = { Index: int; Target: Target }

type Piece =
    { Text: string
      Look: Look
      Stress: Set<Stress>
      Link: LinkStop option }

type LaidLine =
    { Pieces: Piece list
      Anchor: Anchor option
      Block: int }
