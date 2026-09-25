module Marbas.Tests.Ui.SubViewTests

open Xunit
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Ui

let private parent (children: View list) =
    let view = new View()
    children |> List.iter (view.Add >> ignore)
    view

[<Fact>]
let ``a sub-view is found by its type and its id`` () =
    let wanted = new TextField(Id = "second")
    let view = parent [ new TextField(Id = "first"); wanted ]

    let found = SubView.byId<TextField> "second" view

    Assert.Same(wanted, found)

[<Fact>]
let ``two sub-views sharing an id are told apart by their type`` () =
    let wanted = new Label(Id = "shared")
    let view = parent [ new TextField(Id = "shared"); wanted ]

    let found = SubView.byId<Label> "shared" view

    Assert.Same(wanted, found)

[<Fact>]
let ``a sub-view with no id is found by its type alone`` () =
    let wanted = new ListView()
    let view = parent [ new Label(); wanted ]

    let found = SubView.byType<ListView> view

    Assert.Same(wanted, found)
