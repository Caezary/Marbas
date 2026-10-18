namespace Marbas.Programs

open Marbas.Domain
open Marbas.FileSystem

type ExpansionRefusal =
    | InsideArchive of place: FileSystemPath
    | NoEntryChosen of side: PanelSide
    | NoDirectory of side: PanelSide
    | NoProgram
