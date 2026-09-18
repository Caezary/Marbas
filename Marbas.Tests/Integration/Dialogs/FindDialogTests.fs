module Marbas.Tests.Integration.Dialogs.FindDialogTests

open System
open System.Drawing
open Xunit
open Terminal.Gui.Views
open Marbas.Domain
open Marbas.Ui.Theme
open Marbas.Ui.Dialogs
open Marbas.Tests

let private opened ground hidden found =
    DialogStyle.apply ()
    let dialog = FindDialog.create ground hidden found
    dialog.BeginInit()
    dialog.EndInit()
    dialog.Layout(Size(80, 24)) |> ignore
    dialog

let private laidOut ground hidden = opened ground hidden None

let private fresh () = laidOut NoDirectoryMarked Concealed

let private masks dialog = FindDialog.field dialog FindDialog.MasksId

let private text dialog = FindDialog.field dialog FindDialog.TextId

let private scopes dialog = FindDialog.selector dialog

let private box dialog which = FindDialog.checkBox dialog which

let private enabled dialog which = (box dialog which).Enabled

let private tick dialog which =
    (box dialog which).Value <- CheckState.Checked

let private AcceptingButton = 2

let private answered (dialog: FindDialog.FindView) =
    dialog.Result <- AcceptingButton
    FindDialog.choice dialog |> Expect.some

let private accepted (dialog: FindDialog.FindView) =
    match answered dialog with
    | FindDialog.Search query -> query
    | FindDialog.LastResults -> failwith "expected a query, got the last results"

let private scopeChosen index =
    use dialog = fresh ()
    (scopes dialog).Value <- Nullable index
    (accepted dialog).Scope

[<Fact>]
let ``a fresh popup asks for every name, no text, from the directory the panel stands in`` () =
    use dialog = fresh ()

    Assert.Equal(FindDialog.EveryName, (masks dialog).Text)
    Assert.Equal("", (text dialog).Text)
    Assert.Equal(Some 0, (scopes dialog).Value |> Option.ofNullable)

[<Fact>]
let ``every scope is offered`` () =
    use dialog = fresh ()

    Assert.Equal<string list>(
        FindDialog.scopes |> List.map FindDialog.scopeLabel,
        List.ofSeq (scopes dialog).Labels
    )

[<Fact>]
let ``the scope that was chosen is the scope that comes back`` () =
    Assert.Equal(FromDirectory, scopeChosen 0)
    Assert.Equal(DirectoryOnly, scopeChosen 1)
    Assert.Equal(FromVolumeRoot, scopeChosen 3)
    Assert.Equal(AllDrives, scopeChosen 4)
    Assert.Equal(PathDirectories, scopeChosen 5)

[<Fact>]
let ``the marked directories are closed while nothing is marked`` () =
    use dialog = fresh ()

    let marked = (scopes dialog).SubViews |> Seq.item 2

    Assert.False marked.Enabled

[<Fact>]
let ``a marked directory opens the scope that walks it`` () =
    use dialog = laidOut DirectoriesMarked Concealed
    (scopes dialog).Value <- Nullable 2

    let query = accepted dialog

    Assert.True(((scopes dialog).SubViews |> Seq.item 2).Enabled)
    Assert.Equal(MarkedDirectories, query.Scope)

[<Fact>]
let ``the text options are closed while there is no text to look for`` () =
    use dialog = fresh ()

    Assert.False(enabled dialog FindDialog.CaseBox)
    Assert.False(enabled dialog FindDialog.WordsBox)
    Assert.False(enabled dialog FindDialog.SenseBox)
    Assert.False(enabled dialog FindDialog.BinaryBox)
    Assert.True(enabled dialog FindDialog.DirectoriesBox)

[<Fact>]
let ``typed text opens the text options and closes the directories`` () =
    use dialog = fresh ()

    (text dialog).Text <- "needle"

    Assert.True(enabled dialog FindDialog.CaseBox)
    Assert.True(enabled dialog FindDialog.WordsBox)
    Assert.True(enabled dialog FindDialog.SenseBox)
    Assert.True(enabled dialog FindDialog.BinaryBox)
    Assert.False(enabled dialog FindDialog.DirectoriesBox)

[<Fact>]
let ``text taken back again closes the text options`` () =
    use dialog = fresh ()
    (text dialog).Text <- "needle"

    (text dialog).Text <- ""

    Assert.False(enabled dialog FindDialog.CaseBox)
    Assert.True(enabled dialog FindDialog.DirectoriesBox)

[<Fact>]
let ``the hidden box opens closed on a panel that conceals`` () =
    use dialog = laidOut NoDirectoryMarked Concealed

    Assert.Equal(CheckState.UnChecked, (box dialog FindDialog.HiddenBox).Value)

[<Fact>]
let ``the hidden box opens ticked on a panel that shows the hidden entries`` () =
    use dialog = laidOut NoDirectoryMarked Shown

    Assert.Equal(CheckState.Checked, (box dialog FindDialog.HiddenBox).Value)

