module Marbas.Tests.Ui.ModifiersTests

open Xunit
open Terminal.Gui.Input
open Marbas.Ui

[<Fact>]
let ``a plain key holds no modifier`` () =
    let modifiers = Modifiers.ofKey Key.F3

    Assert.Equal<Set<Modifier>>(Modifiers.none, modifiers)

[<Fact>]
let ``a key names the modifier it carries`` () =
    let modifiers = Modifiers.ofKey Key.F3.WithCtrl

    Assert.Equal<Set<Modifier>>(Modifiers.held [ Ctrl ], modifiers)

[<Fact>]
let ``a key can carry several modifiers at once`` () =
    let modifiers = Modifiers.ofKey Key.F1.WithAlt.WithShift

    Assert.Equal<Set<Modifier>>(Modifiers.held [ Alt; Shift ], modifiers)
