module Marbas.Tests.Adapters.HostEnvironmentTests

open Xunit
open Marbas.Adapters

[<Fact>]
let ``the program extensions are read from the variable in their order, blanks dropped`` () =
    let extensions = HostEnvironment.programExtensions ".COM; .EXE;;.CMD "

    Assert.Equal<string list>([ ".COM"; ".EXE"; ".CMD" ], extensions)

[<Theory>]
[<InlineData(null)>]
[<InlineData("")>]
[<InlineData(" ; ")>]
let ``without the variable the program extensions are the ones Windows starts by default`` (raw: string) =
    let extensions = HostEnvironment.programExtensions raw

    Assert.Equal<string list>([ ".COM"; ".EXE"; ".BAT"; ".CMD" ], extensions)
