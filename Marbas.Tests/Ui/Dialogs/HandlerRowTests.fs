module Marbas.Tests.Ui.Dialogs.HandlerRowTests

open System
open Xunit
open Marbas.Domain
open Marbas.Programs
open Marbas.Ui.Dialogs
open Marbas.Tests

let private handler name program =
    Handler.tryCreate name (ProgramPath.tryCreate program) |> Expect.some

[<Fact>]
let ``a row names the program and the path behind it`` () =
    let row =
        HandlerRow.describe (handler "Firefox" "C:\\Program Files\\Mozilla Firefox\\firefox.exe")

    Assert.StartsWith("Firefox", row)
    Assert.EndsWith("firefox.exe", row)

[<Fact>]
let ``the paths line up whatever the name`` () =
    let short = HandlerRow.describe (handler "Firefox" "/usr/bin/firefox")
    let long = HandlerRow.describe (handler "Visual Studio Code" "/usr/bin/code")

    Assert.Equal(
        short.IndexOf("/usr/bin/firefox", StringComparison.Ordinal),
        long.IndexOf("/usr/bin/code", StringComparison.Ordinal)
    )

[<Fact>]
let ``a name too long for the column is still followed by its path`` () =
    let row =
        HandlerRow.describe (handler "A Program With A Very Long Name" "/usr/bin/thing")

    Assert.Contains("A Program With A Very Long Name /usr/bin/thing", row)
