namespace Marbas.Domain

open System

type Likeness =
    | SameExtension
    | SameStem

module Likeness =

    let private part likeness name =
        match likeness with
        | SameExtension -> EntryName.extension name
        | SameStem -> EntryName.stem name

    let alike likeness one other =
        String.Equals(part likeness one, part likeness other, StringComparison.OrdinalIgnoreCase)
