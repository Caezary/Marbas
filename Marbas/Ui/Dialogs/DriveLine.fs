module Marbas.Ui.Dialogs.DriveLine

open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.Format

[<Literal>]
let HomeKind = "home"

[<Literal>]
let private Gap = "  "

let kindText kind =
    match kind with
    | Fixed -> "fixed"
    | Removable -> "removable"
    | Network -> "network"
    | Optical -> "optical"

type private Cells =
    { Root: string
      Label: string
      Kind: string
      Size: string
      Free: string }

let private cellsOf row =
    match row with
    | MountedDrive drive ->
        { Root = FileSystemPath.value drive.Root
          Label = drive.Label |> Option.defaultValue ""
          Kind = kindText drive.Kind
          Size = CompactSize.describe drive.Size
          Free = CompactSize.describe drive.Free + " free" }
    | HomeDirectory home ->
        { Root = FileSystemPath.value home
          Label = ""
          Kind = HomeKind
          Size = ""
          Free = "" }

let private widest (column: Cells -> string) (cells: Cells list) =
    cells |> List.map (column >> String.length) |> List.fold max 0

let describe (rows: DriveRow list) =
    let cells = rows |> List.map cellsOf
    let labelled = cells |> List.exists (fun each -> each.Label <> "")

    let line (each: Cells) =
        [ yield each.Root.PadRight(widest _.Root cells)
          if labelled then
              yield each.Label.PadRight(widest _.Label cells)
          yield each.Kind.PadRight(widest _.Kind cells)
          yield each.Size.PadLeft(widest _.Size cells)
          yield each.Free.PadLeft(widest _.Free cells) ]
        |> String.concat Gap
        |> _.TrimEnd()

    cells |> List.map line
