module Marbas.Tests.State.ClipboardNameTests

open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Locations
open Marbas.Messages
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.ClipboardFixture

[<Fact>]
let ``the name of the entry under the cursor is put on the clipboard`` () =
    let fs = mockFs ()
    let state = started fs |> standingOn "notes.txt"

    let result = update fs NamesCopyRequested state

    Assert.Equal<Effect list>(copying [ "notes.txt" ], result.Effects)

[<Fact>]
let ``a directory is named without a separator after it`` () =
    let fs = mockFs ()
    let state = started fs |> standingOn "docs"

    let result = update fs NamesCopyRequested state

    Assert.Equal<Effect list>(copying [ "docs" ], result.Effects)

[<Fact>]
let ``the marked entries are named in the order of the listing, whatever the cursor stands on`` () =
    let fs = mockFs ()

    let state =
        started fs |> markedOn [ "readme.txt"; "docs" ] |> standingOn "notes.txt"

    let result = update fs NamesCopyRequested state

    Assert.Equal<Effect list>(copying [ "docs"; "readme.txt" ], result.Effects)

[<Fact>]
let ``nothing is copied with the parent entry under the cursor and nothing marked`` () =
    let fs = mockFs ()
    let state = started fs |> standingOn ".."

    let result = update fs NamesCopyRequested state

    Assert.Empty result.Effects

[<Fact>]
let ``the marks stay where they were after the names are copied`` () =
    let fs = mockFs ()
    let state = started fs |> markedOn [ "readme.txt"; "docs" ]

    let result = update fs NamesCopyRequested state

    Assert.Equal<Set<EntryName>>(state.Left.Selected, result.State.Left.Selected)

[<Fact>]
let ``each hit in a panel of search results gives its own name`` () =
    let fs = mockFs ()

    let state =
        started fs
        |> update fs (sentToPanel [ hitIn "start/a" "one.txt"; hitIn "start/b" "two.txt" ])
        |> _.State
        |> update fs (PatternEntered(Select, everything))
        |> _.State

    let result = update fs NamesCopyRequested state

    Assert.Equal<Effect list>(copying [ "one.txt"; "two.txt" ], result.Effects)

[<Fact>]
let ``the entries inside an archive are named as the archive holds them`` () =
    let fs = ArchiveFixture.packed ()

    let state =
        ArchiveFixture.loaded fs
        |> standInside fs Left (InArchive(ArchiveFixture.archive, ArchivePath.root))
        |> Expect.ok
        |> update fs (PatternEntered(Select, everything))
        |> _.State

    let result = update fs NamesCopyRequested state

    Assert.Equal<Effect list>(copying [ "docs"; "notes.txt" ], result.Effects)

[<Fact>]
let ``a clipboard the system does not offer is reported the first time`` () =
    let fs = mockFs ()

    let result = update fs (ClipboardRefused ClipboardUnavailable) (started fs)

    Assert.Equal<Effect list>([ Report(Problem.Clipboard ClipboardUnavailable) ], result.Effects)

[<Fact>]
let ``a clipboard the system does not offer is not reported again in the same session`` () =
    let fs = mockFs ()
    let reported = update fs (ClipboardRefused ClipboardUnavailable) (started fs)

    let result = update fs (ClipboardRefused ClipboardUnavailable) reported.State

    Assert.Empty result.Effects

[<Fact>]
let ``a clipboard that failed is reported every time`` () =
    let fs = mockFs ()
    let failure = ClipboardFailed "The clipboard is held by another program."
    let reported = update fs (ClipboardRefused failure) (started fs)

    let result = update fs (ClipboardRefused failure) reported.State

    Assert.Equal<Effect list>([ Report(Problem.Clipboard failure) ], result.Effects)
