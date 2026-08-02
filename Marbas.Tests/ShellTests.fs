module Marbas.Tests.ShellTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Ui

let private fp = FileSystemPath.create

let private noDeps () = TestHelpers.dependencies (MockFileSystem()) @"C:\start"

let private capture () =
    let sent = ResizeArray<Message>()
    sent, fun (msg: Message) -> sent.Add msg

[<Fact>]
let ``a supplied name is dispatched as NameEntered`` () =
    let sent, dispatch = capture ()
    let deps = { noDeps () with AskName = fun _ -> Some "work" }

    Shell.interpret deps dispatch PromptForName

    Assert.Equal<Message list>([ NameEntered "work" ], List.ofSeq sent)

[<Fact>]
let ``a cancelled name prompt dispatches nothing`` () =
    let sent, dispatch = capture ()

    Shell.interpret (noDeps ()) dispatch PromptForName

    Assert.Empty sent

[<Fact>]
let ``the name prompt uses the mkdir label`` () =
    let _, dispatch = capture ()
    let mutable asked = ""
    let deps = { noDeps () with AskName = fun title -> asked <- title; None }

    Shell.interpret deps dispatch PromptForName

    Assert.Equal(Shell.MkdirPrompt, asked)

[<Fact>]
let ``a selecting pattern uses the select label and carries the context back`` () =
    let sent, dispatch = capture ()
    let mutable asked = ""
    let deps = { noDeps () with AskPattern = fun title -> asked <- title; Some "*.fs" }

    Shell.interpret deps dispatch (PromptForPattern(Right, 4, true))

    Assert.Equal(Shell.SelectPrompt, asked)
    Assert.Equal<Message list>([ PatternEntered(Right, 4, true, "*.fs") ], List.ofSeq sent)

[<Fact>]
let ``a deselecting pattern uses the deselect label`` () =
    let _, dispatch = capture ()
    let mutable asked = ""
    let deps = { noDeps () with AskPattern = fun title -> asked <- title; None }

    Shell.interpret deps dispatch (PromptForPattern(Left, 0, false))

    Assert.Equal(Shell.DeselectPrompt, asked)

[<Fact>]
let ``a chosen drive is dispatched as an opened path`` () =
    let sent, dispatch = capture ()
    let deps = { noDeps () with ChooseOption = fun _ _ -> Some @"D:\" }

    Shell.interpret deps dispatch (PromptForDrive Right)

    Assert.Equal<Message list>([ PathOpened(Right, @"D:\") ], List.ofSeq sent)

[<Fact>]
let ``the drive chooser is offered every mounted drive`` () =
    let _, dispatch = capture ()
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start"
    fs.AddDirectory @"D:\data"
    let mutable offered = []
    let deps = { noDeps () with FileSystem = fs; ChooseOption = fun _ drives -> offered <- drives; None }

    Shell.interpret deps dispatch (PromptForDrive Left)

    Assert.Equal<string list>([ @"C:\"; @"D:\" ], offered)

[<Fact>]
let ``a listing error is reported under the listing title`` () =
    let mutable reported = ("", "")
    let deps = { noDeps () with ReportError = fun title message -> reported <- (title, message) }

    Shell.interpret deps ignore (ReportListingError(DirectoryListing.DirectoryNotFound(fp @"C:\gone")))

    Assert.Equal(ListingErrorMessage.Title, fst reported)
    Assert.Contains(@"C:\gone", snd reported)

[<Fact>]
let ``an operation error is reported under the operation title`` () =
    let mutable reported = ("", "")
    let deps = { noDeps () with ReportError = fun title message -> reported <- (title, message) }

    Shell.interpret deps ignore (ReportOperationError(TargetExists(fp @"C:\start\docs")))

    Assert.Equal(OperationErrorMessage.Title, fst reported)
    Assert.Contains("docs", snd reported)

[<Fact>]
let ``quit runs the quit port`` () =
    let mutable quit = false
    let deps = { noDeps () with OnQuit = fun () -> quit <- true }

    Shell.interpret deps ignore Quit

    Assert.True quit
