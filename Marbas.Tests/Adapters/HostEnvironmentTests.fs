module Marbas.Tests.Adapters.HostEnvironmentTests

open Xunit
open Marbas.Domain
open Marbas.Adapters

[<Fact>]
let ``the program extensions are read from the variable in their order, blanks dropped`` () =
    let extensions = HostEnvironment.programExtensions (Some ".COM; .EXE;;.CMD ")

    Assert.Equal<string list>([ ".COM"; ".EXE"; ".CMD" ], extensions)

[<Fact>]
let ``without the variable the program extensions are the ones Windows starts by default`` () =
    let extensions = HostEnvironment.programExtensions None

    Assert.Equal<string list>([ ".COM"; ".EXE"; ".BAT"; ".CMD" ], extensions)

[<Theory>]
[<InlineData("")>]
[<InlineData(" ; ")>]
let ``a variable that lists no extension gives the ones Windows starts by default`` (raw: string) =
    let extensions = HostEnvironment.programExtensions (Some raw)

    Assert.Equal<string list>([ ".COM"; ".EXE"; ".BAT"; ".CMD" ], extensions)

[<Theory>]
[<InlineData("--version")>]
[<InlineData("--debug", "--version")>]
let ``the version switch asks for the version alone`` ([<System.ParamArray>] arguments: string array) =
    let launch = Launch.ofArguments arguments

    Assert.Equal(PrintVersion, launch)

[<Theory>]
[<InlineData>]
[<InlineData("--debug")>]
[<InlineData("-version")>]
[<InlineData("version")>]
let ``without the version switch the panels open`` ([<System.ParamArray>] arguments: string array) =
    let launch = Launch.ofArguments arguments

    Assert.Equal(OpenPanels, launch)

[<Fact>]
let ``the version is the one stamped on the application, without build metadata`` () =
    let version = HostEnvironment.productVersion ()

    Assert.Matches(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?$", ProductVersion.value version)
