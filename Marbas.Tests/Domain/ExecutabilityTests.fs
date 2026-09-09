module Marbas.Tests.Domain.ExecutabilityTests

open System.IO
open Xunit
open Marbas.Domain

let private classify name mode =
    Executability.classify (EntryName.create name) mode

[<Theory>]
[<InlineData("setup.exe")>]
[<InlineData("SETUP.EXE")>]
[<InlineData("tool.com")>]
[<InlineData("build.bat")>]
[<InlineData("build.cmd")>]
[<InlineData("package.msi")>]
[<InlineData("saver.scr")>]
[<InlineData("services.msc")>]
[<InlineData("mouse.cpl")>]
[<InlineData("install.vbs")>]
[<InlineData("install.vbe")>]
[<InlineData("deploy.wsf")>]
[<InlineData("deploy.wsh")>]
let ``a windows program extension makes an entry executable`` name =
    let executability = classify name UnixFileMode.None

    Assert.Equal(Executable, executability)

[<Theory>]
[<InlineData("readme.txt")>]
[<InlineData("archive.zip")>]
[<InlineData("noextension")>]
[<InlineData("bundle.js")>]
[<InlineData("script.ps1")>]
let ``anything else without an execute bit is not executable`` name =
    let executability = classify name UnixFileMode.None

    Assert.Equal(NotExecutable, executability)

[<Theory>]
[<InlineData(UnixFileMode.UserExecute)>]
[<InlineData(UnixFileMode.GroupExecute)>]
[<InlineData(UnixFileMode.OtherExecute)>]
let ``an execute bit alone is what the mode is read for`` mode =
    Assert.Equal(Executable, Executability.ofMode mode)

[<Fact>]
let ``the mode knows nothing of a windows program extension`` () =
    Assert.Equal(NotExecutable, Executability.ofMode UnixFileMode.None)

[<Theory>]
[<InlineData(UnixFileMode.UserExecute)>]
[<InlineData(UnixFileMode.GroupExecute)>]
[<InlineData(UnixFileMode.OtherExecute)>]
let ``a unix execute bit makes an entry executable whatever its name`` mode =
    let executability = classify "build" mode

    Assert.Equal(Executable, executability)

[<Fact>]
let ``read and write bits alone leave an entry unexecutable`` () =
    let executability =
        classify "notes" (UnixFileMode.UserRead ||| UnixFileMode.UserWrite)

    Assert.Equal(NotExecutable, executability)
