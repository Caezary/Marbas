# Architecture review

Date: 2026-09-19. Scope: every source file under `Marbas/` (91 files, ~9,000
lines) read in full, plus the architecture documents and the test suite layout
(`Marbas.Tests/`, ~19,000 lines). Verification on this machine: `dotnet build`
succeeds with zero warnings under `TreatWarningsAsErrors`; `dotnet test` runs
2,173 tests in 12 s — 2,164 passed, 9 skipped (platform-specific), 0 failed.

Severity scale: **High** = a trap that will bite as data grows or as the next
feature lands; **Medium** = a real weakness worth a task of its own;
**Low** = worth fixing when the file is next touched.

## Verdict

The architecture is sound and unusually well-documented. The layering
(`Adapters` → `Ui` → `State` → `FileSystem` → `Domain`) is enforced by compile
order, every side effect on the outside world goes through a port on
`Shell.Dependencies`, errors are values everywhere, and `PanelLocation` makes
the three kinds of place a panel can stand on explicit. The test suite covers
the logic layers without touching the disk.

The weaknesses are concentrated in four places: the transition/effect boundary
is drawn by convenience rather than by purity, which puts blocking I/O on the
UI thread; the `Effect` type carries ten cases that are special cases of three;
several identities are strings or bare names where a value with a place is
needed; and `Runtime` allows re-entrant dispatch.

## Strengths worth keeping

- **Ports are records of functions**, not interfaces, so a test overrides one
  field (`Fakes.dependencies`, `Marbas.Tests/Fakes.fs:171`). Every dialog,
  process and thread is behind one.
- **Errors as values with one mapping point.** `Operations.attempting`
  (`Marbas/FileSystem/Operations.fs:68`) is the single place an exception
  becomes an `OperationError`; `ArchiveReader.mapException` and
  `ContentError.mapException` do the same for their families.
- **Impossible states are unbuildable**: `Zipper<Tab>` guarantees one current
  tab; `FindWhat` cannot carry word/case options without text
  (`Marbas/Domain/FindQuery.fs:79`); `ArchivePath` cannot climb.
- **The watch is derived, not remembered**: `Update.watching`
  (`Marbas/State/Update.fs:594`) compares locations before and after every
  message instead of asking each transition to raise the effect.
- **The tests describe behaviour**, one module per module, with fakes that
  subclass `MockFileSystem` where `MockFileData` cannot express a failure.

## Findings

### A1. Transitions are not pure; the boundary is drawn by cost, not by kind — High

`CLAUDE.md` and `OVERVIEW.md` state that `AppState` transitions are pure. They
are deterministic given `IFileSystem`, but they read and write the disk:

- `AppState.loadLocationFocusing` (`Marbas/State/AppState.fs:210`) lists a
  directory, reads a whole archive index or verifies every search hit.
- `AppState.makeDirectory`, `makeEntryDirectory`, `makeFile`
  (`AppState.fs:664-680`) create entries; `applyProperties` (`AppState.fs:712`)
  writes attributes and timestamps.
- `AppState.deleteScope` (`AppState.fs:589-602`) enumerates every target
  directory, and `Update` calls it eagerly while building the confirmation
  (`Update.fs:362-366`).
- `AppState.enter` → `openArchive` (`AppState.fs:735-738`) reads the entire
  archive index synchronously.

Copy, move, delete, pack and search are effects that run on a worker under a
progress window; mkdir, properties, archive entry directory, every listing and
every archive index read are transitions on the UI thread. The rule that
decides which is which is "quick enough", which is not a property of the code
but of the data. The performance report (P7) lists where this already hurts.

Recommendation. Either (a) keep the design and correct the documentation to
say "transitions may read and write through `IFileSystem` but never through any
other port", or (b) move the reads that scale with data — archive index,
search-hit verification, `deleteScope` — into effects that answer with a
`…Loaded` message. Option (a) is honest and cheap; option (b) is what fixes the
UI stalls.

### A2. Ten `Effect` cases are special cases of three — High

