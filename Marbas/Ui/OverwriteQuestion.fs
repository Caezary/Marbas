module Marbas.Ui.OverwriteQuestion

open Marbas.FileSystem
open Marbas.FileSystem.Operations

[<Literal>]
let Title = "Overwrite"

let Answers = [ "_Yes"; "_No"; "_All"; "_Cancel" ]

let describe path =
    $"'{FileSystemPath.value path}' already exists. Overwrite it?"

let answerAt index =
    match index with
    | 0 -> Yes
    | 1 -> No
    | 2 -> All
    | _ -> Cancel
