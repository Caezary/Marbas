module Marbas.Tests.Ui.Dialogs.AssociationRowTests

open System
open Xunit
open Marbas.Domain
open Marbas.Ui.Dialogs
open Marbas.Tests

let private association extension program =
    { Extension = Expect.some (FileExtension.tryCreate extension)
      Role = ForEditing
      Program = Expect.some (ProgramPath.tryCreate program) }

[<Fact>]
let ``a row names the extension and the program it opens`` () =
    let row = AssociationRow.describe (association "txt" "notepad.exe")

    Assert.StartsWith("txt", row)
    Assert.EndsWith("notepad.exe", row)

[<Fact>]
let ``the programs line up whatever the extension`` () =
    let short = AssociationRow.describe (association "fs" "rider.exe")
    let long = AssociationRow.describe (association "txt" "notepad.exe")

    Assert.Equal(
        short.IndexOf("rider.exe", StringComparison.Ordinal),
        long.IndexOf("notepad.exe", StringComparison.Ordinal)
    )

[<Fact>]
let ``an extension too long for the column is still followed by its program`` () =
    let row = AssociationRow.describe (association "verylongextension" "code.exe")

    Assert.Contains("verylongextension code.exe", row)