`Update.Effect` (`Marbas/State/Update.fs:80-132`) has:

| Special case | General case it equals |
| --- | --- |
| `CopyTargets(s, d)` | `CopyPlaces([FromDisk s], ToDirectory d)` |
| `ExtractEntries(a, e, d)` | `CopyPlaces([FromArchive(a, e)], ToDirectory d)` |
| `AddEntries(a, l, s)` | `CopyPlaces([FromDisk s], ToArchive(a, l))` |
| `CopyEntriesAcross(a, e, t, l)` | `CopyPlaces([FromArchive(a, e)], ToArchive(t, l))` |
| `MoveTargets`, `MoveEntriesOut`, `MoveEntriesIn`, `MoveEntriesAcross` | `MovePlaces(…)` |
| `DeleteTargets`, `RemoveEntries` | `DeletePlaces(…)` |

`Shell.copyPiece`, `movePiece` and `deletePiece` (`Marbas/Ui/App/Shell.fs:270-319`)
already implement every pairing once. The single-place arms of
`Shell.interpret` (`Shell.fs:350-364`, `411-463`) reimplement the same
pairings a second time, ~110 lines, and `Update.CopyRequested` /
`MoveRequested` (`Update.fs:382-413`) need seven arms each to pick the right
case. Two behaviours differ slightly between the two paths — the single-place
path reports an unchangeable archive with a dialog (`changingArchive`), the
places path reports it as a failed item (`gated`) — which is the kind of drift
duplication produces.

Recommendation. Keep `CopyPlaces`, `MovePlaces`, `DeletePlaces`; delete the
ten special cases; let `Update` build `[FromDisk paths]` for the single-place
case. `interpret` shrinks by a third and the two refusal behaviours become one.

### A3. `Runtime.Dispatch` is re-entrant — Medium

`Runtime.Dispatch` (`Marbas/State/Runtime.fs:22-26`) updates, paints, then runs
`interpret` for each effect *synchronously*, and `Shell.interpret` calls
`dispatch` from inside an effect (`Shell.fs:349`, `356`, `367`, `372`, `378`,
`400`, `410`, `481`, `493`, `494`, `503`, `507`, `511`, `515` …). A nested
`Dispatch` therefore runs to completion — including its own effects — before
the outer loop continues with the *rest of the outer effect list*, and the
outer `WatchLocations` appended by `watching` (`Update.fs:598`) is computed
from the outer state, not the nested one.

Today no transition both moves a panel and raises an effect that dispatches
synchronously, so nothing observable goes wrong. It is a trap for the next
feature: a stale `WatchLocations` interpreted after a fresh one would silently
re-arm the watch on the wrong directory. The synchronous nesting also makes
`RunBatch` in tests (which runs inline) recurse several dispatches deep.

Recommendation. Give `Runtime` a message queue: `Dispatch` enqueues, and one
drain loop runs update → paint → interpret until the queue is empty. Effects
then always see the state they were raised from, and re-entrancy becomes
impossible rather than merely unexercised.

### A4. `Shell.Dependencies` is a 36-field record with stringly-typed ports — Medium

`Shell.Dependencies` (`Marbas/Ui/App/Shell.fs:49-85`) mixes four concerns:
the file system and paths, eleven prompts, three process ports, two watches,
two runners. Four prompts share the type `string -> string -> string option`
(`AskName`, `AskTarget`, `AskPassword`; `AskPattern` is `string -> string
option`), and `ReportError`, `Confirm` and `AskOverwrite` are all
`string -> string -> …`. Wiring `AskTarget` where `AskName` was meant compiles.
The answer type of `AskFind` is `FindDialog.FindChoice` (`Shell.fs:66`), so the
port contract depends on a dialog module in `Ui.Dialogs` rather than on a value
in `Domain`/`FileSystem`.

Recommendation. Split into sub-records (`Prompts`, `Processes`, `Watches`,
`Runners`) and give each prompt a request type (`{ Title; Label; Suggestion }`)
and a typed answer. Move `FindChoice` next to `FindQuery`.

