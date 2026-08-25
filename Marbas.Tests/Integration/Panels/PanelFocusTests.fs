module Marbas.Tests.Integration.Panels.PanelFocusTests

open System
open Xunit
open Terminal.Gui.App
open Terminal.Gui.Drawing
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Marbas.Ui.Panels
open Marbas.Tests
open Marbas.Tests.Integration.Gui

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
    let focusKey = Application.GetDefaultKey Command.NextTabStop

    Assert.Equal(Key.Tab, focusKey)

[<Fact>]
let ``advancing focus toggles between panels`` () =
    use window = createInitialized ()
    window.SetFocus() |> ignore

    advanceFocus window
    let rightHasFocus = (panel FilePanel.RightId window).HasFocus
    advanceFocus window
    let leftHasFocus = (panel FilePanel.LeftId window).HasFocus

    Assert.True rightHasFocus
    Assert.True leftHasFocus

[<Fact>]
let ``focused panel is marked with a double border`` () =
    use window = createInitialized ()
    window.SetFocus() |> ignore
    let left = panel FilePanel.LeftId window
    let right = panel FilePanel.RightId window

    let leftOnStart, rightOnStart = left.BorderStyle, right.BorderStyle
    advanceFocus window
    let leftAdvanced, rightAdvanced = left.BorderStyle, right.BorderStyle

    Assert.Equal(Nullable LineStyle.Double, leftOnStart)
    Assert.Equal(Nullable LineStyle.Single, rightOnStart)
    Assert.Equal(Nullable LineStyle.Single, leftAdvanced)
    Assert.Equal(Nullable LineStyle.Double, rightAdvanced)
