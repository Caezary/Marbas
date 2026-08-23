module Marbas.Tests.Ui.AssociationRowTests

open Xunit
open Marbas.FileSystem
open Marbas.Ui
open Marbas.Tests

let private association extension editor =
    { Extension = Expect.some (FileExtension.tryCreate extension)
      Editor = Expect.some (EditorPath.tryCreate editor) }

[<Fact>]
let ``a row names the extension and the editor it opens`` () =
    let row = AssociationRow.describe (association "txt" "notepad.exe")

    Assert.StartsWith("txt", row)
    Assert.EndsWith("notepad.exe", row)

[<Fact>]
let ``the editors line up whatever the extension`` () =
    let short = AssociationRow.describe (association "fs" "rider.exe")
    let long = AssociationRow.describe (association "txt" "notepad.exe")

    Assert.Equal(short.IndexOf "rider.exe", long.IndexOf "notepad.exe")

[<Fact>]
let ``an extension too long for the column is still followed by its editor`` () =
    let row = AssociationRow.describe (association "verylongextension" "code.exe")

    Assert.Contains("verylongextension code.exe", row)
