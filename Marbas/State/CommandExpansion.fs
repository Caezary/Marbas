module Marbas.State.CommandExpansion

open System
open System.IO.Abstractions
open FsToolkit.ErrorHandling
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.State.AppState

type ExpansionRefusal =
    | InsideArchive of place: FileSystemPath
    | NoEntryChosen of side: PanelSide
    | NoDirectory of side: PanelSide
    | NoProgram

type private Pick =
    | CursorName
    | CursorPath
    | ChosenPaths
    | PanelDirectory

type private Placeholder =
    | Picked of Pick * PanelSide
    | ListFilePath

type private Piece =
    | Typed of string
    | Slot of Placeholder

type private Word =
    { Pieces: Piece list
      Quoting: WordQuoting }

type private Quotes =
    | InsideQuotes
    | OutsideQuotes

type private Reading =
    { Done: Word list
      Current: Word option
      Quotes: Quotes }

type private Resolved =
    | AsTyped of string
    | AsInserted of string list

type private Building =
    { Finished: CommandWord list
      Growing: CommandWord }

let private (|SideLetter|_|) letter =
    match letter with
    | 'L' -> Some Left
    | 'R' -> Some Right
    | _ -> None

let private (|PickLetter|_|) letter =
    match letter with
    | 'n' -> Some CursorName
    | 'p' -> Some CursorPath
    | 's' -> Some ChosenPaths
    | 'd' -> Some PanelDirectory
    | _ -> None

let private started reading =
    reading.Current |> Option.defaultValue { Pieces = []; Quoting = Bare }

let private adding piece reading =
    let word = started reading

    { reading with
        Current =
            Some
                { word with
                    Pieces = piece :: word.Pieces } }

let private flipped quotes =
    match quotes with
    | InsideQuotes -> OutsideQuotes
    | OutsideQuotes -> InsideQuotes

let private toggled reading =
    { reading with
        Current =
            Some
                { started reading with
                    Quoting = Quoted }
        Quotes = flipped reading.Quotes }

let private ended reading =
    match reading.Current with
    | None -> reading
    | Some word ->
        { reading with
            Done =
                { word with
                    Pieces = List.rev word.Pieces }
                :: reading.Done
            Current = None }

let rec private read active reading chars =
    match chars, reading.Quotes with
    | [], _ -> (ended reading).Done |> List.rev
    | '"' :: rest, _ -> read active (toggled reading) rest
    | blank :: rest, OutsideQuotes when Char.IsWhiteSpace blank -> read active (ended reading) rest
    | '%' :: '%' :: rest, _ -> read active (adding (Typed "%") reading) rest
    | '%' :: 'f' :: rest, _ -> read active (adding (Slot ListFilePath) reading) rest
    | '%' :: SideLetter side :: PickLetter pick :: rest, _ ->
        read active (adding (Slot(Picked(pick, side))) reading) rest
    | '%' :: PickLetter pick :: rest, _ -> read active (adding (Slot(Picked(pick, active))) reading) rest
    | letter :: rest, _ -> read active (adding (Typed(string letter)) reading) rest

let private words active line =
    CommandLine.value line
    |> List.ofSeq
    |> read
        active
        { Done = []
          Current = None
          Quotes = OutsideQuotes }

let private archivePlaceOf (panel: PanelState) (entry: Entry) =
    match PanelState.hitAt panel entry with
    | Some hit -> FindHit.archivePlace hit
    | None -> PanelLocation.archivePlace panel.Path entry.Name

let private outsideArchive (fs: IFileSystem) panel entry =
    match archivePlaceOf panel entry with
    | Some(archive, inside) -> Error(InsideArchive(ArchivePath.spelled fs archive inside))
    | None -> Ok entry

let private ownEntry panel entry =
    PanelState.hitAt panel entry |> Option.map _.Entry |> Option.defaultValue entry

let private spelledEntry (fs: IFileSystem) panel (entry: Entry) =
    match PanelState.hitAt panel entry with
    | Some hit -> FindHit.spelled fs hit
    | None -> PanelLocation.spelledChild fs panel.Path entry.Name

let private entryPath fs panel entry =
    outsideArchive fs panel entry
    |> Result.map (spelledEntry fs panel >> FileSystemPath.value)

let private underCursor side panel =
    PanelState.cursorEntry panel
    |> Option.filter PanelState.selectable
    |> Result.requireSome (NoEntryChosen side)

let private chosen fs side panel =
    match PanelState.operationTargets panel with
    | [] -> Error(NoEntryChosen side)
    | targets -> targets |> List.traverseResultM (entryPath fs panel)

let private panelDirectory (fs: IFileSystem) side (panel: PanelState) =
    match panel.Path with
    | OnDisk directory -> Ok(FileSystemPath.value directory)
    | InArchive(archive, level) -> Error(InsideArchive(ArchivePath.spelled fs archive level))
    | SearchResults _ -> Error(NoDirectory side)

let private picked fs side panel pick =
    match pick with
    | CursorName ->
        underCursor side panel
        |> Result.bind (outsideArchive fs panel)
        |> Result.map (fun entry -> [ EntryName.value (ownEntry panel entry).Name ])
    | CursorPath ->
        underCursor side panel
        |> Result.bind (entryPath fs panel)
        |> Result.map List.singleton
    | ChosenPaths -> chosen fs side panel
    | PanelDirectory -> panelDirectory fs side panel |> Result.map List.singleton

let private resolve fs (listFile: FileSystemPath) (state: AppState) piece =
    match piece with
    | Typed text -> Ok(AsTyped text)
    | Slot ListFilePath -> Ok(AsInserted [ FileSystemPath.value listFile ])
    | Slot(Picked(pick, side)) -> picked fs side (panel side state) pick |> Result.map AsInserted

let private joined building text =
    { building with
        Growing =
            { Text = building.Growing.Text + text
              Quoting = Quoted } }

let private another building text =
    { Finished = building.Growing :: building.Finished
      Growing = { Text = text; Quoting = Quoted } }

let private appended building resolved =
    match resolved with
    | AsTyped text ->
        { building with
            Growing =
                { building.Growing with
                    Text = building.Growing.Text + text } }
    | AsInserted [] -> building
    | AsInserted(first :: rest) -> rest |> List.fold another (joined building first)

let private assembled (word: Word) resolved =
    let built =
        resolved
        |> List.fold
            appended
            { Finished = []
              Growing = { Text = ""; Quoting = word.Quoting } }

    built.Growing :: built.Finished |> List.rev

let private expandedWord fs listFile state (word: Word) =
    word.Pieces
    |> List.traverseResultM (resolve fs listFile state)
    |> Result.map (assembled word)

let private mentionsListFile (words: Word list) =
    words |> List.exists (fun word -> List.contains (Slot ListFilePath) word.Pieces)

let private listed fs listFile (state: AppState) words =
    if mentionsListFile words then
        chosen fs state.Active (activePanel state)
        |> Result.map (fun paths ->
            Some
                { FilePath = listFile
                  Listed = paths |> List.map FileSystemPath.create })
    else
        Ok None

let private commandOf listFile (expanded: CommandWord list) =
    match expanded with
    | program :: arguments when program.Text <> "" ->
        Ok
            { Program = program
              Arguments = arguments
              ListFile = listFile }
    | _ -> Error NoProgram

let expand (fs: IFileSystem) (listFile: FileSystemPath) (line: CommandLine) (state: AppState) =
    let read = words state.Active line

    result {
        let! expanded = read |> List.traverseResultM (expandedWord fs listFile state)
        let! written = listed fs listFile state read
        return! expanded |> List.concat |> commandOf written
    }
