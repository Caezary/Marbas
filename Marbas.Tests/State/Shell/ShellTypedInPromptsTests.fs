module Marbas.Tests.State.Shell.ShellTypedInPromptsTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Profile
open Marbas.Domain
open Marbas.State
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.Shell.ShellFixture

let private at = Root.at

let private fp = FileSystemPath.create

[<Fact>]
let ``the name prompt opens on the name the last directory was given`` () =
    let fs = MockFileSystem()
    typedEarlier fs NewDirectory [ "older"; "work" ]
    let mutable offered = None

    let deps =
        { withFs fs with
            Prompts.AskName =
                fun request ->
                    offered <- request.Suggestion
                    None }

    Shell.interpret deps ignore PromptForName

    Assert.Equal(Some(typed "work"), offered)

[<Fact>]
let ``the name prompt starts empty when nothing was remembered`` () =
    let mutable offered = Some(typed "something")

    let deps =
        { noDeps () with
            Prompts.AskName =
                fun request ->
                    offered <- request.Suggestion
                    None }

    Shell.interpret deps ignore PromptForName

    Assert.Equal(None, offered)

[<Fact>]
let ``the name prompt offers the names directories were given, newest first`` () =
    let fs = MockFileSystem()
    typedEarlier fs NewDirectory [ "older"; "work" ]
    let offered = ResizeArray<EnteredText list>()

    let deps =
        { withFs fs with
            Prompts.AskName =
                fun request ->
                    offered.Add request.Remembered
                    None }

    Shell.interpret deps ignore PromptForName

    Assert.Equal<EnteredText list>([ typed "work"; typed "older" ], Assert.Single offered)

[<Fact>]
let ``a name given to a directory is left for the state to remember`` () =
    let fs = MockFileSystem()

    let deps =
        { withFs fs with
            Prompts.AskName = fun _ -> Some "work" }

    Shell.interpret deps ignore PromptForName

    Assert.Empty(rememberedIn fs NewDirectory)

[<Fact>]
let ``the target prompt offers the targets typed before, newest first`` () =
    let fs = MockFileSystem()
    typedEarlier fs CopyOrMoveTarget [ @"D:\older"; @"D:\newer" ]
    let offered = ResizeArray<EnteredText list>()

    let deps =
        { withFs fs with
            Prompts.AskTarget =
                fun request ->
                    offered.Add request.Remembered
                    None }

    Shell.interpret deps ignore (PromptForTarget(fp (at "backup")))

    Assert.Equal<EnteredText list>([ typed @"D:\newer"; typed @"D:\older" ], Assert.Single offered)

[<Fact>]
let ``a target confirmed is remembered as it was typed`` () =
    let fs = MockFileSystem()

    let deps =
        { withFs fs with
            Prompts.AskTarget = fun _ -> Some @" D:\backup " }

    Shell.interpret deps ignore (PromptForTarget(fp (at "backup")))

    Assert.Equal<string list>([ @" D:\backup " ], rememberedIn fs CopyOrMoveTarget)

[<Fact>]
let ``a target prompt cancelled remembers nothing`` () =
    let fs = MockFileSystem()

    Shell.interpret (withFs fs) ignore (PromptForTarget(fp (at "backup")))

    Assert.False(fs.File.Exists(at "history.conf"))

[<Fact>]
let ``a blank target confirmed remembers nothing`` () =
    let fs = MockFileSystem()

    let deps =
        { withFs fs with
            Prompts.AskTarget = fun _ -> Some "   " }

    Shell.interpret deps ignore (PromptForTarget(fp (at "backup")))

    Assert.False(fs.File.Exists(at "history.conf"))

[<Fact>]
let ``the new file prompt offers the names typed before`` () =
    let fs = MockFileSystem()
    typedEarlier fs NewFile [ "notes.md" ]
    let offered = ResizeArray<EnteredText list>()

    let deps =
        { withFs fs with
            Prompts.AskName =
                fun request ->
                    offered.Add request.Remembered
                    None }

    Shell.interpret deps ignore PromptForFileName

    Assert.Equal<EnteredText list>([ typed "notes.md" ], Assert.Single offered)

[<Fact>]
let ``a name for a new file confirmed is remembered`` () =
    let fs = MockFileSystem()

    let deps =
        { withFs fs with
            Prompts.AskName = fun _ -> Some "draft.md" }

    Shell.interpret deps ignore PromptForFileName

    Assert.Equal<string list>([ "draft.md" ], rememberedIn fs NewFile)

[<Fact>]
let ``a new file prompt cancelled remembers nothing`` () =
    let fs = MockFileSystem()

    Shell.interpret (withFs fs) ignore PromptForFileName

    Assert.False(fs.File.Exists(at "history.conf"))

[<Fact>]
let ``the pattern prompts offer one list between selecting and deselecting`` () =
    let fs = MockFileSystem()
    let offered = ResizeArray<EnteredText list>()

    let deps =
        { withFs fs with
            Prompts.AskPattern =
                fun request ->
                    offered.Add request.Remembered
                    Some "*.fs" }

    Shell.interpret deps ignore (PromptForPattern Select)

    Shell.interpret deps ignore (PromptForPattern Deselect)

    Assert.Equal<EnteredText list>([ typed "*.fs" ], offered[1])

[<Fact>]
let ``a pattern confirmed is remembered`` () =
    let fs = MockFileSystem()

    let deps =
        { withFs fs with
            Prompts.AskPattern = fun _ -> Some "*.txt" }

    Shell.interpret deps ignore (PromptForPattern Deselect)

    Assert.Equal<string list>([ "*.txt" ], rememberedIn fs SelectionPattern)

[<Fact>]
let ``a pattern prompt cancelled remembers nothing`` () =
    let fs = MockFileSystem()

    Shell.interpret (withFs fs) ignore (PromptForPattern Select)

    Assert.False(fs.File.Exists(at "history.conf"))

[<Fact>]
let ``a blank pattern is still applied but not remembered`` () =
    let fs = MockFileSystem()
    let sent, dispatch = capture ()

    let deps =
        { withFs fs with
            Prompts.AskPattern = fun _ -> Some "  " }

    Shell.interpret deps dispatch (PromptForPattern Select)

    Assert.Contains(PatternEntered(Select, NamePattern.tryCreate "  " |> Expect.some), sent)
    Assert.False(fs.File.Exists(at "history.conf"))