[<Fact>]
let ``the last results are offered and closed until there are any`` () =
    use dialog = fresh ()

    let button = FindDialog.lastResults dialog

    Assert.Equal(FindDialog.LastResultsTitle, button.Title)
    Assert.False button.Enabled

[<Fact>]
let ``a dismissed popup answers nothing`` () =
    use dialog = fresh ()

    Assert.Equal(None, FindDialog.choice dialog)

[<Fact>]
let ``the masks that were typed are the masks that come back`` () =
    use dialog = fresh ()
    (masks dialog).Text <- "*.fs;*.fsproj"

    let query = accepted dialog

    Assert.True(FindQuery.matchesName query (EntryName.create "Update.fs"))
    Assert.False(FindQuery.matchesName query (EntryName.create "notes.txt"))

[<Fact>]
let ``a popup with no text asks about the name alone`` () =
    use dialog = fresh ()

    let query = accepted dialog

    Assert.Equal(None, FindQuery.text query)
    Assert.Equal(FilesOnly, FindQuery.targets query)

[<Fact>]
let ``the directories are searched for when they are ticked`` () =
    use dialog = fresh ()
    tick dialog FindDialog.DirectoriesBox

    let query = accepted dialog

    Assert.Equal(FilesAndDirectories, FindQuery.targets query)

[<Fact>]
let ``text asks about files alone, whatever the directories box was left on`` () =
    use dialog = fresh ()
    tick dialog FindDialog.DirectoriesBox

    (text dialog).Text <- "needle"

    Assert.Equal(FilesOnly, FindQuery.targets (accepted dialog))

[<Fact>]
let ``the text and every option ticked beside it come back together`` () =
    use dialog = fresh ()
    (text dialog).Text <- "needle"
    [ FindDialog.CaseBox; FindDialog.WordsBox; FindDialog.SenseBox; FindDialog.BinaryBox ]
    |> List.iter (tick dialog)

    let search = FindQuery.text (accepted dialog) |> Expect.some

    Assert.Equal("needle", FindText.value search.Text)
    Assert.Equal(MatchingCase, search.Case)
    Assert.Equal(WholeWords, search.Words)
    Assert.Equal(NotContaining, search.Sense)
    Assert.Equal(Searched, search.Binary)

[<Fact>]
let ``text left untouched comes back with the gentler answer to each of its options`` () =
    use dialog = fresh ()
    (text dialog).Text <- "needle"

    let search = FindQuery.text (accepted dialog) |> Expect.some

    Assert.Equal(IgnoringCase, search.Case)
    Assert.Equal(Anywhere, search.Words)
    Assert.Equal(Containing, search.Sense)
    Assert.Equal(Skipped, search.Binary)

[<Fact>]
let ``the walk follows what the three boxes beneath the scope were left on`` () =
    use dialog = fresh ()

    let query = accepted dialog

    Assert.Equal(Concealed, query.Hidden)
    Assert.Equal(StopAtLinks, query.Links)
    Assert.Equal(SkipArchives, query.Archives)

[<Fact>]
let ``the hidden entries, the links and the archives are asked for when they are ticked`` () =
    use dialog = fresh ()
    [ FindDialog.HiddenBox; FindDialog.LinksBox; FindDialog.ArchivesBox ]
    |> List.iter (tick dialog)

    let query = accepted dialog

    Assert.Equal(Shown, query.Hidden)
    Assert.Equal(FollowLinks, query.Links)
    Assert.Equal(SearchArchives, query.Archives)

[<Fact>]
let ``the popup stays inside an eighty-column terminal`` () =
    use dialog = fresh ()

    Assert.True(dialog.Frame.Width <= 80, $"a frame {dialog.Frame.Width} wide")
    Assert.True(dialog.Frame.Height <= 24, $"a frame {dialog.Frame.Height} tall")

[<Fact>]
let ``every button fits on the row the popup gives them`` () =
    use dialog = fresh ()

    let taken = dialog.Buttons |> Seq.sumBy (fun button -> button.Frame.Width)

    Assert.True(taken <= dialog.Frame.Width - 2, $"buttons take {taken} of a frame {dialog.Frame.Width} wide")

[<Fact>]
let ``every label fits inside the popup`` () =
    use dialog = fresh ()

    let widest =
        dialog.SubViews
        |> Seq.collect (fun view -> Seq.append [ view ] view.SubViews)
        |> Seq.map (fun view -> view.Frame.X + view.Frame.Width)
        |> Seq.fold max 0

    Assert.True(widest <= dialog.Viewport.Width, $"{widest} columns of a viewport {dialog.Viewport.Width} wide")

[<Fact>]
let ``the last results carry how many there are`` () =
    use dialog = opened NoDirectoryMarked Concealed (Some 123)

    let button = FindDialog.lastResults dialog

    Assert.Equal("_Last results (123)", button.Title)
    Assert.True button.Enabled

[<Fact>]
let ``asking for the last results is what the popup answers`` () =
    use dialog = opened NoDirectoryMarked Concealed (Some 123)

    (FindDialog.lastResults dialog).InvokeCommand Terminal.Gui.Input.Command.Accept |> ignore

    Assert.Equal(Some FindDialog.LastResults, FindDialog.choice dialog)
