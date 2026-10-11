module Marbas.Tests.Integration.Dialogs.HistoryDialogFixture

open System
open System.Drawing
open System.IO.Abstractions.TestingHelpers
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.Theme
open Marbas.Ui.Dialogs
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private now = DateTimeOffset(2026, 9, 28, 15, 30, 0, TimeSpan.FromHours 2.0)

let minute m =
    DateTimeOffset(2026, 9, 28, 14, m, 0, TimeSpan.FromHours 2.0)

let folders =
    History.empty
    |> History.apply Windows (minute 1) (Remember(Visited(Place.OnDisk(fp (at "older")))))
    |> History.apply Windows (minute 2) (Remember(Visited(Place.OnDisk(fp (at "newer")))))

type HistoryFile(start: History) =
    let mutable held = start

    member _.Held = held

    member val Changes = ResizeArray<HistoryChange>()

    member this.Change(change: HistoryChange) =
        this.Changes.Add change
        held <- History.apply Windows (minute 59) change held
        Some held

let agreeing (_: string) (_: string) = true

let openedWith confirm change history tab =
    DialogStyle.apply ()

    let dialog = HistoryDialog.create (MockFileSystem()) confirm change history tab now

    dialog.BeginInit()
    dialog.EndInit()
    dialog.Layout(Size(100, 30)) |> ignore
    dialog

let openedOver confirm (file: HistoryFile) tab =
    openedWith confirm file.Change file.Held tab

let laidOut history tab =
    openedOver agreeing (HistoryFile history) tab

let rows (dialog: HistoryDialog.HistoryView) tab =
    let table = (HistoryDialog.table tab dialog).Table
    [ for row in 0 .. table.Rows - 1 -> $"{table[row, 0]} {table[row, 1]}" ]

let started target =
    { Target = fp (at target)
      Program = ProgramPath.tryCreate "code.exe"
      WorkingDirectory = fp (at "work")
      Elevation = AsAdministrator }

let launches =
    History.empty
    |> History.apply Windows (minute 3) (Remember(Launched(started "work/older.md")))
    |> History.apply Windows (minute 4) (Remember(Launched(started "work/newer.md")))

let cells (dialog: HistoryDialog.HistoryView) tab =
    let table = (HistoryDialog.table tab dialog).Table
    [ for row in 0 .. table.Rows - 1 -> [ for column in 0 .. table.Columns - 1 -> string table[row, column] ] ]

let visits =
    History.empty
    |> History.apply
        Windows
        (minute 5)
        (Remember(
            Opened
                { Where = Place.OnDisk(fp (at "work/seen.md"))
                  How = Viewed }
        ))
    |> History.apply
        Windows
        (minute 6)
        (Remember(
            Opened
                { Where = Place.OnDisk(fp (at "work/typed.md"))
                  How = Edited }
        ))

let visitOf path how =
    { Where = Place.OnDisk(fp (at path))
      How = how }

let threeFolders =
    folders
    |> History.apply Windows (minute 3) (Remember(Visited(Place.OnDisk(fp (at "newest")))))
