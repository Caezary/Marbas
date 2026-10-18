module Marbas.Tests.State.Shell.ShellTypedInPopupsTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Programs
open Marbas.Profile
open Marbas.Domain
open Marbas.State
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.Shell.ShellFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private searched text : Ports.FindAsked =
    { Choice =
        Search
            { What = FindWhat.create (NameMasks.read "*.fs") None FilesOnly
              Scope = FromDirectory
              Hidden = Concealed
              Links = StopAtLinks
              Archives = SkipArchives }
      Masks = "*.fs"
      Text = text }

[<Fact>]
let ``the find popup offers the masks and the texts typed before`` () =
    let fs = MockFileSystem()
    typedEarlier fs FindMasks [ "*.md"; "*.fs" ]
    typedEarlier fs SearchedText [ "needle" ]
    let offered = ResizeArray<EnteredText list * EnteredText list>()

    let deps =
        { withFs fs with
            Prompts.AskFind =
                fun request ->
                    offered.Add(request.Masks, request.Texts)
                    None }

    Shell.interpret deps ignore (PromptForFind(NoDirectoryMarked, Concealed, None))

    Assert.Equal(([ typed "*.fs"; typed "*.md" ], [ typed "needle" ]), Assert.Single offered)

[<Fact>]
let ``the viewer is offered the texts searched for before`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes.txt", MockFileData "first")
    typedEarlier fs SearchedText [ "older"; "needle" ]
    let offered = ResizeArray<EnteredText list>()

    let deps =
        { withFs fs with
            Prompts.ShowFile = fun request -> offered.Add(request.Searches.Texts()) }

    Shell.interpret deps ignore (ViewFile(fp (at "start/notes.txt")))

    Assert.Equal<EnteredText list>([ typed "needle"; typed "older" ], Assert.Single offered)

[<Fact>]
let ``a text the viewer searched for is remembered with the texts searched for before`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes.txt", MockFileData "first")
    typedEarlier fs SearchedText [ "older" ]
    let sent, dispatch = capture ()

    let deps =
        { withFs fs with
            Prompts.ShowFile = _.Searches.Remember(typed "needle") }

    Shell.interpret deps dispatch (ViewFile(fp (at "start/notes.txt")))

    Assert.Equal<string list>([ "needle"; "older" ], rememberedIn fs SearchedText)
    Assert.Single sent |> ignore

[<Fact>]
let ``the viewer is offered the bytes searched for before, apart from the texts`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes.txt", MockFileData "first")
    typedEarlier fs SearchedText [ "needle" ]
    typedEarlier fs SearchedBytes [ "4D 5A" ]
    let offered = ResizeArray<EnteredText list>()

    let deps =
        { withFs fs with
            Prompts.ShowFile = fun request -> offered.Add(request.ByteSearches.Texts()) }

    Shell.interpret deps ignore (ViewFile(fp (at "start/notes.txt")))

    Assert.Equal<EnteredText list>([ typed "4D 5A" ], Assert.Single offered)

[<Fact>]
let ``bytes the viewer searched for are remembered in their own list`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes.txt", MockFileData "first")

    let deps =
        { withFs fs with
            Prompts.ShowFile = _.ByteSearches.Remember(typed "4D 5A") }

    Shell.interpret deps ignore (ViewFile(fp (at "start/notes.txt")))

    Assert.Equal<string list>([ "4D 5A" ], rememberedIn fs SearchedBytes)
    Assert.Empty(rememberedIn fs SearchedText)

[<Fact>]
let ``a search confirmed remembers its masks and its text as they were typed`` () =
    let fs = MockFileSystem()

    let deps =
        { withFs fs with
            Prompts.AskFind = fun _ -> Some(searched "needle ") }

    Shell.interpret deps ignore (PromptForFind(NoDirectoryMarked, Concealed, None))

    Assert.Equal<string list>([ "*.fs" ], rememberedIn fs FindMasks)
    Assert.Equal<string list>([ "needle " ], rememberedIn fs SearchedText)

[<Fact>]
let ``a search for names alone remembers no text`` () =
    let fs = MockFileSystem()

    let deps =
        { withFs fs with
            Prompts.AskFind = fun _ -> Some(searched "") }

    Shell.interpret deps ignore (PromptForFind(NoDirectoryMarked, Concealed, None))

    Assert.Empty(rememberedIn fs SearchedText)

