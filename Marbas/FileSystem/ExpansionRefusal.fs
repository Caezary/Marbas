namespace Marbas.FileSystem

open Marbas.Domain

type ExpansionRefusal =
    | InsideArchive of place: FileSystemPath
    | NoEntryChosen of side: PanelSide
    | NoDirectory of side: PanelSide
    | NoProgram
