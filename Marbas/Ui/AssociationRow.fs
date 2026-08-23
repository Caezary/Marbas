module Marbas.Ui.AssociationRow

open Marbas.FileSystem

[<Literal>]
let ExtensionWidth = 12

let describe (association: EditorAssociation) =
    let extension = FileExtension.value association.Extension
    let padded = extension.PadRight(ExtensionWidth - 1)
    $"{padded} {EditorPath.value association.Editor}"
