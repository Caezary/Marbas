module Marbas.Tests.Ui.Keys.ModifiersTests

open Xunit
open Terminal.Gui.Input
open Marbas.Domain
open Marbas.Ui.Keys

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

[<Fact>]
let ``holding nothing leaves a key as it was`` () =
    let key = Modifiers.apply Modifiers.none Key.F3

    Assert.Equal(Key.F3, key)

[<Fact>]
let ``a held modifier lands on the key`` () =
    let key = Modifiers.apply (Modifiers.held [ Ctrl ]) Key.F3

    Assert.Equal(Key.F3.WithCtrl, key)

[<Fact>]
let ``every held modifier lands on the key`` () =
    let key = Modifiers.apply (Modifiers.held [ Alt; Shift ]) Key.F1

    Assert.Equal(Key.F1.WithAlt.WithShift, key)

[<Fact>]
let ``what a key is given is what it then carries`` () =
    for modifiers in
        [ Modifiers.none
          Modifiers.held [ Ctrl ]
          Modifiers.held [ Alt ]
          Modifiers.held [ Shift ]
          Modifiers.held [ Ctrl; Alt; Shift ] ] do
        let key = Modifiers.apply modifiers Key.F5

        Assert.Equal<Set<Modifier>>(modifiers, Modifiers.ofKey key)
