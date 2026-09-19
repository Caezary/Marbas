# Code quality review

Date: 2026-09-19. Scope: every source file under `Marbas/` read in full, the
test fakes and helpers (`Fakes.fs`, `Integration/Gui.fs`) and a sample of test
modules. Criteria, as asked: concise but readable code; comments as a smell;
one operation per line; pipelines over nested parentheses; flat structure over
cascaded `match`; computation expressions where they fit; F# idiom.

Severity: **High** = hurts every reader of a central file; **Medium** = a
pattern repeated across files; **Low** = local.

## Verdict

The code is idiomatic F#: immutable records and unions everywhere, single-case
types for names, paths and patterns, `Result` and `Option` instead of
exceptions and nulls, private helpers named for what they answer, and not a
single code comment in ~9,000 lines — the intent really does live in names,
tests and the documents. Formatting follows Fantomas conventions throughout.

What holds it back is concentrated: one 40-line block of primed aliases at the
heart of `Update`, a family of prepositional names that do not tell each other
apart, `match` cascades three deep in the places that decide what a key does,
computation expressions used in six places where twenty would read better, and
a handful of helpers copied three to seven times across files.

## Strengths

- **Names carry the rule.** `keepUnfinished`, `refusingInArchive`,
  `wouldPackItself`, `standingName`, `cancelledEntries` say what they answer;
  a reader rarely needs the body.
- **Domain types over primitives**, consistently: `EntryName`, `FileSystemPath`,
  `ArchivePath`, `NamePattern`, `ProgramPath`, `FileExtension`,
  `ArchivePassword`, `SearchText` are all private-constructor types with
  `tryCreate`/`value`.
- **Two-case unions instead of booleans** as a rule: `HiddenEntries`,
  `SelectionMode`, `CursorDirection`, `LetterCase`, `WordMatch`, `LinkWalk`,
  `Elevation`.
- **Error mapping in one place per family** (`Operations.mapException`,
  `ArchiveReader.mapException`, `ContentError.mapException`).
- **Tests** follow arrange/act/assert with a blank-line structure, bind the act
  result, use `[<Theory>]` for cases, and never branch (`Marbas.Tests/State/
  TransferTests.fs` is representative).

## Findings

### Q1. Forty primed aliases at the top of `Update.transition` — High

`Marbas/State/Update.fs:291-330`:

```fsharp
let applyListing' = applyListing state
let applyOperation' = applyOperation state
…
let wouldPackItself' = Archive.wouldPackItself fs
```

Forty `let x' = x fs` lines precede the `match`. They exist to save typing
`fs` at ~50 call sites, and they cost the reader a lookup per name (is
`makeDirectory'` the `AppState` one with `fs` and `state` applied, or only
`fs`? — it varies: `chosen'` binds both, `makeDirectory'` binds one), they
allocate ~40 closures on every message including `CursorMoved`, and the
prime convention leaks into `sentToRoot` (`Update.fs:175-183`) as
`loadPanel'`, `getRoot`.

Fix. Delete the block and pass `fs` at the call site:
`makeDirectory fs state.Active directory name state |> applyOperation state`.
Three characters per call, no indirection. Where a helper is called more than
three times with the same first two arguments, a local `let` next to the arm
that needs it is enough.

### Q2. `match` cascades three deep where the shape is a product — High

The decision code in `Update` and `Links` nests one `match` inside another
instead of matching the tuple or short-circuiting with `option {}`:

- `Update.opened` (`Update.fs:209-218`), `openedWith` (`233-245`),
  `runElevated` (`247-259`), `EditRequested` (`532-545`): each is
  `match a with | Some … | None -> match b, c with … | _ -> match d with …`.
- `Links.create` (`Marbas/FileSystem/Links.fs:78-94`): three levels.
- `Settings.tabsFrom` (`Settings.fs:268-282`), `Shell.planFor`
  (`Shell.fs:215-231`), `Shell.showHit` (`149-159`).

Example, `Links.create` today:

