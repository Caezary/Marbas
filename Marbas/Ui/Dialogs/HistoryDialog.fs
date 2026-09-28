module Marbas.Ui.Dialogs.HistoryDialog

open System
open System.Collections.Generic
open System.IO.Abstractions
open Terminal.Gui.Drawing
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.Ui
open Marbas.Ui.Format
open Marbas.Ui.Keys
open Marbas.Ui.Theme

[<Literal>]
let Title = "History"

[<Literal>]
let CloseButton = 0

[<Literal>]
let DeleteButton = 1

[<Literal>]
let GoToButton = 2

[<Literal>]
let OpenButton = 3

[<Literal>]
let private WhenColumn = "When"

[<Literal>]
let private WhatColumn = "What"

[<Literal>]
let private WhatColumnIndex = 1

[<Literal>]
let private TimePatternWidth = 10

[<Literal>]
let private ColumnSeparatorWidth = 1

[<Literal>]
let private SizeShare = 80

type Row = { When: string; What: string }

type HistoryView(history: History) =
    inherit Dialog(Title = Title, Width = Dim.Percent SizeShare, Height = Dim.Percent SizeShare)

    member _.History = history

let private tabOrder = [ RunHistoryTab; ViewHistoryTab; FolderHistoryTab ]

let private tabId tab =
    match tab with
    | RunHistoryTab -> "runHistory"
    | ViewHistoryTab -> "viewHistory"
    | FolderHistoryTab -> "folderHistory"

let private tabTitle tab =
    match tab with
    | RunHistoryTab -> "History"
    | ViewHistoryTab -> "View history"
    | FolderHistoryTab -> "Folder history"

let private rowsOf (fs: IFileSystem) now (history: History) tab =
    let row at what = { When = HistoryRow.moment now at; What = what }

    match tab with
    | RunHistoryTab ->
        history.Launches
        |> List.map (fun entry -> row entry.At (FileSystemPath.value entry.Item.Target))
    | ViewHistoryTab ->
        history.FileVisits
        |> List.map (fun entry -> row entry.At (HistoryRow.place fs entry.Item.Where))
    | FolderHistoryTab -> history.Folders |> List.map (fun entry -> row entry.At (HistoryRow.place fs entry.Item))

let private source rows =
    let columns = Dictionary<string, Func<Row, obj>>()
    columns.Add(WhenColumn, Func<Row, obj>(fun row -> box row.When))
    columns.Add(WhatColumn, Func<Row, obj>(fun row -> box row.What))
    EnumerableTableSource<Row>(rows, columns)

let private fitted (fs: IFileSystem) (table: TableView) (value: obj) =
    let width = table.Viewport.Width - TimePatternWidth - ColumnSeparatorWidth
    PathText.shorten fs.Path.DirectorySeparatorChar width (string value)

let private tableFor (fs: IFileSystem) rows tab =
    let table =
        new TableView(Title = tabTitle tab, Id = tabId tab, Width = Dim.Fill(), Height = Dim.Fill())

    table.Table <- source rows
    table.FullRowSelect <- true
    table.CollectionNavigator <- null
    table.Style.ShowHorizontalHeaderOverline <- false
    table.Style.ShowHorizontalBottomLine <- false
    table.Style.ShowVerticalCellLineForFirstColumn <- false
    table.Style.ShowVerticalCellLineForLastColumn <- false
    table.Style.GetOrCreateColumnStyle(WhatColumnIndex).RepresentationGetter <- Func<obj, string>(fitted fs table)
    table

let tabs (dialog: HistoryView) = SubView.byType<Tabs> dialog

let table tab (dialog: HistoryView) =
    SubView.byId<TableView> (tabId tab) (tabs dialog)

let button index (dialog: HistoryView) = Seq.item index dialog.Buttons

let front (dialog: HistoryView) =
    let shown = (tabs dialog).Value

    tabOrder
    |> List.tryFind (fun tab -> obj.ReferenceEquals(table tab dialog, shown))
    |> Option.defaultValue FolderHistoryTab

let private walk step (dialog: HistoryView) =
    let index = List.findIndex ((=) (front dialog)) tabOrder
    let next = tabOrder[(index + step + tabOrder.Length) % tabOrder.Length]
    (tabs dialog).Value <- table next dialog

let private pressing index (dialog: HistoryView) =
    dialog.Result <- Nullable index
    dialog.RequestStop()

let private answerKey (dialog: HistoryView) (key: Key) =
    if key = KeyMap.nextHistoryTabKey then
        key.Handled <- true
        walk 1 dialog
    elif key = KeyMap.previousHistoryTabKey then
        key.Handled <- true
        walk -1 dialog
    elif key = KeyMap.historyGoToKey then
        key.Handled <- true

        if (button GoToButton dialog).Enabled then
            pressing GoToButton dialog

let private plainButton title = DialogStyle.button title |> LiteralText.showing

let create (fs: IFileSystem) (history: History) (tab: HistoryTab) (now: DateTimeOffset) =
    let dialog = new HistoryView(history)
    DialogStyle.dress dialog |> ignore

    let container = new Tabs(X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill())
    container.TabLineStyle <- LineStyle.Single

    let tables = tabOrder |> List.map (fun shown -> tableFor fs (rowsOf fs now history shown) shown)
    tables |> List.iter (fun shown -> container.Add shown |> ignore)
    dialog.Add container |> ignore

    let close = plainButton "Close"
    let delete = plainButton "Delete"
    let goTo = plainButton "Go to"
    let opening = plainButton "Open"
    delete.Enabled <- false
    goTo.Enabled <- false
    opening.IsDefault <- true
    [ close; delete; goTo; opening ] |> List.iter dialog.AddButton

    container.Value <- table tab dialog
    dialog.KeyDown.Add(answerKey dialog)
    dialog

let private rowUnderCursor (shown: TableView) =
    match shown.Value with
    | null -> None
    | selection -> Some selection.SelectedCell.Y

let private chosen (dialog: HistoryView) tab row =
    match tab with
    | FolderHistoryTab -> List.tryItem row dialog.History.Folders |> Option.map (_.Item >> OpenPlace)
    | RunHistoryTab
    | ViewHistoryTab -> None

let answer (dialog: HistoryView) =
    let tab = front dialog

    let choice =
        match Option.ofNullable dialog.Result with
        | Some OpenButton -> rowUnderCursor (table tab dialog) |> Option.bind (chosen dialog tab)
        | _ -> None

    { Tab = tab; Choice = choice }
