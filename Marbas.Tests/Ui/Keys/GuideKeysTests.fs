module Marbas.Tests.Ui.Keys.GuideKeysTests

open Xunit
open Terminal.Gui.Input
open Marbas.Guide
open Marbas.Ui.Keys
open Marbas.Ui.Viewer
open Marbas.Ui.Find
open Marbas.Ui.Help
open Marbas.Tests.Guide

let private spelled keys =
    keys |> List.map KeySpelling.nameOf |> Set.ofList

let private bound =
    spelled (
        (KeyMap.bindings |> List.map _.Key)
        @ KeyMap.selectionPageDownKeys
        @ KeyMap.selectionPageUpKeys
        @ KeyMap.historyKeys
        @ KeyMap.userMenuKeys
        @ KeyMap.favoritesKeys
        @ ViewerKeys.keys
        @ FindKeys.keys
        @ HelpKeys.keys
    )

let private unmeasured =
    spelled
        [ KeyMap.classicMarkExtensionKey
          KeyMap.classicUnmarkExtensionKey
          KeyMap.classicMarkStemKey
          KeyMap.classicUnmarkStemKey
          KeyMap.classicMarkFilesKey
          KeyMap.classicUnmarkFilesKey
          KeyMap.classicInvertKey
          KeyMap.classicRecallMarksKey
          KeyMap.classicVolumeRootKey
          KeyMap.classicOpenWithKey
          KeyMap.classicElevatedRunKey
          KeyMap.spareCopyFilesKey
          KeyMap.spareCutFilesKey
          KeyMap.sparePasteFilesKey ]

let private handledByTheViews =
    spelled
        [ Key.CursorUp
          Key.CursorDown
          Key.PageUp
          Key.PageDown
          Key.Home
          Key.End
          Key.Tab
          Key.Enter
          Key.Esc
          Key.CursorDown.WithAlt ]

let private named () =
    ChapterKeys.named (UserGuide.chapters ())

[<Fact>]
let ``every key bound and delivered is in a key table of the guide`` () =
    let guide = named ()

    let missing = Set.difference (Set.difference bound unmeasured) guide

    Assert.Empty(missing |> Set.map KeyName.value)

[<Fact>]
let ``every key of a key table is bound or handled by the views`` () =
    let guide = named ()

    let unbound = Set.difference guide (Set.union bound handledByTheViews)

    Assert.Empty(unbound |> Set.map KeyName.value)

[<Fact>]
let ``a key no measured terminal delivers is left out of the key tables`` () =
    let guide = named ()

    let undelivered = Set.intersect unmeasured guide

    Assert.Empty(undelivered |> Set.map KeyName.value)
