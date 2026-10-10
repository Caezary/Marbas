module Marbas.Tests.FileSystem.WorkspaceTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private session = "0f3c9a2b"

[<Fact>]
let ``a session works under a directory of its own inside the temporary one`` () =
    let fs = MockFileSystem()

    let path = Workspace.location fs (at "temp") session

    Assert.Equal(at $"temp/Marbas/{session}", FileSystemPath.value path)

[<Fact>]
let ``a temporary root nobody named falls back to the platform's own`` () =
    let fs = MockFileSystem()

    let path = Workspace.location fs "" session

    Assert.Equal(fs.Path.Combine(fs.Path.GetTempPath(), Workspace.DirectoryName, session), FileSystemPath.value path)

[<Fact>]
let ``a workspace asked for the first time is made`` () =
    let fs = MockFileSystem()
    let path = fp (at "temp/Marbas/session")

    let opened = Workspace.ensure fs path |> Expect.ok

    Assert.Equal(path, opened)
    Assert.True(fs.Directory.Exists(at "temp/Marbas/session"))

[<Fact>]
let ``a workspace already there is answered as it stands`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "temp/Marbas/session/left.txt", MockFileData "left")
    let path = fp (at "temp/Marbas/session")

    let opened = Workspace.ensure fs path |> Expect.ok

    Assert.Equal(path, opened)
    Assert.True(fs.File.Exists(at "temp/Marbas/session/left.txt"))

[<Fact>]
let ``a workspace that cannot be made is an operation failure`` () =
    let fs = Fakes.UnwritableDirectory(at "temp/Marbas/session")

    let failure = Workspace.ensure fs (fp (at "temp/Marbas/session")) |> Expect.error

    Assert.Equal(Operations.AccessDenied(fp (at "temp/Marbas/session")), failure)

[<Fact>]
let ``releasing a workspace takes everything in it`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "temp/Marbas/session/deep/left.txt", MockFileData "left")

    Workspace.release fs (fp (at "temp/Marbas/session"))

    Assert.False(fs.Directory.Exists(at "temp/Marbas/session"))

[<Fact>]
let ``releasing a workspace that was never made does nothing`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "temp")

    Workspace.release fs (fp (at "temp/Marbas/session"))

    Assert.True(fs.Directory.Exists(at "temp"))

[<Fact>]
let ``a scratch directory is made empty inside the workspace`` () =
    let fs = MockFileSystem()

    let scratch = Workspace.scratch fs (fp (at "temp/Marbas/session")) |> Expect.ok

    Assert.StartsWith(at "temp/Marbas/session", FileSystemPath.value scratch)
    Assert.True(fs.Directory.Exists(FileSystemPath.value scratch))
    Assert.Empty(fs.Directory.EnumerateFileSystemEntries(FileSystemPath.value scratch))

[<Fact>]
let ``two scratch directories never share a name`` () =
    let fs = MockFileSystem()
    let workspace = fp (at "temp/Marbas/session")

    let first = Workspace.scratch fs workspace |> Expect.ok
    let second = Workspace.scratch fs workspace |> Expect.ok

    Assert.NotEqual(first, second)

[<Fact>]
let ``a scratch directory the workspace cannot hold is an operation failure`` () =
    let fs = Fakes.UnwritableDirectory(at "temp/Marbas/session")

    let failure = Workspace.scratch fs (fp (at "temp/Marbas/session")) |> Expect.error

    Assert.Equal(Operations.AccessDenied(fp (at "temp/Marbas/session")), failure)

[<Fact>]
let ``the list file of a run is named in the working directory of the session`` () =
    let fs = MockFileSystem()

    let path = Workspace.listFile fs (fp (at "temp/Marbas/session"))

    Assert.Equal(at "temp/Marbas/session/marked-1.txt", FileSystemPath.value path)

[<Fact>]
let ``the list file of a run takes a name no earlier run left there`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "temp/Marbas/session/marked-1.txt", MockFileData "")
    fs.AddFile(at "temp/Marbas/session/marked-2.txt", MockFileData "")

    let path = Workspace.listFile fs (fp (at "temp/Marbas/session"))

    Assert.Equal(at "temp/Marbas/session/marked-3.txt", FileSystemPath.value path)

