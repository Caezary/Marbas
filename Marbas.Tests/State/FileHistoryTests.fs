module Marbas.Tests.State.FileHistoryTests

open System.IO.Abstractions
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Locations
open Marbas.FileSystem.DirectoryListing
open Marbas.Messages
open Marbas.State
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.ArchiveFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private en = EntryName.create

let private inside raw =
    ArchivePath.tryCreate raw |> Expect.some

let private Docs = 1

let private Notes = 2

let private inWork (fs: IFileSystem) =
    loaded fs |> loadPanel fs Left (fp (at "work")) |> Expect.ok

let private remembered place how =
    ChangeHistory(Remember(Opened { Where = place; How = how }))

let private historyChanges effects =
    effects
    |> List.choose (fun effect ->
        match effect with
        | ChangeHistory change -> Some change
        | _ -> None)

let private revisited visit =
    HistoryAnswered
        { Tab = ViewHistoryTab
          Choice = Some(Revisit visit) }

let private notesOnDisk = Place.OnDisk(fp (at "work/notes.txt"))

let private notesInArchive = Place.InArchive(archive, inside "notes.txt")

[<Fact>]
let ``a file viewed is remembered as viewed before the viewer opens`` () =
    let fs = packed ()
    let state = inWork fs |> setCursor Left Notes

    let result = update fs ViewRequested state

    Assert.Equal<Effect list>([ remembered notesOnDisk Viewed; ViewFile(fp (at "work/notes.txt")) ], result.Effects)

[<Fact>]
let ``a file viewed inside an archive is remembered inside it`` () =
    let fs = packed ()
    let state = standingInside fs Left |> setCursor Left Notes

    let result = update fs ViewRequested state

    Assert.Equal<Effect list>(
        [ remembered notesInArchive Viewed
          ViewArchiveEntry(archive, inside "notes.txt") ],
        result.Effects
    )

[<Fact>]
let ``a directory measured with the view key is not remembered`` () =
    let fs = packed ()
    let state = inWork fs |> setCursor Left Docs

    let result = update fs ViewRequested state

    Assert.Empty(historyChanges result.Effects)

[<Fact>]
let ``a file edited is remembered as edited before the editor runs`` () =
    let fs = packed ()
    let state = inWork fs |> setCursor Left Notes

    let result = update fs EditRequested state

    Assert.Equal<Effect list>(
        [ remembered notesOnDisk Edited; EditFile(fp (at "work/notes.txt"), None) ],
        result.Effects
    )

[<Fact>]
let ``a new file is remembered as edited before its editor runs`` () =
    let fs = packed ()
    let state = inWork fs

    let result = update fs (FileNameEntered(en "draft.md")) state

    Assert.Equal<Effect list>(
        [ remembered (Place.OnDisk(fp (at "work/draft.md"))) Edited
          EditFile(fp (at "work/draft.md"), None) ],
        result.Effects
    )

[<Fact>]
let ``a directory whose properties the edit key shows is not remembered`` () =
    let fs = packed ()
    let state = inWork fs |> setCursor Left Docs

    let result = update fs EditRequested state

    Assert.Empty(historyChanges result.Effects)

[<Fact>]
let ``an entry inside an archive that refuses to be edited is not remembered`` () =
    let fs = packed ()
    let state = standingInside fs Left |> setCursor Left Notes

    let result = update fs EditRequested state

    Assert.Empty(historyChanges result.Effects)

[<Fact>]
let ``a file viewed again from the history is viewed and remembered again`` () =
    let fs = packed ()
    let state = inWork fs

    let result = update fs (revisited { Where = notesOnDisk; How = Viewed }) state

    Assert.Equal<Effect list>([ remembered notesOnDisk Viewed; ViewFile(fp (at "work/notes.txt")) ], result.Effects)

[<Fact>]
let ``an archive entry viewed again from the history is viewed inside the archive`` () =
    let fs = packed ()
    let state = inWork fs

    let result = update fs (revisited { Where = notesInArchive; How = Viewed }) state

    Assert.Equal<Effect list>(
        [ remembered notesInArchive Viewed
          ViewArchiveEntry(archive, inside "notes.txt") ],
        result.Effects
    )

[<Fact>]
let ``a file edited again from the history goes to the editor its extension names`` () =
    let fs = packed ()
    let chosen = Expect.some (ProgramPath.tryCreate "code.exe")

    let programs =
        ProgramSettings.associate ForEditing (Expect.some (FileExtension.tryCreate "txt")) chosen ProgramSettings.none

    let state = inWork fs |> withPrograms programs

    let result = update fs (revisited { Where = notesOnDisk; How = Edited }) state

    Assert.Equal<Effect list>(
        [ remembered notesOnDisk Edited
          EditFile(fp (at "work/notes.txt"), Some chosen) ],
        result.Effects
    )

[<Fact>]
let ``an archive entry edited from the history is refused the way the panel refuses it`` () =
    let fs = packed ()
    let state = inWork fs

    let result = update fs (revisited { Where = notesInArchive; How = Edited }) state

    Assert.Equal<Effect list>(
        [ Report(Problem.Operation(Operations.ArchiveEntryNotEditable(fp (at "start/backup.zip/notes.txt")))) ],
        result.Effects
    )

[<Fact>]
let ``a file that is gone is handed to the viewer to report and not remembered again`` () =
    let fs = packed ()
    let state = inWork fs
    let gone = Place.OnDisk(fp (at "work/gone.txt"))

    let result = update fs (revisited { Where = gone; How = Viewed }) state

    Assert.Equal<Effect list>([ ViewFile(fp (at "work/gone.txt")) ], result.Effects)

[<Fact>]
let ``an entry of an archive that is gone is not remembered again`` () =
    let fs = packed ()
    let state = inWork fs
    let gone = Place.InArchive(fp (at "work/gone.zip"), inside "notes.txt")

    let result = update fs (revisited { Where = gone; How = Viewed }) state

    Assert.Empty(historyChanges result.Effects)

[<Fact>]
let ``going to a file viewed inside an archive stands the active panel beside it`` () =
    let fs = packed ()
    let state = inWork fs

    let result =
        update
            fs
            (HistoryAnswered
                { Tab = ViewHistoryTab
                  Choice = Some(GoTo notesInArchive) })
            state

    Assert.Equal(InArchive(archive, ArchivePath.root), result.State.Left.Path)

    Assert.Equal(
        Some "notes.txt",
        PanelState.cursorEntry result.State.Left
        |> Option.map (_.Name >> EntryName.value)
    )
