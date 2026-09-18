module Marbas.Tests.Ui.App.TabCounterTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Collections
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.Ui.App
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private fs = MockFileSystem() :> IFileSystem

let private started () = create (fp (at "start"))

[<Fact>]
let ``one tab says it is the first of one`` () =
    let counter = TabCounter.describe (started ())

    Assert.Equal("[1/1]", counter)

[<Fact>]
let ``the counter says which of how many the tab in front is`` () =
    let state = started () |> openTab fs |> openTab fs

    let counter = TabCounter.describe state

    Assert.Equal("[3/3]", counter)

[<Fact>]
let ``switching tabs moves the number in front`` () =
    let state = started () |> openTab fs |> openTab fs |> switchTab 1

    let counter = TabCounter.describe state

    Assert.Equal("[2/3]", counter)

[<Fact>]
let ``a tenth tab widens the counter rather than losing a digit`` () =
    let state = [ 1..9 ] |> List.fold (fun current _ -> openTab fs current) (started ())

    let counter = TabCounter.describe state

    Assert.Equal("[10/10]", counter)
    Assert.Equal(10, Zipper.count state.Tabs)
