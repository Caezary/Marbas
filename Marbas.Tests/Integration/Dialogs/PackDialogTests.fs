module Marbas.Tests.Integration.Dialogs.PackDialogTests

open System
open System.Drawing
open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Terminal.Gui.Views
open Marbas.FileSystem
open Marbas.Ui.Theme
open Marbas.Ui.Dialogs
open Marbas.Tests

let private suggestion = Root.at "backup/src.zip"

let private laidOutWith remembered =
    DialogStyle.apply ()

    let dialog =
        PackDialog.create (ArchiveName.retarget (MockFileSystem())) remembered suggestion

    dialog.BeginInit()
    dialog.EndInit()
    dialog.Layout(Size(80, 24)) |> ignore
    dialog

let private laidOut () = laidOutWith []

let private target dialog =
    PackDialog.field dialog PackDialog.TargetId

let private password dialog =
    PackDialog.field dialog PackDialog.PasswordId

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

    Assert.Equal<string list>([ "_Zip"; "_Tar.gz"; "_7z" ], List.ofSeq (Expect.notNull formats.Labels))
    Assert.Equal<string list>([ "_Store"; "_Normal"; "Ma_ximum" ], List.ofSeq (Expect.notNull levels.Labels))
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

    choose dialog PackDialog.FormatId 2
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
    Assert.Equal(TarGz, ArchiveFormat.kind answered.Format)
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

[<Fact>]
let ``the archives packed into before are offered as the list of the target field`` () =
    use dialog = laidOutWith [ @"D:\one.zip"; @"D:\two.7z" ]

    let offered =
        [ for item in (Expect.notNull (target dialog :?> DropDownList).Source).ToList() -> string item ]

    Assert.Equal<string list>([ @"D:\one.zip"; @"D:\two.7z" ], offered)

[<Theory>]
[<InlineData("CursorUp")>]
[<InlineData("CursorDown")>]
let ``the arrows are left to move between the fields of the pack popup`` (spelled: string) =
    use dialog = laidOut ()

    let taken = (target dialog).KeyBindings.TryGet(Key.TryParse spelled |> snd) |> fst

    Assert.False taken

[<Fact>]
let ``the password is typed into a plain field, never one with a history`` () =
    use dialog = laidOutWith [ @"D:\one.zip" ]

    let field = password dialog

    Assert.IsNotType<DropDownList>(field)
