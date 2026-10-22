module Marbas.Tests.State.Shell.ShellHelpTests

open Xunit
open Marbas.Guide
open Marbas.State
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.Shell.ShellFixture

let private sorting: GuidePlace =
    { Chapter = ChapterName.tryCreate "02-panels.md" |> Expect.some
      Heading = Some(Anchor.create "sorting") }

[<Fact>]
let ``the help is shown at the place the effect names`` () =
    let shown = ResizeArray<GuidePlace>()

    let deps =
        { noDeps () with
            Prompts.ShowHelp = shown.Add }

    Shell.interpret deps ignore (ShowHelp sorting)

    Assert.Equal(sorting, Assert.Single shown)