```fsharp
match FileSystemPath.kind fs location, FileSystemPath.kind fs target with
| FileEntry, _
| DirectoryEntry, _ -> Error(TargetExists location)
| NoEntry, NoEntry -> Error(SourceNotFound target)
| NoEntry, targetKind ->
    match refusalFor platform fs request target, request.Kind with
    | Some refused, _ -> Error(LinkRefused(refused, location))
    | None, Symbolic -> makeSymbolic fs location target targetKind
    | None, Hard -> attempting location (fun () -> native.MakeHard target location)
    | None, Junction ->
        match native.MakeJunction with
        | Some makeJunction -> attempting location (fun () -> makeJunction target location)
        | None -> Error(LinkRefused(KindUnavailable, location))
```

As a `result {}` with the refusals as guards:

```fsharp
let private refusing location refusal =
    match refusal with
    | Some refused -> Error(LinkRefused(refused, location))
    | None -> Ok()

let create native platform fs request =
    let target = absolute fs request
    let location = request.Location
    let targetKind = FileSystemPath.kind fs target

    result {
        do! FileSystemPath.kind fs location |> Result.requireEqualTo NoEntry (TargetExists location)
        do! Result.requireFalse (SourceNotFound target) (targetKind = NoEntry)
        do! refusalFor platform fs request target |> refusing location
        return! make native fs location target targetKind request.Kind
    }
```

(`Result.requireEqualTo` and `requireFalse` are FsToolkit functions the
project already references; `make` is the last three arms as a plain match on
the kind.)

### Q3. Parenthesis chains where a pipeline reads left to right — Medium

Sites where three or more constructors nest and the meaning is read inside-out:

| Site | Expression |
| --- | --- |
| `Update.fs:145` | `request state (ReportOperationError(Operations.ArchiveFailed(KindCannotBeChanged unchangeable)))` |
| `Update.fs:214` | `request state (ReportOperationError(Operations.ArchiveEntryNotRunnable(ArchivePath.spelled fs archive entry)))` |
| `Update.fs:231`, `273`, `498`, `537` | same shape |
| `Archive.fs:285` | `Error(Operations.SourceNotFound(ArchivePath.spelled fs request.Archive target))` |
| `DirectoryMeasure.fs:18` | `(fs.FileInfo.New(FileSystemPath.value path)).Length` |
| `Trash.fs:58-59` | `FileSystemPath.kind fs (FileSystemPath.combine fs (storedFiles fs trash) name) <> NoEntry` |
| `Operations.fs:362` | `approveInto (inDirectory fs targetDirectory) (fun target -> FileSystemPath.kind fs target <> NoEntry) ask sources` |
| `AppState.fs:636` | a 130-character `Result.bind (reloadFocusing fs (opposite state.Active) (ByName archive) (fun previous -> …))` |

`Update.fs:214` as a pipeline:

```fsharp
ArchivePath.spelled fs archive entry
|> Operations.ArchiveEntryNotRunnable
|> ReportOperationError
|> request state
```

The `ReportOperationError` family appears often enough that a
`let private reporting state error = request state (ReportOperationError error)`
would remove one level everywhere.

### Q4. Computation expressions are under-used — Medium

Six `result {}` blocks exist (`Operations.fs:211, 228, 248, 287`,
`Archive.fs:160`, `Trash.fs:105`). Places that are `Result`/`Option` chains
written as nested matches or `Result.bind` pipelines with lambdas:

- `AppState.finishTransfer`, `finishPack`, `finishLinks`, `makeFile`
  (`AppState.fs:618-621`, `634-637`, `639-649`, `672-680`): two or three
  `Result.bind (fun … -> …)` steps each; `result { let! a = …; let! b = …;
  return b, results }` keeps the two reloads on two lines.
- `Archive.copyAcross` (`Archive.fs:588-608`): a `match` over `changeRefusal`,
  a `match` over `Workspace.scratch`, a `try/finally`, then two `Result.bind`
  lambdas. `result {}` with `let! scratch = …` and `use`-style release flattens
  it to five lines.
- `Update.chosen` (`Update.fs:261-270`), `openedWith` (`233-245`),
  `Shell.planFor` (`215-231`), `Shell.PromptFor*` arms (`365-368`, `381-388`,
  `501-535`): `Option` chains that `option {}` (FsToolkit) expresses without
  the `| None -> ()` tail on every arm.
