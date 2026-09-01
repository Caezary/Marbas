module Marbas.Ui.Dialogs.AssociationRow

open Marbas.FileSystem

[<Literal>]
let ExtensionWidth = 12

let describe (association: ProgramAssociation) =
    let extension = FileExtension.value association.Extension
    let padded = extension.PadRight(ExtensionWidth - 1)
    $"{padded} {ProgramPath.value association.Program}"
