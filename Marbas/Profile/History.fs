namespace Marbas.Profile

open System
open System.Globalization
open System.IO.Abstractions
open FsToolkit.ErrorHandling
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Programs

type FileUse =
    | Viewed
    | Edited

type FileVisit = { Where: Place; How: FileUse }

type InputKind =
    | CopyOrMoveTarget
    | NewDirectory
    | NewFile
    | SelectionPattern
    | FindMasks
    | SearchedText
    | SearchedBytes
    | ArchiveTarget
    | LinkDestination
    | OpenWithProgram

type Remembered<'item> = { Item: 'item; At: DateTimeOffset }

type Started =
    | StartedFile of LaunchRequest
    | StartedCommand of CommandRun

type HistoryItem =
    | Visited of Place
    | Launched of LaunchRequest
    | Ran of CommandRun
    | Opened of FileVisit
    | Typed of InputKind * EnteredText

type HistoryList =
    | FolderHistory
    | LaunchHistory
    | FileHistory
    | InputHistory of InputKind

type HistoryChange =
    | Remember of HistoryItem
    | Forget of HistoryItem
    | ClearList of HistoryList

type History =
    { Folders: Remembered<Place> list
      Launches: Remembered<Started> list
      FileVisits: Remembered<FileVisit> list
      Inputs: Map<InputKind, Remembered<EnteredText> list> }

