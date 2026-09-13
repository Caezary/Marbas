namespace Marbas.Domain

type LinkKind =
    | Symbolic
    | Hard
    | Junction

type LinkRefusal =
    | HardLinkToDirectory
    | HardLinkAcrossVolumes
    | JunctionToFile
    | JunctionOffWindows
