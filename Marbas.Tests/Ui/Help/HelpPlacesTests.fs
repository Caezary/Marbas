module Marbas.Tests.Ui.Help.HelpPlacesTests

open Microsoft.FSharp.Reflection
open Xunit
open Marbas.Guide
open Marbas.State.Update
open Marbas.Ui.Keys
open Marbas.Ui.Help
open Marbas.Tests
open Marbas.Tests.Guide

let private commands =
    KeyMap.bindings
    |> List.map _.Message
    |> List.distinct
    |> List.filter (fun message -> message <> HelpRequested)

let private popups =
    FSharpType.GetUnionCases typeof<HelpPlaces.Popup>
    |> Array.map (fun case -> FSharpValue.MakeUnion(case, [||]) :?> HelpPlaces.Popup)
    |> List.ofArray

let private placeIn name heading : GuidePlace =
    { Chapter = ChapterName.tryCreate name |> Expect.some
      Heading = Some(Anchor.create heading) }

let private places =
    (commands |> List.choose helpPlaceOf)
    @ [ HelpPlaces.viewer ]
    @ (popups |> List.map HelpPlaces.ofPopup)

[<Fact>]
let ``every command a binding raises has a place in the guide`` () =
    let placeless = commands |> List.filter (helpPlaceOf >> Option.isNone)

    Assert.Empty placeless

[<Fact>]
let ``the history popup opens its own section`` () =
    let place = HelpPlaces.ofPopup HelpPlaces.HistoryPopup

    Assert.Equal(placeIn "13-history.md" "the-history-popup", place)

[<Fact>]
let ``the viewer opens its chapter`` () =
    let place = HelpPlaces.viewer

    Assert.Equal(ChapterName.tryCreate "11-viewer.md" |> Expect.some, place.Chapter)
    Assert.Equal(None, place.Heading)

[<Fact>]
let ``every place of the table is a chapter or a heading of the guide`` () =
    let unreached = ChapterLinks.unreached (UserGuide.chapters ()) places

    Assert.Empty unreached

[<Fact>]
let ``no place of the table falls back to the contents`` () =
    let fallen = places |> List.filter (fun place -> place = GuidePlace.contents)

    Assert.Empty fallen
