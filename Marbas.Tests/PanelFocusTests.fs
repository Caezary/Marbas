module Marbas.Tests.PanelFocusTests

open System
open Xunit
open Terminal.Gui.App
open Terminal.Gui.Drawing
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Marbas.Ui
open Marbas.Tests.TestHelpers

let private advanceFocus (window: Terminal.Gui.Views.Window) =
    window.AdvanceFocus(NavigationDirection.Forward, Nullable TabBehavior.TabStop)
    |> ignore

[<Fact>]
let ``left panel is focused on start`` () =
    use window = createInitialized ()
    window.SetFocus() |> ignore
    Assert.True((panel FilePanel.LeftId window).HasFocus)

[<Fact>]
let ``tab is the application key for moving focus`` () =
    Assert.Equal(Key.Tab, Application.GetDefaultKey Command.NextTabStop)

[<Fact>]
let ``advancing focus toggles between panels`` () =
    use window = createInitialized ()
    window.SetFocus() |> ignore
    advanceFocus window
    Assert.True((panel FilePanel.RightId window).HasFocus)
    advanceFocus window
    Assert.True((panel FilePanel.LeftId window).HasFocus)

[<Fact>]
let ``focused panel is marked with a double border`` () =
    use window = createInitialized ()
    window.SetFocus() |> ignore
    let left = panel FilePanel.LeftId window
    let right = panel FilePanel.RightId window
    Assert.Equal(Nullable LineStyle.Double, left.BorderStyle)
    Assert.Equal(Nullable LineStyle.Single, right.BorderStyle)
    advanceFocus window
    Assert.Equal(Nullable LineStyle.Single, left.BorderStyle)
    Assert.Equal(Nullable LineStyle.Double, right.BorderStyle)