module History =

    [<Literal>]
    let private Limit = 100

    [<Literal>]
    let private FileName = "history.conf"

    [<Literal>]
    let private FolderPrefix = "folder"

    [<Literal>]
    let private LaunchPrefix = "run"

    [<Literal>]
    let private FilePrefix = "view"

    [<Literal>]
    let private InputPrefix = "input"

    [<Literal>]
    let private AtPart = "at"

    [<Literal>]
    let private InsidePart = "inside"

    [<Literal>]
    let private ProgramPart = "program"

    [<Literal>]
    let private DirectoryPart = "directory"

    [<Literal>]
    let private ElevationPart = "elevation"

    [<Literal>]
    let private UsePart = "use"

    [<Literal>]
    let private KindPart = "kind"

    [<Literal>]
    let private CommandKind = "command"

    [<Literal>]
    let private CommandPart = "command"

    [<Literal>]
    let private QuotingPart = "quoting"

    [<Literal>]
    let private ArgumentPart = "argument"

    [<Literal>]
    let private ListPart = "list"

    [<Literal>]
    let private ModePart = "mode"

    [<Literal>]
    let private MomentFormat = "o"

    let empty =
        { Folders = []
          Launches = []
          FileVisits = []
          Inputs = Map.empty }

    let inputs kind (history: History) =
        Map.tryFind kind history.Inputs |> Option.defaultValue []

    let beside (fs: IFileSystem) (settingsPath: FileSystemPath) =
        FileSystemPath.combine fs (FileSystemPath.directory fs settingsPath) FileName

    let startedItem started =
        match started with
        | StartedFile request -> Launched request
        | StartedCommand run -> Ran run

    let private sameStart platform (one: Started) (other: Started) =
        match one, other with
        | StartedFile one, StartedFile other -> FileSystemPath.sameEntry platform one.Target other.Target
        | StartedCommand one, StartedCommand other -> one = other
        | _ -> false

    let private spelledRun (run: CommandRun) =
        MenuLabel.value run.Label
        :: FileSystemPath.value run.WorkingDirectory
        :: (ExpandedCommand.words run.Command |> List.map _.Text)
        @ (run.Command.ListFile
           |> Option.map (fun list -> list.FilePath :: list.Listed |> List.map FileSystemPath.value)
           |> Option.defaultValue [])

    let private sameVisit platform (one: FileVisit) (other: FileVisit) =
        Place.same platform one.Where other.Where

    let private spelled item =
        match item with
        | Visited place -> Place.spelled place
        | Launched request ->
            [ FileSystemPath.value request.Target
              FileSystemPath.value request.WorkingDirectory ]
            @ (request.Program |> Option.map ProgramPath.value |> Option.toList)
        | Ran run -> spelledRun run
        | Opened visit -> Place.spelled visit.Where
        | Typed(_, text) -> [ EnteredText.value text ]

    let private breaksALine (text: string) = text.IndexOfAny [| '\n'; '\r' |] >= 0

    let private writable item =
        spelled item |> List.exists breaksALine |> not

    let private without same item (entries: Remembered<'item> list) =
        entries |> List.filter (fun entry -> not (same entry.Item item))

    let private onTop same at item entries =
        { Item = item; At = at } :: without same item entries |> List.truncate Limit

    let private withInputs kind entries (history: History) =
        match entries with
        | [] ->
            { history with
                Inputs = Map.remove kind history.Inputs }
        | kept ->
            { history with
                Inputs = Map.add kind kept history.Inputs }

    let private remembering platform at item (history: History) =
        match item with
        | Visited place ->
            { history with
                Folders = onTop (Place.same platform) at place history.Folders }
        | Launched request ->
            { history with
                Launches = onTop (sameStart platform) at (StartedFile request) history.Launches }
        | Ran run ->
            { history with
                Launches = onTop (sameStart platform) at (StartedCommand run) history.Launches }
        | Opened visit ->
            { history with
                FileVisits = onTop (sameVisit platform) at visit history.FileVisits }
        | Typed(kind, text) -> withInputs kind (onTop (=) at text (inputs kind history)) history

    let private forgetting platform item (history: History) =
        match item with
        | Visited place ->
            { history with
                Folders = without (Place.same platform) place history.Folders }
        | Launched request ->
            { history with
                Launches = without (sameStart platform) (StartedFile request) history.Launches }
        | Ran run ->
            { history with
                Launches = without (sameStart platform) (StartedCommand run) history.Launches }
        | Opened visit ->
            { history with
                FileVisits = without (sameVisit platform) visit history.FileVisits }
        | Typed(kind, text) -> withInputs kind (without (=) text (inputs kind history)) history

    let private clearing list (history: History) =
        match list with
        | FolderHistory -> { history with Folders = [] }
        | LaunchHistory -> { history with Launches = [] }
        | FileHistory -> { history with FileVisits = [] }
        | InputHistory kind -> withInputs kind [] history

    let apply platform at change history =
        match change with
        | Remember item when writable item -> remembering platform at item history
        | Remember _ -> history
        | Forget item -> forgetting platform item history
        | ClearList list -> clearing list history

    let private kinds =
        [ CopyOrMoveTarget
          NewDirectory
          NewFile
          SelectionPattern
          FindMasks
          SearchedText
          SearchedBytes
          ArchiveTarget
          LinkDestination
          OpenWithProgram ]

    let private kindKey kind =
        match kind with
        | CopyOrMoveTarget -> "target"
        | NewDirectory -> "mkdir"
        | NewFile -> "newfile"
        | SelectionPattern -> "pattern"
        | FindMasks -> "find.masks"
        | SearchedText -> "find.text"
        | SearchedBytes -> "bytes"
        | ArchiveTarget -> "archive"
        | LinkDestination -> "link"
        | OpenWithProgram -> "program"

    let private inputPrefix kind = $"{InputPrefix}.{kindKey kind}"

    let private useText how =
        match how with
        | Viewed -> "viewed"
        | Edited -> "edited"

    let private useOf (text: string) =
        match text.ToLowerInvariant() with
        | "viewed" -> Some Viewed
        | "edited" -> Some Edited
        | _ -> None

    let private momentText (at: DateTimeOffset) =
        at.ToString(MomentFormat, CultureInfo.InvariantCulture)

    let private momentOf (text: string) =
        match DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None) with
        | true, at -> Some at
        | _ -> None

    type private Described =
        { Value: string
          Parts: (string * string) list }

    let private describedPlace place =
        match place with
        | Place.OnDisk path ->
            { Value = FileSystemPath.value path
              Parts = [] }
        | Place.InArchive(archive, inside) ->
            { Value = FileSystemPath.value archive
              Parts = [ InsidePart, ArchivePath.value inside ] }

    let private describedLaunch (request: LaunchRequest) =
        { Value = FileSystemPath.value request.Target
          Parts =
            (request.Program
             |> Option.map (fun program -> ProgramPart, ProgramPath.value program)
             |> Option.toList)
            @ [ DirectoryPart, FileSystemPath.value request.WorkingDirectory
                ElevationPart, Elevation.text request.Elevation ] }

    let private quotingText quoting =
        match quoting with
        | Bare -> "bare"
        | Quoted -> "quoted"

    let private quotingOf (text: string) =
        match text.Trim().ToLowerInvariant() with
        | "quoted" -> Quoted
        | _ -> Bare

    let private wordParts part (word: CommandWord) =
        [ part, word.Text; $"{part}.{QuotingPart}", quotingText word.Quoting ]

    let private listParts listFile =
        match listFile with
        | None -> []
        | Some list ->
            (ListPart, FileSystemPath.value list.FilePath)
            :: (list.Listed
                |> List.mapi (fun index path -> $"{ListPart}.{index + 1}", FileSystemPath.value path))

    let private describedRun (run: CommandRun) =
        { Value = MenuLabel.value run.Label
          Parts =
            [ KindPart, CommandKind ]
            @ wordParts CommandPart run.Command.Program
            @ (run.Command.Arguments
               |> List.mapi (fun index -> wordParts $"{ArgumentPart}.{index + 1}")
               |> List.concat)
            @ listParts run.Command.ListFile
            @ [ ModePart, MenuRun.text run.Run
                DirectoryPart, FileSystemPath.value run.WorkingDirectory
                ElevationPart, Elevation.text run.Elevation ] }

    let private describedStart started =
        match started with
        | StartedFile request -> describedLaunch request
        | StartedCommand run -> describedRun run

    let private describedVisit visit =
        let place = describedPlace visit.Where

        { place with
            Parts = place.Parts @ [ UsePart, useText visit.How ] }

    let private describedText text =
        { Value = EnteredText.value text
          Parts = [] }

    let private entryKey prefix number = $"{prefix}.{number}"

    let private entryLines prefix describe number (entry: Remembered<'item>) =
        let key = entryKey prefix number
        let described = describe entry.Item

        $"{key}={described.Value}"
        :: (described.Parts @ [ AtPart, momentText entry.At ]
            |> List.map (fun (part, value) -> $"{key}.{part}={value}"))

    let private numbered prefix describe entries =
        entries
        |> List.mapi (fun index -> entryLines prefix describe (index + 1))
        |> List.concat

    let format (history: History) =
        numbered FolderPrefix describedPlace history.Folders
        @ numbered LaunchPrefix describedStart history.Launches
        @ numbered FilePrefix describedVisit history.FileVisits
        @ (kinds
           |> List.collect (fun kind -> numbered (inputPrefix kind) describedText (inputs kind history)))

    let private keyAndValue (line: string) =
        match line.Split('=', 2) with
        | [| key; value |] -> Some(key.Trim().ToLowerInvariant(), value)
        | _ -> None

    let private parsedNumber (raw: string) =
        match Int32.TryParse raw with
        | true, number -> Some number
        | _ -> None

    let private numbersUnder (read: Map<string, string>) prefix =
        let lead = prefix + "."

        read
        |> Map.keys
        |> Seq.filter _.StartsWith(lead, StringComparison.Ordinal)
        |> Seq.choose (fun key -> key.Substring(lead.Length).Split('.')[0] |> parsedNumber)
        |> Seq.distinct
        |> Seq.sort
        |> List.ofSeq

    type private Stored =
        { Main: string option
          Part: string -> string option
          Numbered: string -> int list }

    let private storedEntry (read: Map<string, string>) prefix number =
        let key = entryKey prefix number

        { Main = Map.tryFind key read |> Option.filter (String.IsNullOrEmpty >> not)
          Part = fun part -> Map.tryFind $"{key}.{part}" read
          Numbered = fun part -> numbersUnder read $"{key}.{part}" }

    let private placeFrom (stored: Stored) =
        stored.Main
        |> Option.bind (fun value ->
            let path = FileSystemPath.create value

            match stored.Part InsidePart with
            | None -> Some(Place.OnDisk path)
            | Some inside ->
                ArchivePath.tryCreate inside
                |> Option.map (fun level -> Place.InArchive(path, level)))

    let private launchFrom (stored: Stored) =
        option {
            let! target = stored.Main

            let! directory =
                stored.Part DirectoryPart |> Option.filter (String.IsNullOrEmpty >> not)

            let! elevation = stored.Part ElevationPart |> Option.bind Elevation.tryParse

            return
                { Target = FileSystemPath.create target
                  Program = stored.Part ProgramPart |> Option.bind ProgramPath.tryCreate
                  WorkingDirectory = FileSystemPath.create directory
                  Elevation = elevation }
        }

    let private filled (text: string option) =
        text |> Option.filter (String.IsNullOrEmpty >> not)

    let private wordFrom (stored: Stored) part =
        stored.Part part
        |> Option.map (fun text ->
            { Text = text
              Quoting =
                stored.Part $"{part}.{QuotingPart}"
                |> Option.map quotingOf
                |> Option.defaultValue Bare })

    let private argumentsFrom (stored: Stored) =
        stored.Numbered ArgumentPart
        |> List.choose (fun number -> wordFrom stored $"{ArgumentPart}.{number}")

    let private listFileFrom (stored: Stored) =
        filled (stored.Part ListPart)
        |> Option.map (fun path ->
            { FilePath = FileSystemPath.create path
              Listed =
                stored.Numbered ListPart
                |> List.choose (fun number -> filled (stored.Part $"{ListPart}.{number}"))
                |> List.map FileSystemPath.create })

    let private runFrom (stored: Stored) =
        option {
            let! label = stored.Main |> Option.bind MenuLabel.tryCreate

            let! program =
                wordFrom stored CommandPart |> Option.filter (fun word -> word.Text <> "")

            let! run = stored.Part ModePart |> Option.bind MenuRun.tryParse
            let! directory = filled (stored.Part DirectoryPart)
            let! elevation = stored.Part ElevationPart |> Option.bind Elevation.tryParse

            return
                StartedCommand
                    { Label = label
                      Command =
                        { Program = program
                          Arguments = argumentsFrom stored
                          ListFile = listFileFrom stored }
                      Run = run
                      Elevation = elevation
                      WorkingDirectory = FileSystemPath.create directory }
        }

    let private startedFrom (stored: Stored) =
        match stored.Part KindPart |> Option.map _.Trim().ToLowerInvariant() with
        | Some CommandKind -> runFrom stored
        | Some _ -> None
        | None -> launchFrom stored |> Option.map StartedFile

    let private visitFrom (stored: Stored) =
        option {
            let! where = placeFrom stored
            let! how = stored.Part UsePart |> Option.bind useOf
            return { Where = where; How = how }
        }

    let private textFrom (stored: Stored) =
        stored.Main |> Option.bind EnteredText.tryCreate

    let private rememberedFrom read prefix itemFrom number =
        let entry = storedEntry read prefix number

        option {
            let! item = itemFrom entry
            let! at = entry.Part AtPart |> Option.bind momentOf
            return { Item = item; At = at }
        }

    let private listFrom read prefix itemFrom =
        numbersUnder read prefix
        |> List.choose (rememberedFrom read prefix itemFrom)
        |> List.truncate Limit

    let parse (lines: string list) =
        let read = lines |> List.choose keyAndValue |> Map.ofList

        { Folders = listFrom read FolderPrefix placeFrom
          Launches = listFrom read LaunchPrefix startedFrom
          FileVisits = listFrom read FilePrefix visitFrom
          Inputs =
            kinds
            |> List.fold (fun inputs kind -> Map.add kind (listFrom read (inputPrefix kind) textFrom) inputs) Map.empty
            |> Map.filter (fun _ entries -> not (List.isEmpty entries)) }

    let load (fs: IFileSystem) (path: FileSystemPath) =
        let file = FileSystemPath.value path

        try
            if fs.File.Exists file then
                fs.File.ReadAllLines file |> List.ofArray |> parse |> Ok
            else
                Ok empty
        with ex ->
            Error(HistoryError.mapException path ex)

    let private save (fs: IFileSystem) (path: FileSystemPath) history =
        try
            fs.Directory.CreateDirectory(FileSystemPath.value (FileSystemPath.directory fs path))
            |> ignore

            fs.File.WriteAllLines(FileSystemPath.value path, format history)
            Ok()
        with ex ->
            Error(HistoryError.mapException path ex)

    let change (fs: IFileSystem) (path: FileSystemPath) at change =
        result {
            let! current = load fs path
            let changed = apply (FileSystemPath.platformOf fs) at change current
            do! save fs path changed
            return changed
        }
