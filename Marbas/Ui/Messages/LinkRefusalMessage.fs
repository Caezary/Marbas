module Marbas.Ui.Messages.LinkRefusalMessage

open Marbas.Domain

let describe refusal =
    match refusal with
    | HardLinkToDirectory -> "A hard link can only point at a file."
    | HardLinkAcrossVolumes -> "A hard link must stand on the volume of the file it points at."
    | JunctionToFile -> "A junction can only point at a directory."
    | JunctionOffWindows -> "A junction exists only on Windows."
