module Marbas.Tests.Domain.ExecutabilityTests

open Xunit
open Marbas.Domain

let private en = EntryName.create

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
let ``a windows program extension makes a name executable`` name =
    let executability = Executability.ofName (en name)

    Assert.Equal(Executable, executability)

[<Theory>]
[<InlineData("readme.txt")>]
[<InlineData("archive.zip")>]
[<InlineData("noextension")>]
[<InlineData("bundle.js")>]
[<InlineData("script.ps1")>]
let ``any other name is not executable by itself`` name =
    let executability = Executability.ofName (en name)

    Assert.Equal(NotExecutable, executability)

[<Fact>]
let ``a program name is executable whatever its mode says`` () =
    let executability = Executability.classify (en "setup.exe") NotExecutable

    Assert.Equal(Executable, executability)

[<Fact>]
let ``an executable mode makes an entry executable whatever its name`` () =
    let executability = Executability.classify (en "build") Executable

    Assert.Equal(Executable, executability)

[<Fact>]
let ``neither the name nor the mode leaves an entry unexecutable`` () =
    let executability = Executability.classify (en "notes") NotExecutable

    Assert.Equal(NotExecutable, executability)
