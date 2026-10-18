module Marbas.Tests.State.Shell.ShellSearchTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Locations
open Marbas.Find
open Marbas.Domain
open Marbas.State
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.Shell.ShellFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private searchFor scope =
    { What = FindWhat.create (NameMasks.read "*.txt") None FilesOnly
      Scope = scope
      Hidden = Concealed
      Links = StopAtLinks
      Archives = SkipArchives }

let private answering landing =
    { Landing = landing
      Results =
        { Query = searchFor FromDirectory
          Ground = { Roots = []; OtherRoots = [] }
          Hits = []
          Outcome =
            FileSearch.Finished
                { Files = 0
                  Directories = 0
                  Skipped = 0 }
          Cursor = 0 } }

[<Fact>]
let ``the search runs over the ground its scope names`` () =
    let mutable asked = { Roots = []; OtherRoots = [] }

    let deps =
        { noDeps () with
            Runners.Search = fun request -> asked <- SearchRun.ground request.Run }

    Shell.interpret deps ignore (RunSearch(searchFor FromDirectory, OnDisk(fp (at "start")), []))

    Assert.Equal<PanelLocation list>([ OnDisk(fp (at "start")) ], asked.Roots)

[<Fact>]
let ``the directories of PATH are the ground of the scope that names them`` () =
    let mutable asked = { Roots = []; OtherRoots = [] }

    let deps =
        { noDeps () with
            Processes.PathDirectories = [ fp (at "tools") ]
            Runners.Search = fun request -> asked <- SearchRun.ground request.Run }

    Shell.interpret deps ignore (RunSearch(searchFor PathDirectories, OnDisk(fp (at "start")), []))

    Assert.Equal<PanelLocation list>([ OnDisk(fp (at "tools")) ], asked.Roots)

[<Fact>]
let ``what the results window answered becomes a message`` () =
    let sent, dispatch = capture ()
    let landing = At(Into(OnDisk(fp (at "start/docs"))))

    let deps =
        { noDeps () with
            Runners.Search = _.Finish(answering (Some landing)) }

    Shell.interpret deps dispatch (RunSearch(searchFor FromDirectory, OnDisk(fp (at "start")), []))

    Assert.Equal<Message list>([ SearchAnswered(answering (Some landing)) ], List.ofSeq sent)

[<Fact>]
let ``a hit the results window asks to view is read out of the file it names`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes.txt", MockFileData "first")
    let mutable shown = ""

    let hit: FindHit =
        { Location = OnDisk(fp (at "start"))
          Entry = DirectoryListing.entryFromInfo (fs.FileInfo.New(at "start/notes.txt")) }

    let deps =
        { noDeps () with
            FileSystem = fs
            Prompts.ShowFile = fun request -> shown <- FileSystemPath.value request.Source.Path
            Runners.Search = fun request -> request.View hit }

    Shell.interpret deps ignore (RunSearch(searchFor FromDirectory, OnDisk(fp (at "start")), []))

    Assert.Equal(at "start/notes.txt", shown)
