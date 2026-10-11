module Marbas.Tests.State.Update.UpdatePackTests

open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Domain
open Marbas.Messages
open Marbas.State
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.Update.UpdateFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private en = EntryName.create

let private zipFormat = ArchiveFormat.tryCreate Zip |> Expect.some

let private packRequest path =
    { Path = fp path
      Format = zipFormat
      Compression = Normal
      Password = None }

let private chose target =
    { Target = target
      Format = zipFormat
      Compression = Normal
      Password = None }

let private marking state =
    state |> toggleSelectionMoving Downward Left

[<Fact>]
let ``pack requested suggests an archive named after the cursor entry`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2

    let result = update fs PackRequested state

    Assert.Equal<Effect list>([ PromptForArchive(fp (at "backup/notes.zip")) ], result.Effects)

[<Fact>]
let ``pack requested with several marks suggests the name of the directory`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2 |> marking |> marking

    let result = update fs PackRequested state

    Assert.Equal<Effect list>([ PromptForArchive(fp (at "backup/start.zip")) ], result.Effects)

[<Fact>]
let ``pack requested on the parent entry raises no effect`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let result = update fs PackRequested state

    Assert.Equal(state, result.State)
    Assert.Empty result.Effects

[<Fact>]
let ``an entered archive is packed where it was typed`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2

    let result = update fs (ArchiveEntered(chose (at "backup/all.zip"))) state

    Assert.Equal<Effect list>(
        [ ArchiveTargets([ fp (at "start/notes.txt") ], packRequest (at "backup/all.zip")) ],
        result.Effects
    )

[<Fact>]
let ``a bare archive name lands beside the entries it packs`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2

    let result = update fs (ArchiveEntered(chose "all.zip")) state

    Assert.Equal<Effect list>(
        [ ArchiveTargets([ fp (at "start/notes.txt") ], packRequest (at "start/all.zip")) ],
        result.Effects
    )

[<Fact>]
let ``an archive that would land inside what it packs is refused`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 1

    let result = update fs (ArchiveEntered(chose (at "start/docs/all.zip"))) state

    Assert.Equal<Effect list>(
        [ Report(Problem.Operation(TargetWithinSource(fp (at "start/docs/all.zip")))) ],
        result.Effects
    )

[<Fact>]
let ``a blank archive name raises no effect`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2

    let result = update fs (ArchiveEntered(chose "  ")) state

    Assert.Empty result.Effects

[<Fact>]
let ``a finished pack puts the cursor on the archive in the other panel`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2

    let results =
        Archive.write fs unattended (packRequest (at "backup/notes.zip")) [ fp (at "start/notes.txt") ]

    let result = update fs (PackFinished(en "notes.zip", results)) state

    Assert.Contains("notes.zip", names result.State.Right.Entries)

    Assert.Equal(
        Some "notes.zip",
        PanelState.cursorEntry result.State.Right
        |> Option.map (_.Name >> EntryName.value)
    )

    Assert.Empty result.Effects

[<Fact>]
let ``a finished pack reports the items that failed`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let failed =
        [ { Source = ItemOnDisk(fp (at "start/notes.txt"))
            Outcome = Error(AccessDenied(fp (at "start/notes.txt"))) } ]

    let result = update fs (PackFinished(en "notes.zip", failed)) state

    Assert.Equal<Effect list>([ Report(Problem.ItemFailures failed) ], result.Effects)

[<Fact>]
let ``a finished pack keeps the marks of what did not make it`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2 |> marking

    let failed =
        [ { Source = ItemOnDisk(fp (at "start/notes.txt"))
            Outcome = Error(AccessDenied(fp (at "start/notes.txt"))) } ]

    let result = update fs (PackFinished(en "notes.zip", failed)) state

    Assert.Equal<string list>(
        [ "notes.txt" ],
        PanelState.selectedEntries result.State.Left
        |> Seq.map (_.Name >> EntryName.value)
        |> List.ofSeq
    )

[<Fact>]
let ``the password from the dialog rides into the request`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2
    let secret = ArchivePassword.tryCreate "letmein"

    let result =
        update
            fs
            (ArchiveEntered
                { chose (at "backup/all.zip") with
                    Password = secret })
            state

    Assert.Equal<Effect list>(
        [ ArchiveTargets(
              [ fp (at "start/notes.txt") ],
              { packRequest (at "backup/all.zip") with
                  Password = secret }
          ) ],
        result.Effects
    )

[<Fact>]
let ``unpack requested sends the targets to the other panel`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2

    let result = update fs UnpackRequested state

    Assert.Equal<Effect list>([ ExtractTargets([ fp (at "start/notes.txt") ], fp (at "backup")) ], result.Effects)

[<Fact>]
let ``unpack requested on the parent entry raises no effect`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let result = update fs UnpackRequested state

    Assert.Equal(state, result.State)
    Assert.Empty result.Effects

[<Fact>]
let ``a finished unpack shows what came out of the archive`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2
    let results = copyAll fs unattended [ fp (at "start/notes.txt") ] (fp (at "backup"))

    let result = update fs (UnpackFinished results) state

    Assert.Contains("notes.txt", names result.State.Right.Entries)
    Assert.Empty result.Effects

[<Fact>]
let ``a finished unpack reports the archives that failed`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let failed =
        [ { Source = ItemOnDisk(fp (at "start/data.bin"))
            Outcome = Error(ArchiveFailed(KindCannotBeRead(fp (at "start/data.bin")))) } ]

    let result = update fs (UnpackFinished failed) state

    Assert.Equal<Effect list>([ Report(Problem.ItemFailures failed) ], result.Effects)
