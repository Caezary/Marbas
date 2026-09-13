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

[<Literal>]
let DeepestFollowedPath = 40

type private LinkFollowingDirectory(fileSystem: IFileSystem, accessor: IMockFileDataAccessor, current: string) =
    inherit MockDirectory(accessor, current)

    let rec resolve (path: string) =
        match Path.GetDirectoryName path with
        | null
        | "" -> path
        | parent ->
            let candidate = Path.Combine(resolve parent, Path.GetFileName path)

            match fileSystem.FileInfo.New(candidate).LinkTarget with
            | null -> candidate
            | target -> resolve target

    let depth (path: string) =
        path.Split([| Path.DirectorySeparatorChar |], StringSplitOptions.RemoveEmptyEntries).Length

    override this.Exists(path: string) = base.Exists(resolve path)

    override this.EnumerateFileSystemEntries(path: string) =
        if depth path > DeepestFollowedPath then
            raise (InvalidOperationException $"The walk went round a link: '{path}'.")
        else
            base.EnumerateFileSystemEntries(resolve path)
            |> Seq.map (fun entry -> Path.Combine(path, Path.GetFileName entry))

type LinkFollowing() =
    inherit MockFileSystem()

    let mutable following: IDirectory = null

    override this.Directory =
        if isNull following then
            following <- LinkFollowingDirectory(this, this, base.Directory.GetCurrentDirectory())

        following

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

type private WitnessedStream(inner: FileSystemStream, path: string, witness: unit -> unit) =
    inherit FileSystemStream(inner, path, false)

    override _.Flush() =
        inner.Flush()
        witness ()

    override _.Flush(flushToDisk: bool) =
        inner.Flush flushToDisk
        witness ()

type private WitnessedFile
    (accessor: IMockFileDataAccessor, flushed: ResizeArray<byte array>, renamed: ResizeArray<string * string>) =
    inherit MockFile(accessor)

    override this.Open(path: string, mode: FileMode, access: FileAccess, share: FileShare) =
        let witness () =
            flushed.Add(Array.copy (accessor.GetFile(path).Contents))

        new WitnessedStream(base.Open(path, mode, access, share), path, witness)

    override this.Move(source: string, target: string, overwrite: bool) =
        renamed.Add((source, target))
        base.Move(source, target, overwrite)

type WipeWitness() =
    inherit MockFileSystem()

    let flushed = ResizeArray<byte array>()
    let renamed = ResizeArray<string * string>()
    let mutable writing: IFile = null

    member _.Flushed = List.ofSeq flushed

    member _.Renamed = List.ofSeq renamed

    override this.File =
        if isNull writing then
            writing <- WitnessedFile(this, flushed, renamed)

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
      Launch = fun _ -> Ok()
      AskLink = fun _ _ -> None
      RefuseLink = fun _ -> None
      MakeLink = fun _ -> Ok()
      HandlersFor = fun _ -> []
      AskHandler = fun _ _ _ -> None
      ChooseTab = fun _ -> None
      EditProperties = fun _ -> None
      EditPrograms = fun _ _ -> None
      AskArchive = (fun _ -> None)
      AskPassword = (fun _ _ -> None)
      AskOverwrite = (fun _ _ -> Operations.No)
      WatchModifiers = ignore
      WatchLocations = fun _ _ -> ()
      RunBatch = fun _ work finish -> work Operations.unattended |> finish
      RunMeasure = fun _ work finish -> work Operations.unattended |> finish }

[<Literal>]
let PrivilegeNotHeld = 0x80070522

let private privilegeRefusal (path: string) =
    IOException($"A required privilege is not held by the client. : '{path}'.", PrivilegeNotHeld)

type private LinkRefusingDirectory(accessor: IMockFileDataAccessor, current: string, refusedUnder: string) =
    inherit MockDirectory(accessor, current)

    override this.CreateSymbolicLink(path: string, target: string) =
        if path.StartsWith refusedUnder then
            raise (privilegeRefusal path)
        else
            base.CreateSymbolicLink(path, target)

type private LinkRefusingFile(accessor: IMockFileDataAccessor, refusedUnder: string) =
    inherit MockFile(accessor)

    override this.CreateSymbolicLink(path: string, target: string) =
        if path.StartsWith refusedUnder then
            raise (privilegeRefusal path)
        else
            base.CreateSymbolicLink(path, target)

type SymbolicLinksRefused(refusedUnder: string) =
    inherit MockFileSystem()

    let mutable making: IDirectory = null
    let mutable writing: IFile = null

    override this.Directory =
        if isNull making then
            making <- LinkRefusingDirectory(this, base.Directory.GetCurrentDirectory(), refusedUnder)

        making

    override this.File =
        if isNull writing then
            writing <- LinkRefusingFile(this, refusedUnder)

        writing
