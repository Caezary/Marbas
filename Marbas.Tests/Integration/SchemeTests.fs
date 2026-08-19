module Marbas.Tests.Integration.SchemeTests

open Xunit
open Terminal.Gui.Drawing
open Marbas.Ui
open Marbas.Tests.Integration.Gui

[<Fact>]
let ``the screen behind the panels wears the screen scheme`` () =
    use window = createInitialized ()

    let scheme = window.GetScheme()

    Assert.Equal(ClassicScheme.screen, scheme)

[<Fact>]
let ``both panels wear the panel scheme`` () =
    use window = createInitialized ()

    let schemes =
        [ FilePanel.LeftId; FilePanel.RightId ]
        |> List.map (fun id -> (panel id window).GetScheme())

    Assert.Equal<Scheme list>([ ClassicScheme.panel; ClassicScheme.panel ], schemes)

[<Fact>]
let ``entries are drawn in the panel scheme`` () =
    use window = createInitialized ()

    let scheme = (leftList window).GetScheme()

    Assert.Equal(ClassicScheme.panel, scheme)

[<Fact>]
let ``the marked summary line wears the summary scheme`` () =
    use window = createInitialized ()

    let scheme = (FilePanel.footer (panel FilePanel.LeftId window)).GetScheme()

    Assert.Equal(ClassicScheme.summary, scheme)

[<Fact>]
let ``the status line wears the screen scheme`` () =
    use window = createInitialized ()

    let scheme = (panel StatusLine.Id window).GetScheme()

    Assert.Equal(ClassicScheme.screen, scheme)

[<Fact>]
let ``the function key bar wears the key bar scheme`` () =
    use window = createInitialized ()

    let scheme = (panel FunctionKeyBar.Id window).GetScheme()

    Assert.Equal(ClassicScheme.keyBar, scheme)
