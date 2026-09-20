module Marbas.Tests.Fakes

open System
open System.IO
open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Marbas.Domain
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

type private CountingFile(accessor: IMockFileDataAccessor, copied: unit -> unit) =
    inherit MockFile(accessor)

    override this.Copy(source: string, target: string, overwrite: bool) =
        copied ()
        base.Copy(source, target, overwrite)

type CopyCounting() =
    inherit MockFileSystem()

    let mutable copies = 0
    let mutable writing: IFile = null

    member _.Copies = copies

    override this.File =
        if isNull writing then
            writing <- CountingFile(this, fun () -> copies <- copies + 1)

        writing

[<Literal>]
let CrossVolumeReason = "Source and destination path must have identical roots."

type private UnmovableDirectory(accessor: IMockFileDataAccessor, current: string) =
    inherit MockDirectory(accessor, current)

    override this.Move(source: string, target: string) =
        raise (IOException CrossVolumeReason)

type DirectoryMoveRefused() =
    inherit MockFileSystem()

    let mutable listing: IDirectory = null

    override this.Directory =
        if isNull listing then
            listing <- UnmovableDirectory(this, base.Directory.GetCurrentDirectory())

        listing

let dependencies (fs: IFileSystem) (startPath: string) : Shell.Dependencies =
    { FileSystem = fs
      StartPath = FileSystemPath.create startPath
      SettingsPath = FileSystemPath.create (at "marbas.conf")
      WorkspacePath = FileSystemPath.create (at "temp/Marbas/session")
      ListDrives = fun () -> Drives.list fs None
      OnQuit = ignore
      ReportError = fun _ _ -> ()
      ChooseDrive = fun _ -> None
      AskPattern = fun _ -> None
      AskName = fun _ _ -> None
      AskTarget = fun _ _ -> None
      Confirm = fun _ _ -> false
      SendToTrash = fun _ -> Ok()
      ShowFile = ignore
      RunEditor = fun _ _ -> Ok()
      Launch = fun _ -> Ok()
      AskFind = fun _ _ _ -> None
      PathDirectories = []
      RunSearch = fun _ _ _ -> ()
      LinkKinds = [ Symbolic; Hard; Junction ]
      AskLink = fun _ _ _ -> None
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
      RunMeasure = fun _ work finish -> work Operations.unattended |> Ok |> finish }

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

type Mount =
    { MountPoint: string
      Format: string
      DriveType: DriveType
      Ready: bool
      Readable: bool
      Size: int64
      Free: int64
      Label: string }

module Mount =

    let ext4 mountPoint =
        { MountPoint = mountPoint
          Format = "ext4"
          DriveType = DriveType.Fixed
          Ready = true
          Readable = true
          Size = 0L
          Free = 0L
          Label = "" }

let private mountedDrive (fs: IFileSystem) (mount: Mount) =
    let measured size =
        if mount.Readable then
            size
        else
            raise (UnauthorizedAccessException $"Access to the path '{mount.MountPoint}' is denied.")

    { new IDriveInfo with
        member _.Name = mount.MountPoint
        member _.DriveFormat = mount.Format
        member _.DriveType = mount.DriveType
        member _.IsReady = mount.Ready
        member _.TotalSize = measured mount.Size
        member _.TotalFreeSpace = measured mount.Free
        member _.AvailableFreeSpace = measured mount.Free
        member _.RootDirectory = fs.DirectoryInfo.New mount.MountPoint
        member _.FileSystem = fs

        member _.VolumeLabel
            with get () = mount.Label
            and set _ = () }

type MountedFileSystem(mounts: Mount list) =
    inherit MockFileSystem()

    let plugged = ResizeArray mounts

    member _.Plug(mount: Mount) = plugged.Add mount

    override this.DriveInfo =
        let drives () =
            plugged |> Seq.map (mountedDrive this) |> Array.ofSeq

        { new IDriveInfoFactory with
            member _.GetDrives() = drives ()
            member _.New(name: string) = drives () |> Array.find (fun drive -> drive.Name = name)
            member _.Wrap(_: DriveInfo) = raise (NotSupportedException())
            member _.FileSystem = this }

let driveAt (root: string) =
    { Root = FileSystemPath.create root
      Label = None
      Kind = Fixed
      Size = 0L
      Free = 0L }

let choosingDrive (root: string) =
    fun (_: DriveRow list) -> root |> driveAt |> MountedDrive |> StandOn |> Some
