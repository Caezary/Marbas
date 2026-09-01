module Marbas.Tests.Fakes

open System
open System.IO
open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Marbas.FileSystem
open Marbas.Ui.App
open Marbas.Tests

let private at = Root.at

[<Literal>]
let DiskFullReason = "There is not enough space on the disk."

type private DeniedDirectory(accessor: IMockFileDataAccessor, current: string, denied: string) =
    inherit MockDirectory(accessor, current)

    override this.EnumerateFileSystemEntries(path: string) =
        if path = denied then
            raise (UnauthorizedAccessException $"Access to the path '{path}' is denied.")
        else
            base.EnumerateFileSystemEntries path

type UnreadableDirectory(denied: string) =
    inherit MockFileSystem()

    let mutable listing: IDirectory = null

    override this.Directory =
        if isNull listing then
            listing <- DeniedDirectory(this, base.Directory.GetCurrentDirectory(), denied)

        listing

type private UncreatableDirectory(accessor: IMockFileDataAccessor, current: string, denied: string) =
    inherit MockDirectory(accessor, current)

    override this.CreateDirectory(path: string) =
        if path = denied then
            raise (UnauthorizedAccessException $"Access to the path '{path}' is denied.")
        else
            base.CreateDirectory path

type UnwritableDirectory(denied: string) =
    inherit MockFileSystem()

    let mutable making: IDirectory = null

    override this.Directory =
        if isNull making then
            making <- UncreatableDirectory(this, base.Directory.GetCurrentDirectory(), denied)

        making

type private RefusedStream(path: string) =
    inherit FileSystemStream(new MemoryStream(), path, false)

    override _.Write(_: byte array, _: int, _: int) = raise (IOException DiskFullReason)

    override _.Write(_: ReadOnlySpan<byte>) = raise (IOException DiskFullReason)

type private RefusingFile(accessor: IMockFileDataAccessor, denied: string option) =
    inherit MockFile(accessor)

    override this.Create(path: string) =
        if denied |> Option.forall ((=) path) then
            (base.Create path).Dispose()
            new RefusedStream(path)
        else
            base.Create path

type FullDisk(denied: string option) =
    inherit MockFileSystem()

    let mutable writing: IFile = null

    new() = FullDisk None

    new(path: string) = FullDisk(Some path)

    override this.File =
        if isNull writing then
            writing <- RefusingFile(this, denied)

        writing

let dependencies (fs: IFileSystem) (startPath: string) : Shell.Dependencies =
    { FileSystem = fs
      StartPath = FileSystemPath.create startPath
      SettingsPath = FileSystemPath.create (at "marbas.conf")
      WorkspacePath = FileSystemPath.create (at "temp/Marbas/session")
      Roots = MountedDrives
      OnQuit = ignore
      ReportError = fun _ _ -> ()
      ChooseOption = fun _ _ -> None
      AskPattern = fun _ -> None
      AskName = fun _ _ -> None
      AskTarget = fun _ _ -> None
      Confirm = fun _ _ -> false
      SendToTrash = fun _ -> Ok()
      ShowFile = ignore
      RunEditor = fun _ _ -> Ok()
      EditProperties = fun _ -> None
      EditPrograms = fun _ _ -> None
      AskArchive = (fun _ -> None)
      AskPassword = (fun _ _ -> None)
      AskOverwrite = (fun _ _ -> Operations.No)
      WatchModifiers = ignore
      RunBatch = fun _ work finish -> work Operations.unattended |> finish }
