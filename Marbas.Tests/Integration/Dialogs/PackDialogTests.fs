module Marbas.Tests.Integration.Dialogs.PackDialogTests

open System
open System.Drawing
open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Views
open Marbas.FileSystem
open Marbas.Ui.Theme
open Marbas.Ui.Dialogs
open Marbas.Tests

let private suggestion = Root.at "backup/src.zip"

let private laidOut () =
    DialogStyle.apply ()
    let dialog = PackDialog.create (ArchiveName.retarget (MockFileSystem())) suggestion
    dialog.BeginInit()
    dialog.EndInit()
    dialog.Layout(Size(80, 24)) |> ignore
    dialog

let private target dialog = PackDialog.field dialog PackDialog.TargetId

let private password dialog = PackDialog.field dialog PackDialog.PasswordId

let private choose dialog id index =
    (PackDialog.selector dialog id).Value <- Nullable index

let private accepted (dialog: Dialog) =
    dialog.Result <- 1
    PackDialog.choice dialog |> Expect.some

[<Fact>]
let ``the suggested archive is offered ready to be edited`` () =
    use dialog = laidOut ()

    Assert.Equal(suggestion, (target dialog).Text)

[<Fact>]
let ``a fresh dialog offers every format and level, on zip and normal`` () =
    use dialog = laidOut ()

    let formats = PackDialog.selector dialog PackDialog.FormatId
    let levels = PackDialog.selector dialog PackDialog.CompressionId

    Assert.Equal<string list>([ "_Zip"; "_Tar.gz" ], List.ofSeq formats.Labels)
    Assert.Equal<string list>([ "_Store"; "_Normal"; "Ma_ximum" ], List.ofSeq levels.Labels)
    Assert.Equal(Some 0, formats.Value |> Option.ofNullable)
    Assert.Equal(Some 1, levels.Value |> Option.ofNullable)

[<Fact>]
let ``choosing another format swaps the extension of the target`` () =
    use dialog = laidOut ()

    choose dialog PackDialog.FormatId 1

    Assert.Equal(Root.at "backup/src.tar.gz", (target dialog).Text)

[<Fact>]
let ``a name the user typed keeps its place when the format changes`` () =
    use dialog = laidOut ()
    (target dialog).Text <- Root.at "backup/release.zip"

    choose dialog PackDialog.FormatId 1

    Assert.Equal(Root.at "backup/release.tar.gz", (target dialog).Text)

[<Fact>]
let ``a format that carries no password closes the password field`` () =
    use dialog = laidOut ()

    Assert.True((password dialog).Enabled)

    choose dialog PackDialog.FormatId 1
    Assert.False((password dialog).Enabled)

    choose dialog PackDialog.FormatId 0
    Assert.True((password dialog).Enabled)

[<Fact>]
let ``the password is typed out of sight`` () =
    use dialog = laidOut ()

    Assert.True((password dialog).Secret)

[<Fact>]
let ``a dismissed dialog answers nothing`` () =
    use dialog = laidOut ()

    Assert.Equal(None, PackDialog.choice dialog)

[<Fact>]
let ``what was typed and chosen is what comes back`` () =
    use dialog = laidOut ()
    (target dialog).Text <- Root.at "backup/release.tar.gz"
    choose dialog PackDialog.FormatId 1
    choose dialog PackDialog.CompressionId 2

    let answered = accepted dialog

    Assert.Equal(Root.at "backup/release.tar.gz", answered.Target)
    Assert.Equal(TarGz, answered.Format)
    Assert.Equal(Maximum, answered.Compression)
    Assert.Equal(None, answered.Password)

[<Fact>]
let ``a password given to a zip comes back with it`` () =
    use dialog = laidOut ()
    (password dialog).Text <- "letmein"

    let answered = accepted dialog

    Assert.Equal(Some "letmein", answered.Password |> Option.map ArchivePassword.value)

[<Fact>]
let ``a blank password is no password at all`` () =
    use dialog = laidOut ()
    (password dialog).Text <- ""

    Assert.Equal(None, (accepted dialog).Password)

[<Fact>]
let ``a password left behind on a format that cannot carry one is dropped`` () =
    use dialog = laidOut ()
    (password dialog).Text <- "letmein"

    choose dialog PackDialog.FormatId 1

    Assert.Equal(None, (accepted dialog).Password)
