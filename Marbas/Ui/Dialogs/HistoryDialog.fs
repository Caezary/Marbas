module Marbas.Ui.Dialogs.HistoryDialog

open System
open System.Collections.Generic
open System.IO.Abstractions
open Terminal.Gui.Drawing
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Domain
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
let private HowColumn = "How"

[<Literal>]
let private WhenColumnIndex = 0

[<Literal>]
let private WhatColumnIndex = 1

[<Literal>]
let private HowColumnIndex = 2

[<Literal>]
let private ColumnSeparatorWidth = 1

[<Literal>]
let private HeaderPadding = 3

[<Literal>]
let private HeaderEdges = 2

let private whenWidth = HistoryRow.DatePattern.Length

let private howWidth = HistoryRow.AdministratorNote.Length

[<Literal>]
let private SizeShare = 80

[<Literal>]
let private FilterLineId = "historyFilter"

[<Literal>]
let private FilterCaption = "Filter: "

type Row =
    { Entry: int
      When: string
      What: string
      How: string }

type HistoryView(history: History) =
    inherit Dialog(Title = Title, Width = Dim.Percent SizeShare, Height = Dim.Percent SizeShare)

    member val History = history with get, set

    member val AskedUse: FileUse option = None with get, set

    member val Filter: SearchText option = None with get, set

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

[<Literal>]
let ClearTitle = "Clear history"

let clearQuestion tab =
    $"Forget every entry on the {tabTitle tab} tab?"

let private rowsOf (fs: IFileSystem) now (history: History) tab =
    let row entry at what how =
        { Entry = entry
          When = HistoryRow.moment now at
          What = what
          How = how }

    match tab with
    | RunHistoryTab ->
        history.Launches
        |> List.mapi (fun index entry ->
            row index entry.At (HistoryRow.launchWhat entry.Item) (HistoryRow.launchHow entry.Item))
    | ViewHistoryTab ->
        history.FileVisits
        |> List.mapi (fun index entry ->
            row index entry.At (HistoryRow.place fs entry.Item.Where) (HistoryRow.visitHow entry.Item.How))
    | FolderHistoryTab ->
        history.Folders
        |> List.mapi (fun index entry -> row index entry.At (HistoryRow.place fs entry.Item) "")

let private holds filter (row: Row) =
    SearchText.foundIn row.What filter || SearchText.foundIn row.How filter

let private narrowed filter rows =
    match filter with
    | Some typed -> rows |> List.filter (holds typed)
    | None -> rows

let private saysHow tab =
    match tab with
    | RunHistoryTab
    | ViewHistoryTab -> true
    | FolderHistoryTab -> false

let private source tab rows =
    let columns = Dictionary<string, Func<Row, obj>>()
    columns.Add(WhenColumn, Func<Row, obj>(fun row -> box row.When))
    columns.Add(WhatColumn, Func<Row, obj>(fun row -> box row.What))

    if saysHow tab then
        columns.Add(HowColumn, Func<Row, obj>(fun row -> box row.How))

    EnumerableTableSource<Row>(rows, columns)

let private besideWhat tab =
    if saysHow tab then
        whenWidth + howWidth + 2 * ColumnSeparatorWidth
    else
        whenWidth + ColumnSeparatorWidth

let private whatWidth tab (table: TableView) =
    table.Viewport.Width - besideWhat tab |> max 0

let private holdWidth width (style: ColumnStyle) =
    style.MinWidth <- width
    style.MaxWidth <- width

let private fitColumns tab (table: TableView) =
    table.Style.GetOrCreateColumnStyle WhenColumnIndex |> holdWidth whenWidth
    table.Style.GetOrCreateColumnStyle WhatColumnIndex |> holdWidth (whatWidth tab table)

    if saysHow tab then
        table.Style.GetOrCreateColumnStyle HowColumnIndex |> holdWidth howWidth

let private fitted (fs: IFileSystem) tab (table: TableView) (value: obj) =
    PathText.shorten fs.Path.DirectorySeparatorChar (whatWidth tab table) (string value)