### A5. `ArchiveItem.WriteTo` is only valid while the enumeration lives — Medium

`ArchiveReader.items` (`Marbas/FileSystem/ArchiveReader.fs:72-91`) yields
records whose `WriteTo` closure captures an `IArchiveEntry` or `IReader` that is
disposed when the sequence ends (`use stream`, `use archive`, `use reader`
inside the `seq`). For tar kinds the closure is invalid as soon as the reader
moves to the next entry. Every current consumer happens to call `WriteTo`
inside the enumeration (`Archive.walking`, `copyKept`, `FileWindow.contentOf`,
`FileSearch.scanArchive`), and `Archive.intoUpdate` defers
`Content()` to `CommitUpdate` (`Archive.fs:380`, `407`) but only ever receives
disk-backed `Stored` values. Nothing in the types says any of this; a
`List.ofSeq` followed by a `WriteTo` compiles and throws
`ObjectDisposedException` at run time.

Recommendation. Replace the lazily-evaluated record with a scoped API:
`ArchiveReader.withItems fs password kind path (fun items -> …)`, or split the
metadata (`ArchiveEntryInfo`, safe to keep) from the content accessor (handed
out only inside a scope).

### A6. `ItemResult` is keyed by bare name — Medium

`Operations.ItemResult` (`Marbas/FileSystem/Operations.fs:27-29`) identifies an
item by `EntryName` alone. Three places build maps or sets keyed on it:
`AppState.keepUnfinished` (`AppState.fs:377-385`), `Archive.droppingArrived`
(`Archive.fs:544-562`) and `Archive.carryingOver` (`Archive.fs:579-586`).
Since P14.13 a batch can span several places (`FromPlaces`), so two sources
named `x.txt` in different directories collide: a failure of one is applied to
the other's mark, and `droppingArrived` may drop the wrong entry from the
archive.

Recommendation. Carry the source (`FileSystemPath` or `PanelLocation *
EntryName`) on `ItemResult`, and key the maps on it.

### A7. Six copies of "is this path under that root", with two different case rules — Medium

| Site | Comparison |
| --- | --- |
| `Archive.within` (`Archive.fs:212-216`) | `OrdinalIgnoreCase`, every platform |
| `Drives.holding` (`Drives.fs:103-119`) | `OrdinalIgnoreCase` (documented as deliberate) |
| `SearchListing.relativeTo` (`SearchListing.fs:8-16`) | `OrdinalIgnoreCase` |
| `FileSearch.run` boundaries (`FileSearch.fs:64-70`) | `OrdinalIgnoreCase` |
| `FindGround.sameRoot` (`FindGround.fs:18-19`) | `OrdinalIgnoreCase` |
| `Links.volumeOf` (`Links.fs:32-52`) | platform-aware |

On Linux, `wouldPackItself` refuses to pack `/home/u/Docs` into
`/home/u/docs/x.zip` because `within` ignores case, and a search bounded by
`/media/Stick` also refuses to enter `/media/stick`. Conversely
`FileSearch.visited` (`FileSearch.fs:61`) is a case-*sensitive* `HashSet`, so
on Windows a link whose target is spelled in a different case is entered
twice. TD-020 records the duplication; the inconsistency is the trap.

Recommendation. One `FileSystemPath.isUnder platform` (or a `PathComparison`
value carried in `Dependencies`) used by all six.

### A8. Long synchronous work on the UI thread without a progress window — Medium

- Permanent delete and trash (`Shell.fs:355-360`) walk the tree inline
  (`Operations.deleteTree`, `RecycleBin.send`); `Esc` cannot stop them and the
  screen freezes.
- Rename (`Shell.fs:478-481` → `Operations.rename` → `moveTo`,
  `Operations.fs:283-310`) copies a directory tree and deletes it, inline.
- Opening an archive, every level change inside it, `Ctrl+R` there, and every
  external change to the archive file re-read the whole index inline
  (`ArchiveListing.read`, `ArchiveListing.fs:55`).
