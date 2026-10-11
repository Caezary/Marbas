module Marbas.Tests.State.Shell.ShellPromptTests

open Xunit
open Marbas.Domain
open Marbas.State
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.Shell.ShellFixture

let private en = EntryName.create

[<Fact>]
let ``a supplied name is dispatched as NameEntered`` () =
    let sent, dispatch = capture ()

    let deps =
        { noDeps () with
            Prompts.AskName = fun _ -> Some "work" }

    Shell.interpret deps dispatch PromptForName

    Assert.Equal<Message list>([ NameEntered(en "work") ], List.ofSeq sent)

[<Fact>]
let ``a cancelled name prompt dispatches nothing`` () =
    let sent, dispatch = capture ()

    Shell.interpret (noDeps ()) dispatch PromptForName

    Assert.Empty sent

[<Fact>]
let ``the name prompt uses the mkdir label`` () =
    let _, dispatch = capture ()
    let mutable asked = ""

    let deps =
        { noDeps () with
            Prompts.AskName =
                fun request ->
                    asked <- request.Title
                    None }

    Shell.interpret deps dispatch PromptForName

    Assert.Equal(Shell.MkdirPrompt, asked)

[<Fact>]
let ``a selecting pattern uses the select label and carries the answer back`` () =
    let sent, dispatch = capture ()
    let mutable asked = ""

    let deps =
        { noDeps () with
            Prompts.AskPattern =
                fun request ->
                    asked <- request.Title
                    Some "*.fs" }

    Shell.interpret deps dispatch (PromptForPattern Select)

    Assert.Equal(Shell.SelectPrompt, asked)
    Assert.Contains(PatternEntered(Select, NamePattern.tryCreate "*.fs" |> Expect.some), sent)

[<Fact>]
let ``a deselecting pattern uses the deselect label`` () =
    let _, dispatch = capture ()
    let mutable asked = ""

    let deps =
        { noDeps () with
            Prompts.AskPattern =
                fun request ->
                    asked <- request.Title
                    None }

    Shell.interpret deps dispatch (PromptForPattern Deselect)

    Assert.Equal(Shell.DeselectPrompt, asked)

[<Fact>]
let ``a name for a new file is dispatched as FileNameEntered`` () =
    let sent, dispatch = capture ()

    let deps =
        { noDeps () with
            Prompts.AskName = fun _ -> Some "draft.md" }

    Shell.interpret deps dispatch PromptForFileName

    Assert.Contains(FileNameEntered(en "draft.md"), sent)

[<Fact>]
let ``a cancelled new file prompt dispatches nothing`` () =
    let sent, dispatch = capture ()

    let deps =
        { noDeps () with
            Prompts.AskName = fun _ -> None }

    Shell.interpret deps dispatch PromptForFileName

    Assert.Empty sent

[<Fact>]
let ``the new file prompt opens on nothing`` () =
    let offered = ResizeArray<EnteredText option>()

    let deps =
        { noDeps () with
            Prompts.AskName =
                fun request ->
                    offered.Add request.Suggestion
                    None }

    Shell.interpret deps ignore PromptForFileName

    Assert.Equal(None, Assert.Single offered)
