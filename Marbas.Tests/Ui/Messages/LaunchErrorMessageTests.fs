module Marbas.Tests.Ui.Messages.LaunchErrorMessageTests

open Xunit
open Marbas.FileSystem
open Marbas.Ui.Messages
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

[<Fact>]
let ``a name nothing is associated with is explained with its path`` () =
    let message = LaunchErrorMessage.describe (NothingAssociated(fp (at "start/notes.odd")))

    Assert.Contains(at "start/notes.odd", message)
    Assert.Contains("Nothing", message)

[<Fact>]
let ``a program that is not there is named`` () =
    let message =
        LaunchErrorMessage.describe (ProgramNotFound(Expect.some (ProgramPath.tryCreate "gone.exe")))

    Assert.Contains("gone.exe", message)

[<Fact>]
let ``a refusal carries the reason the system gave`` () =
    let message = LaunchErrorMessage.describe (LaunchRefused(fp (at "start/tool.exe"), "access is denied"))

    Assert.Contains(at "start/tool.exe", message)
    Assert.Contains("access is denied", message)

[<Fact>]
let ``a declined elevation is explained with its path`` () =
    let message = LaunchErrorMessage.describe (ElevationDeclined(fp (at "start/tool.exe")))

    Assert.Contains(at "start/tool.exe", message)
    Assert.Contains("administrator", message)

[<Fact>]
let ``an entry that is not a program is told what administrator rights are for`` () =
    let message = LaunchErrorMessage.describe (NotAProgram(fp (at "start/notes.txt")))

    Assert.Contains(at "start/notes.txt", message)
    Assert.Contains("administrator", message)
