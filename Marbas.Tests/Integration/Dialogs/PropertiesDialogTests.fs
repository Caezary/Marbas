module Marbas.Tests.Integration.Dialogs.PropertiesDialogTests

open System
open System.Drawing
open Xunit
open Terminal.Gui.Views
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.Theme
open Marbas.Ui.Dialogs
open Marbas.Tests

let private at = Root.at

let private shown =
    { Path = FileSystemPath.create (at "start/docs")
      Flags = Set.ofList [ HiddenFlag ]
      Created = DateTimeOffset(DateTime(2024, 3, 1, 9, 5, 7))
      Modified = DateTimeOffset(DateTime(2024, 4, 2, 10, 6, 8))
      Accessed = DateTimeOffset(DateTime(2024, 5, 3, 11, 7, 9)) }

let private laidOutOn platform properties =
    DialogStyle.apply ()
    let dialog = PropertiesDialog.create platform properties
    dialog.BeginInit()
    dialog.EndInit()
    dialog.Layout(Size(80, 24)) |> ignore
    dialog

let private laidOut properties = laidOutOn Windows properties

[<Fact>]
let ``the dialog names the entry it stands for`` () =
    use dialog = laidOut shown

    Assert.Equal(at "start/docs", (PropertiesDialog.name dialog).Text)

[<Fact>]
let ``the dialog built for Windows offers all four flags`` () =
    use dialog = laidOutOn Windows shown

    let offered = PropertiesDialog.offeredFlags dialog

    Assert.Equal<EntryFlag list>([ ReadOnlyFlag; HiddenFlag; SystemFlag; ArchiveFlag ], offered)

[<Fact>]
let ``the dialog built for Unix offers read-only and nothing else`` () =
    use dialog = laidOutOn Unix shown

    let offered = PropertiesDialog.offeredFlags dialog

    Assert.Equal<EntryFlag list>([ ReadOnlyFlag ], offered)

[<Fact>]
let ``a flag the dialog does not offer comes back as the entry carried it`` () =
    use dialog = laidOutOn Unix shown
    (PropertiesDialog.checkBox dialog ReadOnlyFlag).Value <- CheckState.Checked
    dialog.Result <- 1

    let answered = PropertiesDialog.properties Unix shown dialog |> Expect.some

    Assert.Equal<Set<EntryFlag>>(Set.ofList [ ReadOnlyFlag; HiddenFlag ], answered.Flags)

[<Fact>]
let ``the stamps of the dialog built for Unix sit right under its one flag`` () =
    use dialog = laidOutOn Unix shown

    let created = (PropertiesDialog.field dialog PropertiesDialog.CreatedId).Frame.Y

    Assert.Equal((PropertiesDialog.checkBox dialog ReadOnlyFlag).Frame.Y + 2, created)

[<Fact>]
let ``every flag has a box, ticked where the entry carries it`` () =
    use dialog = laidOut shown

    Assert.Equal(CheckState.Checked, (PropertiesDialog.checkBox dialog HiddenFlag).Value)
    Assert.Equal(CheckState.UnChecked, (PropertiesDialog.checkBox dialog ReadOnlyFlag).Value)
    Assert.Equal(CheckState.UnChecked, (PropertiesDialog.checkBox dialog SystemFlag).Value)
    Assert.Equal(CheckState.UnChecked, (PropertiesDialog.checkBox dialog ArchiveFlag).Value)

[<Fact>]
let ``every stamp is offered ready to be edited`` () =
    use dialog = laidOut shown

    Assert.Equal("2024-03-01 09:05:07", (PropertiesDialog.field dialog PropertiesDialog.CreatedId).Text)
    Assert.Equal("2024-04-02 10:06:08", (PropertiesDialog.field dialog PropertiesDialog.ModifiedId).Text)
    Assert.Equal("2024-05-03 11:07:09", (PropertiesDialog.field dialog PropertiesDialog.AccessedId).Text)

[<Fact>]
let ``every field fits inside the dialog`` () =
    use dialog = laidOut shown

    for id in
        [ PropertiesDialog.CreatedId
          PropertiesDialog.ModifiedId
          PropertiesDialog.AccessedId ] do
        let frame = (PropertiesDialog.field dialog id).Frame
        Assert.True(frame.Width > 0, $"the field {id} must be wide enough to show a stamp")
        Assert.True(frame.Bottom <= dialog.Viewport.Height, $"the field {id} at %A{frame} is clipped")

[<Fact>]
let ``a dismissed dialog yields no properties`` () =
    use dialog = laidOut shown

    Assert.Equal(None, PropertiesDialog.properties Windows shown dialog)

[<Fact>]
let ``a ticked box comes back as the flag it stands for`` () =
    use dialog = laidOut shown
    (PropertiesDialog.checkBox dialog ReadOnlyFlag).Value <- CheckState.Checked
    (PropertiesDialog.checkBox dialog HiddenFlag).Value <- CheckState.UnChecked
    dialog.Result <- 1

    let answered = PropertiesDialog.properties Windows shown dialog |> Expect.some

    Assert.True(EntryProperties.has ReadOnlyFlag answered)
    Assert.False(EntryProperties.has HiddenFlag answered)

[<Fact>]
let ``a stamp that was typed comes back`` () =
    use dialog = laidOut shown
    (PropertiesDialog.field dialog PropertiesDialog.CreatedId).Text <- "1999-12-31 23:59:58"
    dialog.Result <- 1

    let answered = PropertiesDialog.properties Windows shown dialog |> Expect.some

    Assert.Equal(DateTimeOffset(DateTime(1999, 12, 31, 23, 59, 58)), answered.Created)

[<Fact>]
let ``a stamp that reads as nothing leaves the one the entry carried`` () =
    use dialog = laidOut shown
    (PropertiesDialog.field dialog PropertiesDialog.CreatedId).Text <- "whenever"
    dialog.Result <- 1

    let answered = PropertiesDialog.properties Windows shown dialog |> Expect.some

    Assert.Equal(shown.Created, answered.Created)

[<Fact>]
let ``the entry the properties belong to is never changed`` () =
    use dialog = laidOut shown
    dialog.Result <- 1

    let answered = PropertiesDialog.properties Windows shown dialog |> Expect.some

    Assert.Equal(shown.Path, answered.Path)
