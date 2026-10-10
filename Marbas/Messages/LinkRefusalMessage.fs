module Marbas.Messages.LinkRefusalMessage

open Marbas.Domain

let describe refusal =
    match refusal with
    | HardLinkToDirectory -> "A hard link can only point at a file."
    | HardLinkAcrossVolumes -> "A hard link must stand on the volume of its file."
    | JunctionToFile -> "A junction can only point at a directory."
    | KindUnavailable -> "This system has no way to make a link of this kind."
