module Marbas.Tests.State.ClipboardFixture

open System
open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "start/readme.txt", MockFileData "hi")
    fs.AddFile(at "start/a/one.txt", MockFileData "1")
    fs.AddFile(at "start/b/two.txt", MockFileData "2")
    fs

let started (fs: IFileSystem) =
    create (fp (at "start"))
    |> loadPanel fs Left (fp (at "start"))
    |> Expect.ok
    |> loadPanel fs Right (fp (at "start"))
    |> Expect.ok

let private indexOf name state =
    (activePanel state).Entries
    |> Array.findIndex (fun entry -> EntryName.value entry.Name = name)

let standingOn name state =
    setCursor Left (indexOf name state) state

let markedOn names state =
    names
    |> List.fold (fun marked name -> marked |> standingOn name |> toggleSelection Left) state

let copying lines =
    [ PutOnClipboard(ClipboardText.tryCreate lines |> Expect.some) ]

let file name =
    { Name = EntryName.create name
      Kind = File
      Size = Some 1L
      Measured = None
      Modified = DateTimeOffset.MinValue
      Visibility = Plain
      Executability = NotExecutable
      LinkTarget = None
      Verification = Verified }

let hitIn where name =
    { Location = OnDisk(fp (at where))
      Entry = file name }

let sentToPanel hits =
    SearchAnswered
        { Landing = Some InPanel
          Results =
            { Query =
                { What = FindWhat.create (NameMasks.read "*.txt") None FilesOnly
                  Scope = FromDirectory
                  Hidden = Concealed
                  Links = StopAtLinks
                  Archives = SkipArchives }
              Ground =
                { Roots = [ OnDisk(fp (at "start")) ]
                  OtherRoots = [] }
              Hits = hits
              Outcome =
                FileSearch.Finished
                    { Files = List.length hits
                      Directories = 0
                      Skipped = 0 }
              Cursor = 0 } }

let everything = NamePattern.tryCreate "*" |> Expect.some

let docs = ArchivePath.tryCreate "docs" |> Expect.some