- A results panel re-verifies every hit and re-opens every archive on each
  refresh (`SearchListing.surviving`, `SearchListing.fs:44-63`).

These follow from A1 and are quantified in the performance report (P4-P7).

### A9. Silent failures — Low

`Settings.save` (`Settings.fs:354-361`) swallows every exception, so a session
that could not be written is indistinguishable from one that was.
`BatchRunner.runMeasuring` (`BatchRunner.fs:37`) turns any exception into an
empty measurement. `Archive.needsPassword` (`Archive.fs:307-314`) reads a
damaged archive as "no password needed", so the damage is reported later by a
different path with a different message.

Recommendation. `save` should answer a `Result` that `QuitRequested` /
`SessionSaveRequested` can report.

### A10. `Domain` depends on `System.IO` types — Low

`Domain/HiddenEntries.fs:3`, `Domain/Executability.fs:3` and
`Domain/NamePattern.fs:4` take `FileAttributes`, `UnixFileMode` and use
`FileSystemName.MatchesSimpleExpression`. The innermost layer therefore knows
the BCL's file model. It is harmless today; it means a `Domain` value cannot be
built without those enums, which is why `ArchiveListing.fileEntry` has to pass
`FileAttributes.None` and `UnixFileMode.None`.

### A11. `AppState.fs` carries three roles — Low

At 780 lines it holds (1) pure panel manipulation — cursor, marks, search,
sort; (2) `IFileSystem`-backed loaders and `finish*` transitions; (3) query
helpers `transferSource`, `cursor*`, `operationPaths`, `measureTargets`. The
first group never mentions `fs` and would be a `PanelState` module with its
own tests; the third is what `Update` asks before raising an effect.

### A12. Documentation drift — Low

`OVERVIEW.md:4` and `CLAUDE.md` say transitions are pure (see A1).
`TESTING.md:6` says no test touches the disk, `TESTING.md:15` says `Disk/`
does. The `FILE-OPERATIONS.md` note "Moving a directory is copy-then-delete"
gives the cross-volume reason but not the same-volume cost (performance P5).

### A13. Undisposed resources in the runners — Low

`BatchRunner.drive` (`BatchRunner.fs:10-11`) creates a
`CancellationTokenSource` and a `ProgressDialog` and disposes neither;
`SearchRunner.run` (`SearchRunner.fs:61`) disposes the window but not the
token source. One leak per batch; Terminal.Gui views hold event subscriptions.

### A14. Rendering is coupled to every message — Medium

`Runtime.Dispatch` paints after every message, and `AppView.render`
(`Marbas/Ui/App/AppView.fs:34-47`) rebuilds both panels' row collections each
time, including for `CursorMoved`. The architecture has no notion of "what
changed". This is the largest performance item (P1) and is an architectural
choice rather than a local bug: a `render` that receives the previous state as
well as the new one, or per-panel memoisation on the `PanelState` reference,
fixes it without touching the loop.

## Summary table

| # | Finding | Severity |
| --- | --- | --- |
| A1 | Transition/effect boundary drawn by cost; I/O inside transitions | High |
| A2 | Ten redundant `Effect` cases duplicate the `Piece` machinery | High |
| A3 | Re-entrant `Runtime.Dispatch` | Medium |
| A4 | 36-field `Dependencies`, stringly-typed prompt ports | Medium |
| A5 | `ArchiveItem.WriteTo` valid only inside the enumeration | Medium |
| A6 | `ItemResult` keyed by bare name | Medium |
| A7 | Six path-prefix checks with inconsistent case rules | Medium |
| A8 | Blocking work on the UI thread | Medium |
| A14 | Render coupled to every message | Medium |
| A9 | Silent failures in `Settings.save`, `runMeasuring`, `needsPassword` | Low |
| A10 | `Domain` depends on `System.IO` enums | Low |
| A11 | `AppState.fs` carries three roles | Low |
| A12 | Documentation drift | Low |
| A13 | Undisposed runner resources | Low |
