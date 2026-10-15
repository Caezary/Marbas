module Marbas.Tests.Adapters.HostEnvironmentTests

open Xunit
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
