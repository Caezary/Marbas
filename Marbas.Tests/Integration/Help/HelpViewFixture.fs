module Marbas.Tests.Integration.Help.HelpViewFixture

open System.Drawing
open Marbas.Guide
open Marbas.Ui.Help
open Marbas.Tests
open Marbas.Tests.Guide

let name raw =
    ChapterName.tryCreate raw |> Expect.some

let quietPorts: HelpView.Ports =
    { Close = ignore
      Read = EmbeddedGuide.read
      Report = ignore
      AskSearch = fun _ -> None
      ReportSearch = ignore }

let showing ports (size: Size) =
    let window =
        HelpView.create ports (HelpState.start (UserGuide.chapterOf "README.md"))

    window.BeginInit()
    window.EndInit()
    window.Layout(size) |> ignore
    window

let laidOut () = showing quietPorts (Size(80, 24))

let chapterShown window =
    (HelpView.content window).State.Page.Chapter.Name
