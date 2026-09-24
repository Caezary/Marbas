module Marbas.Ui.App.Ports

open System.IO.Abstractions
open Marbas.Collections
open Marbas.Domain
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.Ui.Keys

type ErrorReport = { Title: string; Message: string }

type Confirmation = { Title: string; Question: string }

type OverwritePrompt = { Target: FileSystemPath }

type PatternPrompt = { Title: string }

type NamePrompt =
    { Title: string
      Suggestion: EntryName option }

type TargetPrompt =
    { Title: string
      Suggestion: FileSystemPath }

type PasswordPrompt = { Archive: EntryName }

type PackPrompt = { Suggestion: FileSystemPath }

type FindPrompt =
    { Ground: MarkedGround
      Hidden: HiddenEntries
      Found: int option }

type LinkPrompt =
    { Kinds: LinkKind list
      Suggestion: FileSystemPath
      Refusal: string -> LinkKind -> LinkRefusal option }

type HandlerPrompt =
    { Name: EntryName
      Handlers: Handler list
      Chosen: ProgramPath option }

type ProgramsPrompt =
    { Programs: ProgramSettings
      Candidate: ProgramPath option }

type Prompts =
    { ReportError: ErrorReport -> unit
      Confirm: Confirmation -> bool
      AskOverwrite: OverwritePrompt -> Operations.OverwriteAnswer
      AskPattern: PatternPrompt -> NamePattern option
      AskName: NamePrompt -> EntryName option
      AskTarget: TargetPrompt -> string option
      AskPassword: PasswordPrompt -> ArchivePassword option
      AskArchive: PackPrompt -> ArchiveChoice option
      AskFind: FindPrompt -> FindChoice option
      AskLink: LinkPrompt -> LinkChoice option
      AskHandler: HandlerPrompt -> OpenWithChoice option
      ChooseDrive: DriveRow list -> DriveChoice option
      ChooseTab: Zipper<Tab> -> int option
      EditProperties: EntryProperties -> EntryProperties option
      EditPrograms: ProgramsPrompt -> ProgramSettings option
      ShowFile: FileWindow.Source -> unit }

type Processes =
    { ListDrives: unit -> DriveRow list
      SendToTrash: FileSystemPath -> Result<unit, Operations.OperationError>
      RunEditor: ProgramPath option -> FileSystemPath -> Result<unit, Operations.OperationError>
      Launch: LaunchRequest -> Result<unit, LaunchError>
      LinkKinds: LinkKind list
      RefuseLink: LinkRequest -> LinkRefusal option
      MakeLink: LinkRequest -> Result<unit, Operations.OperationError>
      HandlersFor: EntryName -> Handler list
      PathDirectories: FileSystemPath list }

type Watches =
    { Modifiers: (Set<Modifier> -> unit) -> unit
      Locations: FileSystemPath list -> (unit -> unit) -> unit }

type Runners =
    { Batch:
        string -> (Operations.BatchControl -> Operations.ItemResult list) -> (Operations.ItemResult list -> unit) -> unit
      Measure:
          string
              -> (Operations.BatchControl -> Measurement list)
              -> (Result<Measurement list, Operations.OperationError> -> unit)
              -> unit
      Search: SearchRun -> (FindHit -> unit) -> (FindAnswer -> unit) -> unit }

type Dependencies =
    { FileSystem: IFileSystem
      StartPath: FileSystemPath
      SettingsPath: FileSystemPath
      WorkspacePath: FileSystemPath
      OnQuit: unit -> unit
      Prompts: Prompts
      Processes: Processes
      Watches: Watches
      Runners: Runners }
