module Marbas.Tests.Ui.Messages.OperationErrorMessageTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Ui.Messages
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

[<Fact>]
let ``a missing source is explained with its path`` () =
    let message = OperationErrorMessage.describe (SourceNotFound(fp (at "gone")))

    Assert.Contains(at "gone", message)
    Assert.Contains("no longer exists", message)

[<Fact>]
let ``an existing target is explained with its path`` () =
    let message = OperationErrorMessage.describe (TargetExists(fp (at "data/new")))

    Assert.Contains(at "data/new", message)
    Assert.Contains("already exists", message)

[<Fact>]
let ``access denied is explained with its path`` () =
    let message = OperationErrorMessage.describe (AccessDenied(fp (at "locked")))

    Assert.Contains(at "locked", message)
    Assert.Contains("Access denied", message)

[<Fact>]
let ``an entry the batch stopped inside says so`` () =
    let message = OperationErrorMessage.describe (Cancelled(fp (at "data/tree")))

    Assert.Contains(at "data/tree", message)
    Assert.Contains("Stopped before", message)

[<Fact>]
let ``an entry that would land outside the target says where`` () =
    let message = OperationErrorMessage.describe (EntryOutsideTarget(fp (at "evil.txt")))

    Assert.Contains(at "evil.txt", message)
    Assert.Contains("outside the target", message)

[<Fact>]
let ``an unexpected failure includes the underlying reason`` () =
    let message = OperationErrorMessage.describe (OperationFailed(fp (at "bad"), "disk full"))

    Assert.Contains(at "bad", message)
    Assert.Contains("disk full", message)

[<Fact>]
let ``every failed item is named with its own reason`` () =
    let items =
        [ { Source = ItemOnDisk(fp (at "data/gone.txt"))
            Outcome = Error(SourceNotFound(fp (at "data/gone.txt"))) }
          { Source = ItemOnDisk(fp (at "data/locked"))
            Outcome = Error(AccessDenied(fp (at "data/locked"))) } ]

    let message = OperationErrorMessage.describeFailures (MockFileSystem()) items

    Assert.Contains("gone.txt", message)
    Assert.Contains("no longer exists", message)
    Assert.Contains("locked", message)
    Assert.Contains("Access denied", message)

[<Fact>]
let ``items that succeeded are left out`` () =
    let items =
        [ { Source = ItemOnDisk(fp (at "data/a.txt")); Outcome = Ok() }
          { Source = ItemOnDisk(fp (at "data/gone.txt"))
            Outcome = Error(SourceNotFound(fp (at "data/gone.txt"))) } ]

    let message = OperationErrorMessage.describeFailures (MockFileSystem()) items

    Assert.DoesNotContain("a.txt", message)
    Assert.Contains("gone.txt", message)

[<Fact>]
let ``a batch that could not run carries the reason it gave`` () =
    let message = OperationErrorMessage.describe (BatchFailed "no writer for tar")

    Assert.Contains("no writer for tar", message)

[<Fact>]
let ``a name no program on this system opens is explained with its path`` () =
    let message = OperationErrorMessage.describe (NoProgramAssociated(fp (at "start/notes.odd")))

    Assert.Contains(at "start/notes.odd", message)
    Assert.Contains("Nothing", message)

[<Fact>]
let ``a start the user turned down is explained with its path`` () =
    let message = OperationErrorMessage.describe (StartDeclined(fp (at "start/tool.exe")))

    Assert.Contains(at "start/tool.exe", message)
    Assert.Contains("administrator", message)

[<Fact>]
let ``a symbolic link Windows refuses names the two ways to be allowed one`` () =
    let message = OperationErrorMessage.describe (SymbolicLinkNotPermitted(fp (at "data/twin")))

    Assert.Contains(at "data/twin", message)
    Assert.Contains("Developer Mode", message)
    Assert.Contains("administrator", message)

[<Fact>]
let ``a link refused by its kind is explained with its path and the reason`` () =
    let message =
        OperationErrorMessage.describe (LinkRefused(JunctionToFile, fp (at "data/twin")))

    Assert.Contains(at "data/twin", message)
    Assert.Contains(LinkRefusalMessage.describe JunctionToFile, message)

[<Fact>]
let ``an entry that cannot be cut from an archive is named and offered a copy instead`` () =
    let message = OperationErrorMessage.describe (ArchiveEntryNotCut(fp (at "start/backup.zip/notes.txt")))

    Assert.Contains(at "start/backup.zip/notes.txt", message)
    Assert.Contains("cannot be cut", message)
    Assert.Contains("copy it", message)