let private fill (fs: IFileSystem) now (history: History) filter tab (table: TableView) =
    let rows = rowsOf fs now history tab |> narrowed filter
    table.Table <- source tab rows
    fitColumns tab table

    table.Style.GetOrCreateColumnStyle(WhatColumnIndex).RepresentationGetter <-
        Func<obj, string>(fitted fs tab table)

let private tableFor tab =
    let table =
        new TableView(Title = tabTitle tab, Id = tabId tab, Width = Dim.Fill(), Height = Dim.Fill())

    table.FullRowSelect <- true
    table.CollectionNavigator <- null
    table.Style.ShowHorizontalHeaderOverline <- false
    table.Style.ShowHorizontalBottomLine <- false
    table.Style.ShowVerticalCellLineForFirstColumn <- false
    table.Style.ShowVerticalCellLineForLastColumn <- false
    table.FrameChanged.Add(fun _ ->
        fitColumns tab table
        table.EnsureCursorIsVisible())

    table

let tabs (dialog: HistoryView) = SubView.byType<Tabs> dialog

let table tab (dialog: HistoryView) =
    SubView.byId<TableView> (tabId tab) (tabs dialog)

let filterLine (dialog: HistoryView) = SubView.byId<Label> FilterLineId dialog

let private longestTitle =
    tabOrder |> List.map (tabTitle >> String.length) |> List.max

let headerLength available =
    let sharing = (available + tabOrder.Length - 1) / tabOrder.Length

    sharing
    |> min (longestTitle + 2 * HeaderPadding + HeaderEdges)
    |> max (longestTitle + HeaderEdges)

let private borderOf (shown: TableView) = shown.Border.View :?> BorderView

let header tab (dialog: HistoryView) = (borderOf (table tab dialog)).TitleView

let private dressHeader length (shown: TableView) =
    let border = borderOf shown
    border.TabLength <- Nullable length
    border.TitleView.TextAlignment <- Alignment.Center
    border.TitleView.SetScheme ClassicScheme.tabHeader |> ignore

let private dressHeaders (container: Tabs) tables =
    let length = headerLength container.Viewport.Width
    tables |> List.iter (snd >> dressHeader length)

let button index (dialog: HistoryView) = Seq.item index dialog.Buttons

let private shownIn (container: Tabs) =
    match container.Value with
    | null -> None
    | shown -> tabOrder |> List.tryFind (fun tab -> tabId tab = shown.Id)

let front (dialog: HistoryView) =
    shownIn (tabs dialog) |> Option.defaultValue FolderHistoryTab

let private walk step (dialog: HistoryView) =
    let index = List.findIndex ((=) (front dialog)) tabOrder
    let next = tabOrder[(index + step + tabOrder.Length) % tabOrder.Length]
    (tabs dialog).Value <- table next dialog

let private pressing index (dialog: HistoryView) =
    dialog.Result <- Nullable index
    dialog.RequestStop()

let private rowCount (shown: TableView) =
    match shown.Table with
    | null -> 0
    | rows -> rows.Rows

let private entryUnderCursor (shown: TableView) =
    match shown.Value, shown.Table with
    | null, _
    | _, null -> None
    | selection, (:? EnumerableTableSource<Row> as rows) when selection.SelectedCell.Y < rows.Rows ->
        Some (rows.GetObjectOnRow selection.SelectedCell.Y).Entry
    | _ -> None

let private rowUnderCursor (shown: TableView) =
    match shown.Value with
    | null -> None
    | selection -> Some selection.SelectedCell.Y

let private onFirstRow (shown: TableView) =
    rowUnderCursor shown |> Option.forall (fun row -> row <= 0)

let private onLastRow (shown: TableView) =
    rowUnderCursor shown |> Option.forall (fun row -> row >= rowCount shown - 1)

let private leavesList (shown: TableView) (key: Key) =
    (key = KeyMap.historyRowUpKey && onFirstRow shown)
    || (key = KeyMap.historyRowDownKey && onLastRow shown)

let private tabStep (key: Key) =
    if key = KeyMap.nextHistoryTabKey || key = KeyMap.nextHistoryTabArrow then Some 1
    elif key = KeyMap.previousHistoryTabKey || key = KeyMap.previousHistoryTabArrow then Some -1
    else None

let private walking step (key: Key) (dialog: HistoryView) =
    key.Handled <- true
    walk step dialog