let private own = fp (at "temp/Marbas/session")

let private started = DateTimeOffset(2026, 10, 9, 8, 15, 0, TimeSpan.FromHours 2.0)

let private now = DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.FromHours 2.0)

let private owner = { ProcessId = 4120; Started = started }

let private nobodyRuns (_: SessionOwner) = false

let private running (alive: SessionOwner list) (asked: SessionOwner) = List.contains asked alive

let private leftBy (fs: MockFileSystem) (name: string) (owner: SessionOwner) =
    fs.AddFile(at $"temp/Marbas/{name}/1/left.txt", MockFileData "left")
    Workspace.claim fs owner (fp (at $"temp/Marbas/{name}")) |> Expect.ok

let private unclaimed (fs: MockFileSystem) (name: string) (written: DateTimeOffset) =
    fs.AddFile(at $"temp/Marbas/{name}/1/left.txt", MockFileData("left", LastWriteTime = written))
    fs.Directory.SetLastWriteTimeUtc(at $"temp/Marbas/{name}/1", written.UtcDateTime)
    fs.Directory.SetLastWriteTimeUtc(at $"temp/Marbas/{name}", written.UtcDateTime)

let private keptFor (directory: string) reason : Workspace.SweepRefusal =
    { Directory = fp directory
      Reason = reason }

[<Fact>]
let ``a session writes who owns its working directory when it makes it`` () =
    let fs = MockFileSystem()

    let claimed = Workspace.claim fs owner own

    Assert.Equal(Ok(), claimed)

    Assert.Equal<string>(
        [ "4120"; "2026-10-09T08:15:00.0000000+02:00" ],
        fs.File.ReadAllLines(at "temp/Marbas/session/owner")
    )