[<Fact>]
let ``asking for the last results remembers nothing`` () =
    let fs = MockFileSystem()

    let deps =
        { withFs fs with
            Prompts.AskFind =
                fun _ ->
                    Some
                        { searched "needle" with
                            Choice = LastResults } }

    Shell.interpret deps ignore (PromptForFind(NoDirectoryMarked, Concealed, Some 3))

    Assert.False(fs.File.Exists(at "history.conf"))

[<Fact>]
let ``the pack popup offers the archives packed into before`` () =
    let fs = MockFileSystem()
    typedEarlier fs ArchiveTarget [ @"D:\one.zip" ]
    let offered = ResizeArray<EnteredText list>()

    let deps =
        { withFs fs with
            Prompts.AskArchive =
                fun request ->
                    offered.Add request.Remembered
                    None }

    Shell.interpret deps ignore (PromptForArchive(fp (at "backup/notes.zip")))

    Assert.Equal<EnteredText list>([ typed @"D:\one.zip" ], Assert.Single offered)

[<Fact>]
let ``an archive confirmed remembers where it was packed and never its password`` () =
    let fs = MockFileSystem()

    let packed =
        { Target = @"D:\one.zip"
          Format = ArchiveFormat.initial
          Compression = Normal
          Password = ArchivePassword.tryCreate "hunter2" }

    let deps =
        { withFs fs with
            Prompts.AskArchive = fun _ -> Some packed }

    Shell.interpret deps ignore (PromptForArchive(fp (at "backup/notes.zip")))

    Assert.Equal<string list>([ @"D:\one.zip" ], rememberedIn fs ArchiveTarget)
    Assert.DoesNotContain("hunter2", fs.File.ReadAllText(at "history.conf"))

[<Fact>]
let ``the link popup offers the places linked at before`` () =
    let fs = MockFileSystem()
    typedEarlier fs LinkDestination [ @"D:\links\one" ]
    let offered = ResizeArray<EnteredText list>()

    let deps =
        { withFs fs with
            Prompts.AskLink =
                fun request ->
                    offered.Add request.Remembered
                    None }

    Shell.interpret
        deps
        ignore
        (PromptForLink([ fp (at "start/notes.txt") ], fp (at "backup"), fp (at "backup/notes.txt")))

    Assert.Equal<EnteredText list>([ typed @"D:\links\one" ], Assert.Single offered)

[<Fact>]
let ``a link confirmed remembers where it was made`` () =
    let fs = MockFileSystem()

    let deps =
        { withFs fs with
            Prompts.AskLink =
                fun _ ->
                    Some
                        { Target = @"D:\links\one"
                          Kind = Symbolic } }

    Shell.interpret
        deps
        ignore
        (PromptForLink([ fp (at "start/notes.txt") ], fp (at "backup"), fp (at "backup/notes.txt")))

    Assert.Equal<string list>([ @"D:\links\one" ], rememberedIn fs LinkDestination)

[<Fact>]
let ``the open-with popup offers the programs chosen before`` () =
    let fs = MockFileSystem()
    typedEarlier fs OpenWithProgram [ "code.exe" ]
    let offered = ResizeArray<EnteredText list>()

    let deps =
        { withFs fs with
            Prompts.AskHandler =
                fun request ->
                    offered.Add request.Remembered
                    None }

    Shell.interpret deps ignore (ShowHandlers(fp (at "start/page.html"), None))

    Assert.Equal<EnteredText list>([ typed "code.exe" ], Assert.Single offered)

[<Fact>]
let ``a program confirmed in the open-with popup is remembered`` () =
    let fs = MockFileSystem()

    let choice =
        { Program = Expect.some (ProgramPath.tryCreate "firefox.exe")
          Use = ThisTimeOnly }

    let deps =
        { withFs fs with
            Prompts.AskHandler = fun _ -> Some choice }

    Shell.interpret deps ignore (ShowHandlers(fp (at "start/page.html"), None))

    Assert.Equal<string list>([ "firefox.exe" ], rememberedIn fs OpenWithProgram)

[<Fact>]
let ``popups of the larger dialogs cancelled remember nothing`` () =
    let fs = MockFileSystem()
    let deps = withFs fs

    Shell.interpret deps ignore (PromptForFind(NoDirectoryMarked, Concealed, None))
    Shell.interpret deps ignore (PromptForArchive(fp (at "backup/notes.zip")))

    Shell.interpret
        deps
        ignore
        (PromptForLink([ fp (at "start/notes.txt") ], fp (at "backup"), fp (at "backup/notes.txt")))

    Shell.interpret deps ignore (ShowHandlers(fp (at "start/page.html"), None))

    Assert.False(fs.File.Exists(at "history.conf"))