let private offersFiles (dialog: HistoryView) =
    front dialog = ViewHistoryTab && rowCount (table ViewHistoryTab dialog) > 0

let private openingAs how (key: Key) (dialog: HistoryView) =
    if offersFiles dialog then
        key.Handled <- true
        dialog.AskedUse <- Some how
        pressing OpenButton dialog

let private item entry (entries: Remembered<'item> list) =
    List.tryItem entry entries |> Option.map _.Item

let private itemOn (history: History) tab entry =
    match tab with
    | FolderHistoryTab -> item entry history.Folders |> Option.map Visited
    | RunHistoryTab -> item entry history.Launches |> Option.map Launched
    | ViewHistoryTab -> item entry history.FileVisits |> Option.map Opened

let private listOn tab =
    match tab with
    | RunHistoryTab -> LaunchHistory
    | ViewHistoryTab -> FileHistory
    | FolderHistoryTab -> FolderHistory

let private hasEntries (history: History) tab =
    match tab with
    | RunHistoryTab -> not history.Launches.IsEmpty
    | ViewHistoryTab -> not history.FileVisits.IsEmpty
    | FolderHistoryTab -> not history.Folders.IsEmpty

let private offer (container: Tabs) (goTo: Button) (delete: Button) =
    match container.Value with
    | :? TableView as shown ->
        let showsRows = rowCount shown > 0
        goTo.Enabled <- showsRows
        delete.Enabled <- showsRows
    | _ -> ()

let private offerButtons (dialog: HistoryView) =
    offer (tabs dialog) (button GoToButton dialog) (button DeleteButton dialog)

let private standOn row (shown: TableView) =
    let last = rowCount shown - 1

    if last >= 0 then
        shown.SetSelection(0, min row last, false, null)
        shown.EnsureCursorIsVisible()

let private filterText filter =
    match filter with
    | Some typed -> FilterCaption + SearchText.value typed
    | None -> ""

type private Changing =
    { Fs: IFileSystem
      Now: DateTimeOffset
      Confirm: string -> string -> bool
      Change: HistoryChange -> History option
      Tables: (HistoryTab * TableView) list
      Line: Label }

let private refill (changing: Changing) (dialog: HistoryView) =
    changing.Tables
    |> List.iter (fun (tab, shown) -> fill changing.Fs changing.Now dialog.History dialog.Filter tab shown)

    changing.Line.Text <- filterText dialog.Filter

let private changed (changing: Changing) (dialog: HistoryView) change =
    let shown = table (front dialog) dialog
    let row = rowUnderCursor shown |> Option.defaultValue 0

    changing.Change change
    |> Option.iter (fun history ->
        dialog.History <- history
        refill changing dialog
        standOn row shown
        offerButtons dialog
        dialog.SetNeedsDraw())

let private forgetting (changing: Changing) (dialog: HistoryView) =
    let tab = front dialog

    entryUnderCursor (table tab dialog)
    |> Option.bind (itemOn dialog.History tab)
    |> Option.iter (Forget >> changed changing dialog)

let private clearing (changing: Changing) (dialog: HistoryView) =
    let tab = front dialog

    if hasEntries dialog.History tab && changing.Confirm ClearTitle (clearQuestion tab) then
        changed changing dialog (ClearList(listOn tab))

let private filtering (changing: Changing) (dialog: HistoryView) filter =
    dialog.Filter <- filter
    refill changing dialog
    standOn 0 (table (front dialog) dialog)
    offerButtons dialog
    dialog.SetNeedsDraw()

let private typedInto filter character =
    match filter with
    | Some typed -> SearchText.append character typed
    | None -> SearchText.ofChar character

let private typingKey (changing: Changing) (dialog: HistoryView) (key: Key) =
    if KeyMap.printable key then
        key.Handled <- true
        filtering changing dialog (Some(typedInto dialog.Filter (char key.AsRune.Value)))

let private tableKey (changing: Changing) (dialog: HistoryView) (shown: TableView) (key: Key) =
    match tabStep key with
    | Some step -> walking step key dialog
    | None when leavesList shown key -> key.Handled <- true
    | None -> typingKey changing dialog key

let private dialogKey (changing: Changing) (dialog: HistoryView) (key: Key) =
    if key = KeyMap.historyGoToKey then
        key.Handled <- true

        if (button GoToButton dialog).Enabled then
            pressing GoToButton dialog
    elif key = KeyMap.historyViewKey then
        openingAs Viewed key dialog
    elif key = KeyMap.historyEditKey then
        openingAs Edited key dialog
    elif key = KeyMap.historyForgetKey then
        key.Handled <- true
        forgetting changing dialog
    elif key = KeyMap.historyClearKey then
        key.Handled <- true
        clearing changing dialog
    elif key = KeyMap.cancelKey && dialog.Filter.IsSome then
        key.Handled <- true
        filtering changing dialog None
    elif key = Key.Backspace then
        key.Handled <- true
        filtering changing dialog (dialog.Filter |> Option.bind SearchText.backspace)
    else
        typingKey changing dialog key

let private answerKey (changing: Changing) (dialog: HistoryView) (key: Key) =
    match tabStep key with
    | Some step -> walking step key dialog
    | None -> dialogKey changing dialog key

let private leavingTab (changing: Changing) (dialog: HistoryView) =
    if dialog.Filter.IsSome then
        dialog.Filter <- None
        refill changing dialog

let private plainButton title = DialogStyle.button title |> LiteralText.showing

let create
    (fs: IFileSystem)
    (confirm: string -> string -> bool)
    (change: HistoryChange -> History option)
    (history: History)
    (tab: HistoryTab)
    (now: DateTimeOffset)
    =
    let dialog = new HistoryView(history)
    DialogStyle.dress dialog |> ignore

    let container = new Tabs(X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(1))
    container.TabLineStyle <- LineStyle.Single
    let tables = tabOrder |> List.map (fun shown -> shown, tableFor shown)
    tables |> List.iter (fun (_, shown) -> container.Add shown |> ignore)

    let line =
        new Label(Id = FilterLineId, X = 0, Y = Pos.Bottom container, Width = Dim.Fill(), Height = 1)

    dialog.Add(container, line)

    let changing =
        { Fs = fs
          Now = now
          Confirm = confirm
          Change = change
          Tables = tables
          Line = line }

    refill changing dialog

    let close = plainButton "Close"
    let delete = plainButton "Delete"
    let goTo = plainButton "Go to"
    let opening = plainButton "Open"
    opening.IsDefault <- true
    [ close; delete; goTo; opening ] |> List.iter dialog.AddButton

    delete.Accepting.Add(fun args ->
        args.Handled <- true
        forgetting changing dialog)

    container.Value <- table tab dialog
    offerButtons dialog

    container.ValueChanged.Add(fun _ ->
        leavingTab changing dialog
        offer container goTo delete)

    container.FrameChanged.Add(fun _ -> dressHeaders container tables)
    tables |> List.iter (fun (_, shown) -> shown.KeyDown.Add(tableKey changing dialog shown))
    dialog.KeyDown.Add(answerKey changing dialog)
    dialog

let private revisited asked (visit: FileVisit) =
    Revisit { visit with How = Option.defaultValue visit.How asked }

let private chosen (dialog: HistoryView) tab pressed entry =
    let history = dialog.History

    match tab, pressed with
    | FolderHistoryTab, OpenButton
    | FolderHistoryTab, GoToButton -> item entry history.Folders |> Option.map OpenPlace
    | RunHistoryTab, OpenButton -> item entry history.Launches |> Option.map RunAgain
    | RunHistoryTab, GoToButton ->
        item entry history.Launches
        |> Option.map (fun started -> GoTo(Place.OnDisk started.Target))
    | ViewHistoryTab, OpenButton -> item entry history.FileVisits |> Option.map (revisited dialog.AskedUse)
    | ViewHistoryTab, GoToButton -> item entry history.FileVisits |> Option.map (_.Where >> GoTo)
    | _ -> None

let answer (dialog: HistoryView) =
    let tab = front dialog

    let choice =
        Option.ofNullable dialog.Result
        |> Option.bind (fun pressed -> entryUnderCursor (table tab dialog) |> Option.bind (chosen dialog tab pressed))

    { Tab = tab; Choice = choice }
