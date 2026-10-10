module Marbas.Tests.Messages.ExpansionRefusalMessageTests

open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Messages
open Marbas.Tests

let private at = Root.at

[<Fact>]
let ``a place inside an archive is named, with the way out`` () =
    let message =
        ExpansionRefusalMessage.describe (InsideArchive(FileSystemPath.create (at "backup.zip/notes.txt")))

    Assert.Contains(at "backup.zip/notes.txt", message)
    Assert.Contains("Unpack", message)

[<Fact>]
let ``a panel with nothing chosen is named by its side`` () =
    let message = ExpansionRefusalMessage.describe (NoEntryChosen Right)

    Assert.Contains("right panel", message)

[<Fact>]
let ``a panel of search results is said to have no directory`` () =
    let message = ExpansionRefusalMessage.describe (NoDirectory Left)

    Assert.Contains("left panel", message)
    Assert.Contains("search results", message)

[<Fact>]
let ``a line that names no program says so`` () =
    let message = ExpansionRefusalMessage.describe NoProgram

    Assert.Contains("no program", message)
