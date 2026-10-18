module Marbas.State.Ports

open System
open System.IO.Abstractions
open Marbas.Collections
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Programs
open Marbas.Profile
open Marbas.Locations
open Marbas.Viewing
open Marbas.Find
open Marbas.State
open Marbas.State.AppState

type ErrorReport = { Title: string; Message: string }

type Confirmation = { Title: string; Question: string }

type OverwritePrompt = { Target: FileSystemPath }

type PatternPrompt =
    { Title: string
      Remembered: EnteredText list }

type NamePrompt =
    { Title: string
      Suggestion: EnteredText option
      Remembered: EnteredText list }

type TargetPrompt =
    { Title: string
      Suggestion: FileSystemPath
      Remembered: EnteredText list }

type PasswordPrompt = { Archive: EntryName }

type PackPrompt =
    { Suggestion: FileSystemPath
      Remembered: EnteredText list }

type FindPrompt =
    { Ground: MarkedGround
      Hidden: HiddenEntries
      Found: int option
      Masks: EnteredText list
      Texts: EnteredText list }

type FindAsked =
    { Choice: FindChoice
      Masks: string
      Text: string }

type TypedLinkRefusal = string -> LinkKind -> LinkRefusal option

type LinkPrompt =
    { Kinds: LinkKind list
      Suggestion: FileSystemPath
      Refusal: TypedLinkRefusal
      Remembered: EnteredText list }

type HandlerPrompt =
    { Name: EntryName
      Handlers: Handler list
      Chosen: ProgramPath option
      Remembered: EnteredText list }

type ProgramsPrompt =
    { Programs: ProgramSettings
      Candidate: ProgramPath option }

type DrivePrompt =
    { Side: PanelSide
      Drives: DriveRow list }

type FavoritesAfter = FavoriteChange -> Favorites option

type FavoritesPrompt =
    { Favorites: Favorites
      Current: Place option
      Change: FavoritesAfter }

type UserMenuAfter = MenuChange -> UserMenu option

type UserMenuPrompt =
    { Menu: UserMenu
      Change: UserMenuAfter }

type HistoryAfter = HistoryChange -> History option

type HistoryPrompt =
    { History: History
      Tab: HistoryTab
      Now: DateTimeOffset
      Change: HistoryAfter }

type SearchHistory =
    { Texts: unit -> EnteredText list
      Remember: EnteredText -> unit }

type ViewRequest =
    { Source: FileWindow.Source
      Searches: SearchHistory
      ByteSearches: SearchHistory }

type Prompts =
    { ReportError: ErrorReport -> unit
      Confirm: Confirmation -> bool
      AskOverwrite: OverwritePrompt -> Operations.OverwriteAnswer
      AskPattern: PatternPrompt -> string option
      AskName: NamePrompt -> string option
      AskTarget: TargetPrompt -> string option
      AskPassword: PasswordPrompt -> ArchivePassword option
      AskArchive: PackPrompt -> ArchiveChoice option
      AskFind: FindPrompt -> FindAsked option
      AskLink: LinkPrompt -> LinkChoice option
      AskHandler: HandlerPrompt -> OpenWithChoice option
      ChooseDrive: DrivePrompt -> DriveChoice option
      ChooseTab: Zipper<Tab> -> int option
      ChooseHistory: HistoryPrompt -> HistoryAnswer
      AskFavoriteSlot: Place -> FavoriteSlot option
      ChooseFavorite: FavoritesPrompt -> Place option
      ChooseFromUserMenu: UserMenuPrompt -> MenuCommand option
      EditProperties: EntryProperties -> EntryProperties option
      EditPrograms: ProgramsPrompt -> ProgramSettings option
      ShowFile: ViewRequest -> unit }

type Processes =
    { ListDrives: unit -> DriveRow list
      SendToTrash: FileSystemPath -> Result<unit, Operations.OperationError>
      StartEditor: ProgramPath option -> FileSystemPath -> Result<unit, Operations.OperationError>
      Launch: LaunchRequest -> Result<unit, LaunchError>
      RunCommand: CommandRun -> Result<unit, LaunchError>
      LinkKinds: LinkKind list
      RefuseLink: LinkRequest -> LinkRefusal option
      MakeLink: LinkRequest -> Result<unit, Operations.OperationError>
      HandlersFor: EntryName -> Handler list
      PathDirectories: FileSystemPath list
      EnvironmentVariable: string -> string option
      Owner: SessionOwner
      IsRunning: SessionOwner -> bool }

type Clipboard =
    { SetText: ClipboardText -> Result<unit, ClipboardError>
      GetText: unit -> Result<string option, ClipboardError>
      SetFiles: ClipboardFiles -> Result<unit, ClipboardError>
      GetFiles: unit -> Result<ClipboardFiles option, ClipboardError>
      Empty: unit -> Result<unit, ClipboardError> }

type ModifiersWatch = { Report: Set<Modifier> -> unit }

type LocationsWatch =
    { Paths: FileSystemPath list
      Notify: unit -> unit }

type Watches =
    { Modifiers: ModifiersWatch -> unit
      Locations: LocationsWatch -> unit }

type BatchRequest =
    { Title: string
      Work: Operations.BatchControl -> Operations.ItemResult list
      Finish: Operations.ItemResult list -> unit }

type MeasureRequest =
    { Title: string
      Measure: Operations.BatchControl -> Measurement list
      Finish: Result<Measurement list, Operations.OperationError> -> unit }

type SearchRequest =
    { Run: SearchRun
      View: FindHit -> unit
      Finish: FindAnswer -> unit }

type BackgroundRequest = { Work: unit -> unit }

type Runners =
    { Batch: BatchRequest -> unit
      Measure: MeasureRequest -> unit
      Search: SearchRequest -> unit
      Background: BackgroundRequest -> unit }

type Dependencies =
    { FileSystem: IFileSystem
      StartPath: FileSystemPath
      SettingsPath: FileSystemPath
      HistoryPath: FileSystemPath
      FavoritesPath: FileSystemPath
      UserMenuPath: FileSystemPath
      WorkspacePath: FileSystemPath
      Clock: unit -> DateTimeOffset
      OnQuit: unit -> unit
      Log: LogEntry -> unit
      Prompts: Prompts
      Processes: Processes
      Clipboard: Clipboard
      Watches: Watches
      Runners: Runners }