- `Workspace.scratch` (`Workspace.fs:26-37`).

Where the chain is two steps a pipeline is fine; the cut-off the project
should adopt is "three binds or one nested match → CE".

### Q5. Copies of the same helper across files — Medium

| Helper | Copies |
| --- | --- |
| "item is strictly below level" (`below`) | `DirectoryMeasure.fs:43`, `ArchiveListing.fs:9`, `FileSearch.fs:47`; plus the `>=` variant `Archive.underPath` (`Archive.fs:222`) |
| `parentEntry` record for `..` | `DirectoryListing.fs:121`, `ArchiveListing.fs:59`, `SearchListing.fs:68` |
| enumerate children as `FileSystemPath list` | `Operations.fs:106`, `DirectoryMeasure.fs:20`, `FileSearch.fs:20`, `Archive.fs:163-167` (TD-019) |
| `dialog.SubViews \|> Seq.pick (function :? T as v when v.Id = id -> Some v \| _ -> None)` | 26 occurrences in 13 `Ui` files |
| "which sources are approved to land in an archive level" | `Shell.approvedInto` (`161-175`), `approvedAcross` (`177-199`), `approvedEntries` (`116-125`) |
| open a `Source` and show it | `Shell.showPath` (`135-140`), `showEntry` (`142-147`) |
| ask the password then show | `Shell.showHit` (`153-159`), `ViewArchiveEntry` arm (`489-492`) |
| run a dialog and read it | `Prompts.askPattern`, `askName`, `askTarget`, `askPassword`, `editProperties`, `askArchive`, `askFind`, `askLink`, `askHandler`, `editPrograms`, `chooseTab` (`Prompts.fs:32-93`) — eleven copies of `use d = create …; app.Run d; read d` |
| lazily replace one `MockFileSystem` member | `Fakes.fs:29-35`, `69-75`, `89-95`, `117-127`, `159-169`, `235-248` |

Two concrete helpers pay for themselves immediately:

```fsharp
// Ui/Dialogs/Views.fs
let find<'view when 'view :> View> (id: string) (dialog: View) =
    dialog.SubViews |> Seq.pick (function
        | :? 'view as view when view.Id = id -> Some view
        | _ -> None)
```

```fsharp
// Adapters/Prompts.fs
let private running (app: IApplication) create read =
    use dialog = create ()
    app.Run dialog |> ignore
    read dialog

let askName app title suggestion =
    running app (fun () -> PromptDialog.create title "Name:" suggestion |> PromptDialog.selecting) PromptDialog.text
```

### Q6. Prepositional names that do not discriminate — Medium

Within the archive family the reader meets `below` (strictly deeper,
`DirectoryMeasure.fs:43`), `under` / `underPath` (at or deeper,
`Archive.fs:222-227`), `beneath` (segments after the target's parent,
`Archive.fs:229`), `within` (path prefix, `Archive.fs:212`), `at` (a `Sink`
re-rooted at a level, `Archive.fs:119`), `keyIn` (`114`), `into`
(`PanelLocation.into`) and `insideArchive` (`AppState.fs:502`). Each is a
one-word preposition whose exact boundary rule (strict or not, path or
segments, disk or archive) can only be learned by opening it. The same holds
for the verb family `changing` (`Update.fs:142`), `changing'`,
`changingArchive` (`Shell.fs:210`), `changeWith` and the record `Changing`
(`Archive.fs:354`, `478`), and for `opened` / `opened'` / `opening` /
`openedWith` (`Update.fs:201-245`). `guarding` is already on the tech-debt
list (TD-021).

Fix. Encode the boundary in the name: `strictlyBelow`, `atOrBelow`,
`pathAfterTarget`, `sinkAtLevel`; for the gates, `refuseUnchangeable`
(`Update`), `reportIfUnchangeable` (`Shell`), `withArchiveOpenForChange`
(`Archive`).

### Q7. `guarding` (TD-021) — a flat replacement — Medium

`Update.guarding state source receives proceed` (`Update.fs:275-282`) takes a
bare `bool`, a continuation, and is called from seven arms, each of which
gains one indentation level. The refusal it computes depends only on the
message and the two panels, so it can be decided once, before `transition`:

