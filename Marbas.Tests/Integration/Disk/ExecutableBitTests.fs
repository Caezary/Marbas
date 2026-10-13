module Marbas.Tests.Integration.Disk.ExecutableBitTests

open System.IO
open System.IO.Abstractions
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Adapters
open Marbas.Tests

[<Literal>]
let private ModeBits = "The executable bit of a file is a Unix concept"

let private disk = FileSystem() :> IFileSystem

let private ownerRuns =
    UnixFileMode.UserRead ||| UnixFileMode.UserWrite ||| UnixFileMode.UserExecute

let private withProgram () =
    let scratch = new Scratch()
    scratch.File "tool" "#!/bin/sh\n"
    scratch.File "notes" "text"
    File.SetUnixFileMode(scratch.At "tool", ownerRuns)
    scratch

let private executabilityOf name (entries: DirectoryListing.Entry list) =
    (entries |> List.find (fun entry -> entry.Name = EntryName.create name)).Executability

let private opening (scratch: Scratch) name =
    { Target = scratch.Path name
      Program = None
      WorkingDirectory = scratch.Path ""
      Elevation = AsUser }

[<UnixFact(ModeBits)>]
let ``a file given the executable bit is listed as a program`` () =
    use scratch = withProgram ()

    let entries = DirectoryListing.read disk (scratch.Path "") |> Expect.ok

    Assert.Equal(Executable, executabilityOf "tool" entries)

[<UnixFact(ModeBits)>]
let ``a file left without the bit is listed as a plain file`` () =
    use scratch = withProgram ()

    let entries = DirectoryListing.read disk (scratch.Path "") |> Expect.ok

    Assert.Equal(NotExecutable, executabilityOf "notes" entries)

[<UnixFact(ModeBits)>]
let ``a link to a program is listed as a program`` () =
    use scratch = withProgram ()
    File.CreateSymbolicLink(scratch.At "shortcut", scratch.At "tool") |> ignore

    let entries = DirectoryListing.read disk (scratch.Path "") |> Expect.ok

    Assert.Equal(Executable, executabilityOf "shortcut" entries)

[<UnixFact(ModeBits)>]
let ``a link that points at nothing is listed as a plain file and the listing stands`` () =
    use scratch = withProgram ()
    File.CreateSymbolicLink(scratch.At "dangling", scratch.At "nowhere") |> ignore

    let entries = DirectoryListing.read disk (scratch.Path "") |> Expect.ok

    Assert.Equal(NotExecutable, executabilityOf "dangling" entries)
    Assert.Equal(Executable, executabilityOf "tool" entries)

[<UnixFact(ModeBits)>]
let ``a file given the executable bit is started from its own file`` () =
    use scratch = withProgram ()

    let described =
        LaunchProcess.describe disk Unix WindowPrograms.none (opening scratch "tool")

    let start = Expect.ok described
    Assert.Equal(scratch.At "tool", start.FileName)
    Assert.Empty start.ArgumentList

[<UnixFact(ModeBits)>]
let ``a file left without the bit is handed to the desktop's opener`` () =
    use scratch = withProgram ()

    let described =
        LaunchProcess.describe disk Unix WindowPrograms.none (opening scratch "notes")

    let start = Expect.ok described
    Assert.Equal(LaunchProcess.DesktopOpener, start.FileName)
    Assert.Equal<string list>([ scratch.At "notes" ], List.ofSeq start.ArgumentList)