[<Fact>]
let ``claiming a working directory keeps what it already holds`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "temp/Marbas/session/1/left.txt", MockFileData "left")

    let claimed = Workspace.claim fs owner own

    Assert.Equal(Ok(), claimed)
    Assert.True(fs.File.Exists(at "temp/Marbas/session/1/left.txt"))
    Assert.True(fs.File.Exists(at "temp/Marbas/session/owner"))

[<Fact>]
let ``a working directory that cannot be made is not claimed`` () =
    let fs = Fakes.UnwritableDirectory(at "temp/Marbas/session")

    let failure = Workspace.claim fs owner own |> Expect.error

    Assert.Equal(Operations.AccessDenied own, failure)

[<Fact>]
let ``the directory of a session whose process is gone is removed whole`` () =
    let fs = MockFileSystem()
    leftBy fs "earlier" owner

    let refused = Workspace.sweep fs nobodyRuns now own

    Assert.Empty refused
    Assert.False(fs.Directory.Exists(at "temp/Marbas/earlier"))

[<Fact>]
let ``a directory whose process id now belongs to a process started at another moment is removed`` () =
    let fs = MockFileSystem()
    leftBy fs "earlier" owner

    let reused =
        { owner with
            Started = started.AddHours 3.0 }

    let refused = Workspace.sweep fs (running [ reused ]) now own

    Assert.Empty refused
    Assert.False(fs.Directory.Exists(at "temp/Marbas/earlier"))

[<Fact>]
let ``the directory of a session still running is kept`` () =
    let fs = MockFileSystem()
    leftBy fs "beside" owner

    let refused = Workspace.sweep fs (running [ owner ]) now own

    Assert.Empty refused
    Assert.True(fs.File.Exists(at "temp/Marbas/beside/1/left.txt"))

[<Fact>]
let ``the session's own directory is kept whoever is asked about it`` () =
    let fs = MockFileSystem()
    leftBy fs "session" owner

    let refused = Workspace.sweep fs nobodyRuns now own

    Assert.Empty refused
    Assert.True(fs.File.Exists(at "temp/Marbas/session/1/left.txt"))

[<Fact>]
let ``a directory nobody claimed is removed once nothing in it was written for a day`` () =
    let fs = MockFileSystem()
    unclaimed fs "older" (now.AddHours -25.0)

    let refused = Workspace.sweep fs nobodyRuns now own

    Assert.Empty refused
    Assert.False(fs.Directory.Exists(at "temp/Marbas/older"))

[<Fact>]
let ``a directory nobody claimed is kept before the day is over`` () =
    let fs = MockFileSystem()
    unclaimed fs "older" (now.AddHours -23.0)

    let refused = Workspace.sweep fs nobodyRuns now own

    Assert.Empty refused
    Assert.True(fs.File.Exists(at "temp/Marbas/older/1/left.txt"))

[<Fact>]
let ``a directory nobody claimed is kept while anything under it was written within the day`` () =
    let fs = MockFileSystem()
    unclaimed fs "older" (now.AddDays -30.0)
    fs.AddFile(at "temp/Marbas/older/1/fresh.txt", MockFileData("fresh", LastWriteTime = now.AddHours -1.0))

    let refused = Workspace.sweep fs nobodyRuns now own

    Assert.Empty refused
    Assert.True(fs.File.Exists(at "temp/Marbas/older/1/left.txt"))

[<Fact>]
let ``an owner file that names no owner leaves the directory to the day`` () =
    let fs = MockFileSystem()
    unclaimed fs "older" (now.AddHours -23.0)
    fs.AddFile(at "temp/Marbas/older/owner", MockFileData("half a li", LastWriteTime = now.AddHours -23.0))

    let refused = Workspace.sweep fs nobodyRuns now own

    Assert.Empty refused
    Assert.True(fs.File.Exists(at "temp/Marbas/older/1/left.txt"))

[<Fact>]
let ``a directory that refuses deletion is kept and answered with the reason`` () =
    let fs = Fakes.HeldOpen(at "temp/Marbas/earlier/1/left.txt")
    leftBy fs "earlier" owner

    let refused = Workspace.sweep fs nobodyRuns now own

    Assert.Equal<Workspace.SweepRefusal list>(
        [ keptFor
              (at "temp/Marbas/earlier")
              (Operations.OperationFailed(fp (at "temp/Marbas/earlier/1/left.txt"), Fakes.HeldOpenReason)) ],
        refused
    )

    Assert.True(fs.File.Exists(at "temp/Marbas/earlier/1/left.txt"))

[<Fact>]
let ``a directory that refuses deletion does not stop the sweep`` () =
    let fs = Fakes.HeldOpen(at "temp/Marbas/earlier/1/left.txt")
    leftBy fs "earlier" owner
    leftBy fs "later" owner

    let refused = Workspace.sweep fs nobodyRuns now own

    Assert.Single refused |> ignore
    Assert.False(fs.Directory.Exists(at "temp/Marbas/later"))

[<Fact>]
let ``a file beside the working directories is left alone`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "temp/Marbas/note.txt", MockFileData("note", LastWriteTime = now.AddDays -30.0))

    let refused = Workspace.sweep fs nobodyRuns now own

    Assert.Empty refused
    Assert.True(fs.File.Exists(at "temp/Marbas/note.txt"))

[<Fact>]
let ``nothing beside the directory the sessions work under is touched`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "temp/other/1/left.txt", MockFileData("left", LastWriteTime = now.AddDays -30.0))
    fs.Directory.SetLastWriteTimeUtc(at "temp/other/1", now.AddDays(-30.0).UtcDateTime)
    fs.Directory.SetLastWriteTimeUtc(at "temp/other", now.AddDays(-30.0).UtcDateTime)
    unclaimed fs "older" (now.AddDays -30.0)

    let refused = Workspace.sweep fs nobodyRuns now own

    Assert.Empty refused
    Assert.True(fs.File.Exists(at "temp/other/1/left.txt"))

[<Fact>]
let ``with no session ever started there is nothing to sweep`` () =
    let fs = MockFileSystem()

    let refused = Workspace.sweep fs nobodyRuns now own

    Assert.Empty refused