```fsharp
type private Gate =
    { Source: ResultsRefusal option
      Target: TargetGate }

and private TargetGate =
    | TargetMayBeResults
    | TargetMustBeDirectory

let private gateOf msg =
    match msg with
    | MakeDirectoryRequested -> Some { Source = Some MakingDirectory; Target = TargetMayBeResults }
    | NewFileRequested -> Some { Source = Some MakingFile; Target = TargetMayBeResults }
    | LinkRequested -> Some { Source = Some Linking; Target = TargetMayBeResults }
    | PackRequested -> Some { Source = Some Packing; Target = TargetMustBeDirectory }
    | UnpackRequested -> Some { Source = Some Unpacking; Target = TargetMustBeDirectory }
    | CopyRequested
    | MoveRequested -> Some { Source = None; Target = TargetMustBeDirectory }
    | DeleteRequested _ -> Some { Source = None; Target = TargetMayBeResults }
    | _ -> None

let private refusal state gate =
    match gate.Source, gate.Target with
    | Some refused, _ when showsResults (activePanel state) -> Some refused
    | _, TargetMustBeDirectory when showsResults (panel (opposite state.Active) state) -> Some Receiving
    | _ -> None

let update fs msg state =
    match gateOf msg |> Option.bind (refusal state) with
    | Some refused -> refusing state refused
    | None -> transition fs msg state
    |> watching state
```

The seven arms lose their `guarding' … (fun () ->` wrapper and one level of
indentation; the `bool` and the continuation disappear; the whole rule is
readable in one place.

### Q8. Booleans and strings where a type would carry the meaning — Medium

- `ArchiveCapabilities` is three `bool`s (`ArchiveKind.fs:15-18`); a
  `Set<ArchiveCapability>` (`Readable | Creatable | Changeable`) makes
  `capabilities kind |> Set.contains Changeable` read as the rule it is.
- `ArchiveItem.IsDirectory`, `IsEncrypted` (`ArchiveReader.fs:10-16`) —
  `Kind: EntryKind` already exists in `DirectoryListing`.
- `EntryProperties.setFlag flag (wanted: bool)` (`EntryProperties.fs:53`);
  `OpenWithChoice.Remember: bool` (`Handler.fs:5-7`).
- `LinkDialog.kindsOf` (`LinkDialog.fs:29-32`) recovers `LinkKind`s from the
  selector's *label strings* by reverse lookup; the dialog was built from a
  `kinds` list and should keep it.
- `ResultsWindow.standingRow` (`ResultsWindow.fs:103-110`) identifies the row
  under the cursor by `ToString()` of the row object and re-finds it by string
  equality after a refresh.
- `FindDialog.boxId box = $"find%O{box}"` (`FindDialog.fs:102`) — view ids
  derived from a union's `%O` rendering.
- Prompt ports typed `string -> string -> string option` (see architecture A4).

### Q9. Dense one-liners — Low

Lines that do three things:

- `FileSearch.fs:242` — `if deep && follows entry && not (crosses child) && entered (identityOf entry child) then` (four predicates, one with a side effect: `entered` mutates the visited set).
- `Update.fs:426` — `| trimmed, _, _ when showsResults panel && isBareName' trimmed -> refusing state Renaming`.
- `SearchListing.fs:59` — `| Some names -> names.Contains(ArchivePath.segments level @ [ hit.Entry.Name ])`.
- `Drives.fs:110-113` — a three-line boolean with `||`, `&&`, an index and `Array.contains`.
- `PanelWiring.fs:27` — `not key.IsCtrl && not key.IsAlt && key.AsRune.Value >= 32 && key.AsRune.Value <> 127`.
- `AppState.fs:596-597` — `operationTargets panel |> List.exists (fun entry -> entry.Kind = Directory && entryInArchive fs panel entry |> Option.isSome)` under an `||`.
- `ContentMatch.fs:35` — `| WholeWords when not atEnd && index + needle.Length = text.Length -> false`.

Each wants one named predicate per clause (`let private stepsInto entry child = …`).

### Q10. `FileSearch.run` is one 200-line imperative function — Medium

