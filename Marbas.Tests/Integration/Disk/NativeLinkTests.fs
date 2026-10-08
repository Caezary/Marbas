module Marbas.Tests.Integration.Disk.NativeLinkTests

open System.IO
open System.IO.Abstractions
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Adapters
open Marbas.Tests

let private disk = FileSystem() :> IFileSystem

let private platform = HostEnvironment.current().Platform

let private make = Links.create (NativeLinks.forPlatform platform) platform disk

let private failedPath error =
    match error with
    | Operations.OperationFailed(path, _) -> Some path
    | _ -> None

let private withData () =
    let scratch = new Scratch()
    scratch.Directory "data/docs"
    scratch.File "data/docs/inner.txt" "inner"
    scratch.File "data/notes.txt" "notes"
    scratch

let private request kind (scratch: Scratch) target location =
    { Kind = kind
      Target = scratch.Path target
      Location = scratch.Path location }

[<Fact>]
let ``a hard link made through the system is the same file under a second name`` () =
    use scratch = withData ()

    make (request Hard scratch "data/notes.txt" "data/twin.txt") |> Expect.ok
    File.AppendAllText(scratch.At "data/twin.txt", " and more")

    Assert.Equal("notes and more", File.ReadAllText(scratch.At "data/notes.txt"))

[<Fact>]
let ``a hard link the system cannot make is reported rather than thrown`` () =
    use scratch = withData ()

    let failure =
        make (request Hard scratch "data/notes.txt" "data/gone/twin.txt")
        |> Expect.error

    Assert.Equal(Some(scratch.Path "data/gone/twin.txt"), failedPath failure)

[<WindowsFact("A junction is a Windows concept")>]
let ``a junction made through the system leads into its directory`` () =
    use scratch = withData ()

    make (request Junction scratch "data/docs" "data/twin") |> Expect.ok

    Assert.Equal(Some(scratch.Path "data/docs"), FileSystemPath.linkTarget disk (scratch.Path "data/twin"))
    Assert.Equal("inner", File.ReadAllText(scratch.At "data/twin/inner.txt"))

[<SymbolicLinkFact>]
let ``a symbolic link made through the system leads to its target`` () =
    use scratch = withData ()

    make (request Symbolic scratch "data/docs" "data/twin") |> Expect.ok

    Assert.Equal(Some(scratch.Path "data/docs"), FileSystemPath.linkTarget disk (scratch.Path "data/twin"))
    Assert.Equal("inner", File.ReadAllText(scratch.At "data/twin/inner.txt"))

[<WindowsFact("A junction is a Windows concept")>]
let ``deleting a directory recursively stumbles on a junction inside it`` () =
    use scratch = withData ()
    scratch.Directory "backup"
    make (request Junction scratch "data/docs" "backup/twin") |> Expect.ok

    let thrown = Record.Exception(fun () -> Directory.Delete(scratch.At "backup", true))

    Assert.NotNull thrown
    Assert.True(Directory.Exists(scratch.At "backup"))
    Assert.Equal("inner", File.ReadAllText(scratch.At "data/docs/inner.txt"))

[<WindowsFact("A junction is a Windows concept")>]
let ``deleting a tree holding a junction removes the tree and leaves what the junction points at`` () =
    use scratch = withData ()
    scratch.Directory "backup"
    make (request Junction scratch "data/docs" "backup/twin") |> Expect.ok

    Operations.delete disk Operations.unattended (scratch.Path "backup")
    |> Expect.ok

    Assert.False(Directory.Exists(scratch.At "backup"))
    Assert.Equal("inner", File.ReadAllText(scratch.At "data/docs/inner.txt"))
