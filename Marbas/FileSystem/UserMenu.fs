namespace Marbas.FileSystem

open System
open System.IO.Abstractions
open FsToolkit.ErrorHandling
open Marbas.Domain

type MenuCommand =
    { Hotkey: MenuHotkey
      Label: MenuLabel
      Line: CommandLine
      Run: MenuRun
      Elevation: Elevation }

type MenuItem =
    | CommandItem of MenuCommand
    | SeparatorItem

type MenuItemRef =
    | CommandAt of hotkey: MenuHotkey
    | SeparatorAfter of anchor: MenuHotkey option * skipped: int

type MenuMove =
    | Earlier
    | Later

type MenuChange =
    | AddItem of item: MenuItem * after: MenuItemRef option
    | ChangeCommand of hotkey: MenuHotkey * command: MenuCommand
    | RemoveItem of item: MenuItemRef
    | MoveItem of item: MenuItemRef * move: MenuMove

type UserMenu = private { Items: MenuItem list }

module UserMenu =

    [<Literal>]
    let private FileName = "user-menu.conf"

    [<Literal>]
    let private EntryPrefix = "entry"

    [<Literal>]
    let private CommandKind = "command"

    [<Literal>]
    let private SeparatorKind = "separator"

    [<Literal>]
    let private HotkeyPart = "hotkey"

    [<Literal>]
    let private LabelPart = "label"

    [<Literal>]
    let private CommandPart = "command"

    [<Literal>]
    let private RunPart = "run"

    [<Literal>]
    let private ElevationPart = "elevation"

    let empty = { Items = [] }

    let items (menu: UserMenu) = menu.Items

    let beside (fs: IFileSystem) (settingsPath: FileSystemPath) =
        FileSystemPath.combine fs (FileSystemPath.directory fs settingsPath) FileName

    let private hotkeyOf item =
        match item with
        | CommandItem command -> Some command.Hotkey
        | SeparatorItem -> None

    let private commandIndex hotkey items =
        items |> List.tryFindIndex (fun item -> hotkeyOf item = Some hotkey)

    let private separatorsFrom start items =
        items |> List.skip start |> List.takeWhile ((=) SeparatorItem) |> List.length

    let private runStart anchor items =
        match anchor with
        | None -> Some 0
        | Some hotkey -> commandIndex hotkey items |> Option.map ((+) 1)

    let private indexOf reference items =
        match reference with
        | CommandAt hotkey -> commandIndex hotkey items
        | SeparatorAfter(anchor, skipped) ->
            runStart anchor items
            |> Option.filter (fun start -> skipped >= 0 && skipped < separatorsFrom start items)
            |> Option.map ((+) skipped)

    let refAt index (menu: UserMenu) =
        match List.tryItem index menu.Items with
        | None -> None
        | Some(CommandItem command) -> Some(CommandAt command.Hotkey)
        | Some SeparatorItem ->
            let above = menu.Items |> List.take index |> List.rev
            let skipped = above |> List.takeWhile ((=) SeparatorItem) |> List.length
            Some(SeparatorAfter(List.tryPick hotkeyOf above, skipped))

    let private placeAfter after items =
        match after with
        | None -> 0
        | Some reference ->
            indexOf reference items
            |> Option.map ((+) 1)
            |> Option.defaultValue (List.length items)

    let private adding item after items =
        match hotkeyOf item |> Option.filter (fun hotkey -> (commandIndex hotkey items).IsSome) with
        | Some taken -> Error(HotkeyTaken taken)
        | None -> Ok(List.insertAt (placeAfter after items) item items)

    let private changing hotkey (command: MenuCommand) items =
        match commandIndex hotkey items, commandIndex command.Hotkey items with
        | None, _ -> Error(CommandGone hotkey)
        | Some position, Some holder when holder <> position -> Error(HotkeyTaken command.Hotkey)
        | Some position, _ -> Ok(List.updateAt position (CommandItem command) items)

    let private removing reference items =
        indexOf reference items
        |> Option.map (fun position -> List.removeAt position items)
        |> Option.defaultValue items

    let private neighbour move position =
        match move with
        | Earlier -> position - 1
        | Later -> position + 1

    let private swapped one other (items: MenuItem list) =
        items |> List.updateAt one items[other] |> List.updateAt other items[one]

    let private moving reference move items =
        indexOf reference items
        |> Option.map (fun position -> position, neighbour move position)
        |> Option.filter (fun (_, target) -> target >= 0 && target < List.length items)
        |> Option.map (fun (position, target) -> swapped position target items)
        |> Option.defaultValue items

    let apply change (menu: UserMenu) =
        let changed =
            match change with
            | AddItem(item, after) -> adding item after menu.Items
            | ChangeCommand(hotkey, command) -> changing hotkey command menu.Items
            | RemoveItem reference -> Ok(removing reference menu.Items)
            | MoveItem(reference, move) -> Ok(moving reference move menu.Items)

        changed |> Result.map (fun items -> { Items = items })

    let private entryKey number = $"{EntryPrefix}.{number}"

    let private itemLines number item =
        let key = entryKey number

        match item with
        | SeparatorItem -> [ $"{key}={SeparatorKind}" ]
        | CommandItem command ->
            [ $"{key}={CommandKind}"
              $"{key}.{HotkeyPart}={MenuHotkey.value command.Hotkey}"
              $"{key}.{LabelPart}={MenuLabel.value command.Label}"
              $"{key}.{CommandPart}={CommandLine.value command.Line}"
              $"{key}.{RunPart}={MenuRun.text command.Run}"
              $"{key}.{ElevationPart}={Elevation.text command.Elevation}" ]

    let format (menu: UserMenu) =
        menu.Items |> List.mapi (fun index -> itemLines (index + 1)) |> List.concat

    let private keyAndValue (line: string) =
        match line.Split('=', 2) with
        | [| key; value |] -> Some(key.Trim().ToLowerInvariant(), value)
        | _ -> None

    let private parsedNumber (raw: string) =
        match Int32.TryParse raw with
        | true, number -> Some number
        | _ -> None

    let private entryNumbers (read: Map<string, string>) =
        let lead = EntryPrefix + "."

        read
        |> Map.keys
        |> Seq.filter _.StartsWith(lead)
        |> Seq.choose (fun key -> key.Substring(lead.Length).Split('.')[0] |> parsedNumber)
        |> Seq.distinct
        |> Seq.sort
        |> List.ofSeq

    let private commandFrom (part: string -> string option) =
        option {
            let! hotkey = part HotkeyPart |> Option.bind MenuHotkey.tryCreate
            let! label = part LabelPart |> Option.bind MenuLabel.tryCreate
            let! line = part CommandPart |> Option.bind CommandLine.tryCreate

            return
                { Hotkey = hotkey
                  Label = label
                  Line = line
                  Run = part RunPart |> Option.bind MenuRun.tryParse |> Option.defaultValue Directly
                  Elevation = part ElevationPart |> Option.bind Elevation.tryParse |> Option.defaultValue AsUser }
        }

    let private itemFrom (read: Map<string, string>) number =
        let key = entryKey number
        let part name = Map.tryFind $"{key}.{name}" read

        match Map.tryFind key read |> Option.map _.Trim().ToLowerInvariant() with
        | Some SeparatorKind -> Some SeparatorItem
        | Some CommandKind -> commandFrom part |> Option.map CommandItem
        | _ -> None

    let private keptFirst kept item =
        match hotkeyOf item with
        | Some hotkey when (commandIndex hotkey kept).IsSome -> kept
        | _ -> kept @ [ item ]

    let parse (lines: string list) =
        let read = lines |> List.choose keyAndValue |> Map.ofList

        { Items = entryNumbers read |> List.choose (itemFrom read) |> List.fold keptFirst [] }

    let load (fs: IFileSystem) (path: FileSystemPath) =
        let file = FileSystemPath.value path

        try
            if fs.File.Exists file then
                fs.File.ReadAllLines file |> List.ofArray |> parse |> Ok
            else
                Ok empty
        with ex ->
            Error(UserMenuError.mapException path ex)

    let private save (fs: IFileSystem) (path: FileSystemPath) menu =
        try
            fs.Directory.CreateDirectory(FileSystemPath.value (FileSystemPath.directory fs path))
            |> ignore

            fs.File.WriteAllLines(FileSystemPath.value path, format menu)
            Ok()
        with ex ->
            Error(UserMenuError.mapException path ex)

    let change (fs: IFileSystem) (path: FileSystemPath) change =
        result {
            let! current = load fs path
            let! changed = apply change current |> Result.mapError MenuRefused
            do! save fs path changed
            return changed
        }