`Marbas/FileSystem/FileSearch.fs:60-264` holds four `mutable`s, twelve nested
closures, two `Dictionary`s, a `while` loop with `locked`/`running` flags
(`159-197`) and three mutually recursive walkers. It is correct and well
tested, and a walker with early exit is a fair place for mutation, but the
function has no seams: `scanArchive` (40 lines) cannot be tested or read on
its own, and the counters are updated from six places.

Fix. Thread a `Walk` record (`{ Counts; Stopped }`) through the recursion, or
at least lift `scanArchive` and `noteLevels` into a private module with
explicit inputs (`query`, `control`, `report`) and an explicit result.
`TextEncoding.isUtf8` (`TextEncoding.fs:40-54`) is the same pattern in
miniature and is fine as it is.

### Q11. Broad `with _ ->` — Low

23 sites. Most are the documented "an error is a reason to skip" rule in
adapters and walks. Four deserve a narrower net or a value: `Settings.save`
(`Settings.fs:360`, silent), `BatchRunner.runMeasuring` (`BatchRunner.fs:37`,
empties the answer), `Archive.needsPassword` (`Archive.fs:312`, a damaged
archive reads as "not protected"), `SearchListing.heldIn`
(`SearchListing.fs:41`, an unreadable archive keeps every hit as if verified).

### Q12. Partial access and `.Value` — Low

`DriveDialog.choice` (`DriveDialog.fs:72`) and `TabDialog.choice`
(`TabDialog.fs:39`) read `SelectedItem.Value`; `Prompts.chooseDrive` guards the
empty list one layer up. `ArchiveFormat.initial = List.head all`
(`ArchiveFormat.fs:44`) and `Links.sharedRefusal`'s `List.head`
(`Links.fs:117`) are safe by construction; `ProgramCommand.unquoted`
(`ProgramCommand.fs:29`) indexes `words[0]` after `Split`, which is never
empty. `Zipper.at` (`Zipper.fs:44`) clamps an out-of-range position silently
rather than answering `None`.

### Q13. Small style inconsistencies — Low

- Trailing whitespace: `Operations.fs:105`, `PathText.fs:26`; a double blank
  line at `AppState.fs:622-623`.
- Lambda in a pipeline: `Shell.fs:480` `|> fun outcome -> RenameFinished(newName, outcome)`.
- Parenthesised lambdas in a record where the neighbours are bare:
  `Fakes.fs:200-202`.
- `Update.fs:175-183` names a local `getRoot` where every other local is a
  noun or a primed alias.
- File-level `module` vs `namespace` + nested `module` is mixed
  (`DirectoryListing`, `Operations`, `Archive` on one side; `Links`, `Drives`,
  `Trash` on the other) — acceptable, but the rule is not written down.

### Q14. Tests — Low

The suite is the strongest part of the codebase: 2,173 cases, arrange/act/assert
by blank lines, `[<Theory>]` with `InlineData`, no branching, fakes that
subclass the mock rather than stub the interface. Two things: the six lazy
member overrides in `Fakes.fs` (Q5) want one helper; and integration tests
that assert a listing order by index (`leftList … SelectedItem`) will be the
first to break if `Entries` becomes an array (performance P1), which is the
right reason to make that change now rather than later.

## Summary table

| # | Finding | Severity |
| --- | --- | --- |
| Q1 | Forty primed aliases in `Update.transition` | High |
| Q2 | Three-deep `match` cascades in decision code | High |
| Q3 | Parenthesis chains instead of pipelines | Medium |
| Q4 | Computation expressions under-used | Medium |
| Q5 | Helpers copied 3-26 times | Medium |
| Q6 | Prepositional names that do not discriminate | Medium |
| Q7 | `guarding` (TD-021): decide the gate once, before `transition` | Medium |
| Q8 | Booleans and strings where a type carries the meaning | Medium |
| Q10 | `FileSearch.run` monolith | Medium |
| Q9 | Dense one-liners | Low |
| Q11 | Broad `with _ ->` at four sites | Low |
| Q12 | Partial access and `.Value` | Low |
| Q13 | Small style inconsistencies | Low |
| Q14 | Tests: one helper, one anticipated break | Low |
