# Phase 15 — Answering the reviews

The tasks are in [Phase 15 — Answering the reviews](../Plan/PHASE-15-ANSWERING-THE-REVIEWS.md).

**P15.1 gave a batch result a place to come from, and the collisions went with
the bare name.** `Operations.ItemResult` carried an `EntryName` and nothing
else, which was enough while a batch worked on one directory and stopped being
enough the moment P14.13 let one run over several. Two hits called `same.txt` in
different directories answered under one name, so a failure of one was read as a
failure of both and the results panel kept two marks where it should have kept
one. The name is now an `ItemSource`: a `FileSystemPath` for an item on disk,
the archive and the entry's `ArchivePath` for one inside an archive, and
`WholeBatch` for the net `Operations.guarded` throws under a batch that raised.
Where a name is still wanted — the failures dialog — `ItemSource.name` answers
it, which is the one place that translation now happens.

The three maps the review named followed the type. `AppState.keepUnfinished`
resolves each marked name to the source the panel would operate on, through the
same `PanelLocation.into` the operation paths are built with, and compares
sources rather than names; a results panel resolves through its hit first, so a
mark inside an archive and a mark on disk of one name no longer trade outcomes.
`Archive.droppingArrived` keys its refusals on the source, and
`Archive.arrivedOf` stopped being a positional `List.zip` of targets against
results — it reads the entry off each result that came back `Ok`, which is what
the task's second half asked for. `Archive.carryingOver` could no longer match
by name, because the two halves of a copy between archives speak different
sources: the entry taken out is `ItemInArchive`, the file added is `ItemOnDisk`
under the scratch directory. It is handed the map from one to the other, built
from the same `landedAs` that named the scratch file, so the join is explicit
instead of coincidental.

`Operations.itemResult`, `allWatched` and `removeAll` lost their `IFileSystem`
parameter along the way: a source is the path itself now, and nothing has to ask
the file system what the path is called.

**P15.2 put one rule where six copies stood, and two of them had disagreed
about case.** "Does this path lie under that one" was answered in
`Archive.within`, `Drives.holding`, `SearchListing.relativeTo`, the boundaries
of `FileSearch.run`, `FindGround.sameRoot` and `Links.volumeOf` — five of them
case-insensitively on every platform, one of them platform-aware, and
`FileSearch`'s record of the directories it had already entered case-sensitively
on every platform. So on Linux packing `/home/u/Docs` into `/home/u/docs/x.zip`
was refused as packing an archive into itself, while on Windows a link whose
target was spelled in another case was walked into twice.

The rule is now `FileSystemPath.strictlyUnder`, `atOrUnder` and `sameEntry`,
with `pathAfter` spelling what lies below a root. Each takes a `Platform`:
Windows folds case and reads both separators, Unix minds case and reads `/`
alone, and a trailing separator never changes the answer — which is what lets a
volume root hold everything below it without a special case. The strictness is
in the name, so no reader has to open the body to learn whether the root counts
as under itself.

Where the platform comes from was the one decision worth making deliberately.
Threading it down from `HostEnvironment` would have reached `SearchListing` only
through `AppState` and `Update`, and `Archive.unpackEntry` only through the whole
`extract` family — some fifty-five call sites, and a second parameter beside
`fs` in exactly the function P15.5 is about to simplify. Instead
`FileSystemPath.platformOf` reads the platform off the `IFileSystem` the module
already holds, which is the same abstraction those modules already ask for their
separators. No signature moved. `Links` keeps the `Platform` it is given,
because it needs one anyway to know which link kinds exist, and its private
`comparison` went with `volumeOf`'s hand-rolled prefix test.

The suite covers the rule with `[<Theory>]` rows for both platforms over literal
Windows and Unix paths, so both halves are checked whichever system the run is
on; the end-to-end consequence — a differently-cased sibling refused or allowed
— is a `UnixFact` and a `WindowsFact` beside each other.

**P15.3 made a move a move again.** `Operations.moveTo` handled a directory as
`copyTo` followed by `delete`, whatever the two paths were, so renaming a 50 GB
directory in place read and wrote 50 GB and `F6` between two directories of one
drive did the same — where `IDirectory.Move` is a metadata operation. The copy
was not atomic either: a failure in the middle left both trees standing.

It now asks `IDirectory.Move` first. The answer is a three-case
`DirectoryMove`: `Renamed` is done, `RenameFailed` carries the error through
`mapException` as any other operation does, and only `RenameRefused` — an
`IOException`, the shape .NET raises for a move across volumes — falls back to
copy-then-delete. That `IOException` is also what a target directory that
already stands raises, which is deliberate: a move onto an existing directory
keeps merging into it the way it always has, because the fallback is exactly
the old code. An `UnauthorizedAccessException` is no longer answered by copying
what cannot be removed afterwards; it is reported as the move's own failure.

Cancellation moved with it. A same-volume rename cannot be stopped halfway, so
`moveTo` reads the flag before it starts and reports the directory once instead
of reporting every file under it. The test that pinned "cancelled halfway keeps
the source where it is" now says "cancelled before it starts", and a second one
beside it takes the fallback path — over a `MockFileSystem` whose
`Directory.Move` refuses — and pins the halfway rule where halfway still exists.

The measurement the task asked for is a counting fake: `Fakes.CopyCounting`
overrides `MockFile.Copy` and shows no copies at all for a same-volume move, and
a control test beside it copies a two-file tree and sees exactly two, so the
zero is a reading and not an oversight.

**P15.4 made four quiet failures speak.** Each was a `with _ ->` that answered
something plausible instead of saying what went wrong.

`Settings.save` swallowed every exception, so a session that could not be
written looked exactly like one that was. It answers
`Result<unit, SettingsError>` now — a new error family beside `ContentError` and
`ArchiveError`, with `SettingsError.mapException` as its one place for turning an
exception into a value — and `Shell` reports it through `SettingsErrorMessage`.
`QuitRequested` raises `SaveSettings` before `Quit` and effects are interpreted
in order, so the dialog is seen before the application goes; `SessionSaveRequested`
takes the same path.

`BatchRunner.runMeasuring` turned any exception into an empty measurement, which
is also what an empty directory answers. `BatchRunner.measured` now wraps the
work the way `Operations.guarded` wraps a batch and answers
`Result<Measurement list, OperationError>` with `BatchFailed`; `MeasureFinished`
carries that result and `Update` turns an `Error` into `ReportOperationError`.
Pulling the guard out of the runner is also what made it testable without a
Terminal.Gui application running.

`SearchListing.heldIn` read an unreadable archive as "no index" and kept every
hit inside it as if it had been verified. It answers a `Result` now, `surviving`
traverses instead of filtering, and the listing fails as `ArchiveUnreadable`, so
the panel says the archive cannot be read rather than showing hits nothing
checked. Seven results-panel tests had leant on the old rule by standing on
`MockFileData "zip"`; they build a real zip and a real tar now, which is the
honest fixture anyway.

`BatchRunner.drive` leaked a `CancellationTokenSource` and a `ProgressDialog` per
batch and `SearchRunner.run` leaked a token source per search. Both are `use`
bindings now. The order matters and is kept: `cancellation.Cancel()` runs before
either is disposed, so the work still on the batch thread sees a cancelled token
and never touches a disposed dialog.

`Archive.needsPassword` is the fifth site A9 names and is left alone on purpose:
P15.13 answers it from the cached index, and narrowing its net before that would
be written twice.

**P15.5 deleted the forty aliases, and the arms say what they call.** Forty
`let x' = …` bindings stood between `transition`'s signature and its `match`,
and they were not one thing: `applyListing'`, `applyOperation'` and
`applyBatch'` bound `state`, `changing'` and `chosen'` bound both `fs` and
`state`, and the other thirty-five bound `fs` alone. So a reader meeting
`makeDirectory'` had to climb back to the block to learn which arguments were
already there, and the block itself allocated forty closures for a `CursorMoved`
that used one of them (Q1, perf P10).

Every arm now names the function and passes the arguments — `makeDirectory fs
state.Active directory name state |> applyOperation state` — and `sentToRoot`
lost the two the convention had leaked into it, `loadPanel'` and `getRoot`,
which were a closure and an alias standing over one call site each. Three arms
grew past the line limit and took Fantomas' break onto the next line; nothing
else about the file changed, and the suite passed unchanged, which is the whole
proof this task can offer.

What the review also suggested — a local `let` beside any arm calling one helper
more than three times — turned out to have no site: after the block went, no arm
repeats a call often enough to earn a name back.

**P15.6 turned a gate into a rule, and the rule now reads in one place.**
`guarding state source receives proceed` took a `ResultsRefusal option`, a bare
`bool` and a continuation, and eight arms — not the seven the task counted —
passed it their whole body, each paying an indentation level for it (Q7,
TD-021). Nothing in it depended on anything `transition` computes: the refusal
follows from the message and from whether each of the two panels shows results.

So it is decided before `transition` runs. `refusedAsSource` answers what a
message is refused for while the active panel shows results — `MakingDirectory`,
`MakingFile`, `Linking`, `Packing`, `Unpacking` — and `refusedAsTarget` answers
`Receiving` for the messages that need a directory on the other side: copy,
move, pack, unpack. `resultsRefusal` matches the two answers against the two
panels, and `update` either reports the refusal or runs `transition`. The `bool`
went with the continuation: a rule that answers *which* refusal has no use for a
flag that only says *whether*.

The review sketched a `Gate` record with a `Source: ResultsRefusal option` and a
`TargetGate` union. Two functions over the message say the same thing with no
record and no second union — the target side has exactly one refusal, so
`Some Receiving` is the whole of it — and they let `DeleteRequested` drop out
altogether. Its gate had been `None`, `false`: a row that refused nothing.
Deleting from a results panel and deleting while facing one are both allowed,
and the honest way to write that is to leave delete out of both tables, which
the tests of P14.12 already pin from the other side.

`Renaming` stays where it was, inside the `TargetEntered` arm, because it is the
one refusal that does not follow from the message: it depends on whether the
text the user typed is a bare name.

Two corners of the rule had no test, and both are where a careless rewrite
would have broken it. When the active panel shows results and the message has no
source refusal, the target check must still run — `F5` between two results panels
refuses as `Receiving`, not at all. And when both refusals apply at once —
`Shift+F1` between two results panels — the source refusal wins, so the message
names `Packing` rather than `Receiving`. Both were written against the old
`guarding`, passed there, and pin the order the new rule keeps.

**P15.7 deleted ten effects that were special cases of three.** `CopyTargets`,
`MoveTargets`, `DeleteTargets`, `ExtractEntries`, `AddEntries`, `MoveEntriesIn`,
`MoveEntriesOut`, `RemoveEntries`, `CopyEntriesAcross` and `MoveEntriesAcross`
each named one pairing of a source kind with a target kind, and `Shell` already
had all of those pairings once, in `copyPiece`, `movePiece` and `deletePiece`.
The single-place arms of `interpret` were the second implementation — a hundred
lines of it — and `CopyRequested` and `MoveRequested` needed seven arms apiece to
choose between them (A2).

`Update` builds a one-group list where it built a special case. `places` turns a
`TransferSource` into the groups the effect carries, which for everything but
`FromPlaces` is a list of one. `CopyRequested` is now a single arm;
`DeleteConfirmed` is a single arm; `TargetEntered` lost the split between a
rename and a move. `MoveRequested` keeps three, because moving *out* of an
archive has never asked for a target the way a move to a directory does, and
that is a behaviour the phase keeps rather than a shape the task asked to flatten.

The archive gate became a rule instead of a list per arm. `sourceArchives`
answers the archives a group takes from and `targetArchives` the one the
transfer writes into; a copy gates the target, a move gates both. Written that
way it covers `FromPlaces` too, which it had not before — a move out of an
unchangeable archive among several marks used to reach `Shell` and come back as
a failed item, and is now refused by name up front like every other move.

In `Shell` the ten arms, `startBatch`, `crossing` and `changingArchive` are
gone. `changingArchive` was the drift A2 pointed at: the single-place path
reported an unchangeable or password-protected archive through an error dialog
and never started, while the places path let it come back as a failed item.
There is one behaviour now, the places one, and two tests changed their names to
say so. It is also the honest one for a batch of several groups, where one
refusing archive must not silence the groups that would have worked.

Two consequences are worth naming because neither is in A2. The trash and a
permanent delete used to run synchronously on the UI thread — `DeleteTargets`
dispatched straight from `interpret` — and now go through `RunBatch` like every
other place, so they run off the UI thread behind the progress window. That is
where P15.15 wants them; what P15.15 still owes them is a cancel flag they
actually read.

The other was a defect the unification exposed. `runPlaces` asked
`control.IsCancelled()` *before* each piece and answered `[]` for the ones it
skipped, which is right for the pieces after a stop and wrong for the first:
a batch cancelled before it started reported nothing at all, where the
single-place path reported the item it stopped at. `untilStopped` runs a piece
and asks afterwards, so each piece reports its own cancelled items — which is
what `Operations.allWatched` was written to do — and the groups after a stop are
still skipped. The wipe test that caught it is unchanged; it had been asserting
the right thing all along, against the arm that no longer exists.

**P15.8 gave `Runtime` a queue, and a nested dispatch stopped cutting the
line.** `Dispatch` updated, painted and then interpreted each effect in turn —
and `Shell.interpret` dispatches from inside a dozen of them. So a nested
`Dispatch` ran to completion, its own effects included, in the middle of the
outer effect list, and the `WatchLocations` that `Update.watching` appends to
the outer result was interpreted *after* the nested one, re-arming the watch on
the directories the panels had already left (A3).

`Dispatch` now enqueues. If no drain is running it starts one, and the loop
takes messages off the queue, running update, render and interpret until the
queue is empty. A dispatch raised from inside an effect therefore waits until
the message that raised it has finished with all of its effects. The drain stays
synchronous — `Dispatch` returns with the queue empty — which is what
`MainWindow.create` relies on when it reads `Runtime.State` straight after
seeding both panels, and what every test that dispatches and then asserts on
the state relies on too. A `try/finally` lowers the draining flag even if an
interpreter throws, so one failed effect cannot wedge the runtime.

The review called this "a trap for the next feature" rather than a live defect,
because no transition today both moves a panel and raises an effect that
dispatches synchronously. The trap is now two tests. The generic one belongs to
`Runtime` and is written in its own toy vocabulary: a message whose effects are
`[FollowUp; Marked 100]`, where interpreting `FollowUp` dispatches a message
whose effect is `Marked 101`, and the interpreter sees `FollowUp`, `Marked 100`,
`Marked 101` — where before it saw the 101 first. The second is the real thing:
a tab switch raising `[SaveSettings; WatchLocations …]`, an interpreter that
opens another directory when it sees the save, and the assertion that the outer
`WatchLocations` carries the directories of the tab switched to and the *last*
one carries the directory opened after it.

The change also reaches the batch path, which nobody had called a problem.
`deps.RunBatch` dispatches its `…Finished` message from inside the progress
dialog's own event loop, which is inside the effect being interpreted; before
the queue that recursion went several dispatches deep, exactly as the review
noted about `RunBatch` in tests. It is one queued message now.

**P15.9 made a keystroke pay for what it changed.** `Runtime` paints after
every message, and `FilePanel.render` rebuilt everything it could see: a
formatted string per entry, a classified colour per entry, a fresh
`ObservableCollection`, a `SetMark` per entry — for both panels, on every arrow
key. The listing the review measured at 50,000 entries cost a quarter of a
million string allocations before the cursor moved, and the panel nobody had
touched paid the same (perf P1, A14).

Those three passes depend on the entries, the marks and the width the rows are
formatted to. `PanelRows.Painting` is that triple and `PanelRows.rows` answers
`Kept` or `Rebuilt` by comparing it with what the panel last painted, which is
why `PanelState.Entries` became an array: the comparison is by reference, and
an array is what every other access to it already wanted — `cursorEntry`,
`withCursorWithin`, `namesOf` were all reaching into an F# list by index.
`Selected` is a set and rides along unchanged, so `{ p with Cursor = … }` keeps
both references and a cursor move is `Kept`; a load, a mark, a sort or a resize
is not. On `Kept` the panel moves `ListView.SelectedItem` and writes its footer,
and nothing else.

`PanelRows` is pure, so the decision has ordinary unit tests — the one that
matters says a second array of the same entries is `Rebuilt`, which is what
stops the comparison from quietly becoming structural. The counting renderer
the task asked for turned out to be already on the screen: a rebuild replaces
`ListView.Source`, so its identity across a dispatch *is* the count, and
`Integration/Panels/PanelPaintingTests` asserts it for a cursor move, for the
untouched panel while the other one opens a directory, and the other way round
for a mark and for a directory opened. Both fail if the memo is switched off.

`PanelState.Entries` reached further than the panel. `DirectoryListing.order`
now takes any sequence and answers the array, which let the three
`listForDisplay` functions keep their bodies and their `parent :: listed`, and
moved the change to the tests' own name projections. Nothing in `Marbas` needed
a conversion.

The sort stopped recomputing its keys (perf P12). The comparator called
`EntryName.extension` — a `Substring` — and a culture-aware compare on every
one of the *n* log *n* comparisons; `order` now picks the projection its key
needs, decorates each entry with `(group rank, key, name)` once and sorts on
that. `Seq.sortWith` keeps the sort stable, which is not academic in a results
panel, where two hits can carry the same display name and should stay in the
order the walk found them.

The held-modifier poll was the last of it (perf P14). A change fired every
100 ms went through `Runtime.Refresh` — the whole screen, for two rows of
labels — and `MainWindow` now hands `FunctionKeyBar.show` straight to
`WatchModifiers`. That one has a test of its own, and it is the reason
`PanelView` carries its last painting as a public member rather than a private
field: "the panels were not painted again" is only observable from outside as
the painting the panel still holds.

What the task chose not to do: `PanelFooter.describe` still runs on every
render, four passes over the listing, although it can only change when the
entries, the marks or the search do. The plan asks for the footer on a
cursor-only change and it is cheap next to what was removed; folding it into
the memo is a line of work for whoever needs it.

**P15.10 made a tick cost the hits it brought, not the hits so far.** Every
100 ms the search runner drained its queue into `ResultsWindow.add`, and `add`
rebuilt the window from scratch: regroup every hit into a dictionary, format
every row again, copy the whole source out to read the one row under the cursor,
and hand the `ListView` a new `ObservableCollection`. A search finding H hits
over T ticks did O(H·T) formatting on the UI thread, which is why a whole-disk
search stopped responding as it went on (perf P2).

A tick now formats only what it brought and appends it. Terminal.Gui's
`ListView` follows an `ObservableCollection` it was already given — that was
worth checking before designing around it, and it does, including leaving the
cursor where it stood. So the window keeps the collection, adds the new rows to
it, and the cursor needs no rescue: nothing above it moved. The whole list is
rebuilt for one event only, a change of width, and there the cursor's index is
kept as it is, because the rows are the same rows in the same order. The
`ToString()` search that used to find the row again is gone with the rebuild
that needed it.

`ResultRow.rows` became `ResultRow.following`, which takes the run already open
and opens a new one only where the location changes. **This is a visible
change, and it is the one thing in this task that is not purely internal.** The
old grouping was a dictionary: a hit for a directory already seen joined that
directory's group wherever it stood. Appending cannot do that without inserting
into the middle of the list, and inserting is what makes it quadratic again — so
a directory the walk comes back to now heads each run it comes back with. A
depth-first walk comes back often: it reports a directory's own entries
interleaved with descending into its subdirectories, so a directory with hits on
either side of a subdirectory is split. Rendering `*.md` over `_documentation`
shows it — 48 hits under 8 headers where 5 directories hold them, `_documentation`
heading four runs. Both the review (P2) and the plan asked for exactly this, and
the list still reads correctly, directory by directory, in walk order. What
would remove the split for good is `FileSearch` reporting a directory's own hits
before descending; that is a change to the walk, and the walks are P15.14's.

The counts stopped being computed twice. `FileSearch.run` already tracks files,
directories and skipped entries, and `report` now carries a `FindReport` — the
hit and the tally as it stood — so the window keeps the last tally instead of
scanning all its hits twice per tick. One consequence is worth naming: the
walking line used to show `0 skipped` until the walk ended, because the window
could only count what it could see. It now shows them as they happen, which is
what the finished line always said.

`walking` writes the summary label and nothing else, which is the whole of perf
P2's third part.

The tests that pin it: `the ticks after a hit do not format it again` feeds ten
ticks and asserts the row from the first tick is the *same string instance*
afterwards — that is "formatted once", observable without a counter; `a tick adds
to the rows already shown rather than replacing them` and `a step of the walk
leaves the rows alone` assert the source's identity. All three fail if `add` or
`walking` goes back to rebuilding. A real search was rendered to a screen to
check the result: headers flush left, hits indented with size and date, and
`Finished — 48 files, 0 directories` on the summary line.

**P15.11 turned reading a name back into a lookup.** A results panel names each
entry by its path — below the single root the search started from, or whole when
there were several — and every operation has to read that name back into the hit
it stands for. `SearchListing.hitFor` did it by spelling every hit's name again
until one matched, and spelling a name is a `FindHit.spelled` and a
`Path.Combine`. Since `AppState.hitAt` is asked once per marked entry, marking m
entries in a panel of H hits cost m × H of them, and `operationPaths`,
`keepUnfinished`, `transferSource` and `deleteScope` each pay it at least once —
`deleteScope` twice (perf P3).

`FoundEntries` now carries `Shown`, a `Map` from the display name to the hit,
and `hitFor` is `Map.tryFind`. The review offered two ways round this and the
plan wrote down the other one — the display name stored on `FindHit` — but that
one puts a presentation concern into the walk, since `FileSearch` is where hits
are made, and it still leaves `hitFor` scanning. The map is the review's own
first suggestion and it is the one that makes an operation linear in the marks
rather than in the hits. Measured over the real code, 20,000 marks read back
against 20,000 hits: ten seconds before, nine milliseconds after.

`displayName` moved out of `SearchListing` and into the `FindHit` module in
`PanelLocation.fs`, next to the `FindHit.spelled` it is built on, and it takes
the `FindGround` rather than the whole `FoundEntries` — which is what lets the
index be built while the record it belongs to is being constructed.
`FoundEntries.shownBy` is that one builder, so the map is never assembled by
hand: `AppState.showResults` is the only place in the application that makes a
`FoundEntries`, and nothing afterwards changes its hits, so the index cannot go
stale. The display names are paths, so they are unique and the map loses no hit.

`hitFor` and `hitAt` lost their `IFileSystem`: a lookup asks the disk nothing.

The task's own "done when" was that the results-panel tests of P14.12 and P14.13
pass unchanged, and they do — they never built a `FoundEntries` by hand, they go
through `showResults` like the application. What is pinned on top of them is the
index itself, in `PanelLocationTests`: the name a hit is shown under below one
root, the whole path when there are several, and that `shownBy` keys every hit
by the name it is shown under.

**P15.12 stopped a results panel from re-checking itself because something
happened next door.** Every listing of a results panel verifies its hits — a
`File.Exists` per hit on disk, a full index read per archive — and
`ExternalChangeDetected` listed a panel through `readTabQuietly`, which read
*both* panels of the tab. So a file written by another program into the
directory the *other* panel shows cost a stat per hit, on the UI thread, for a
set of hits nothing had touched (perf P4).

`readTabQuietly` now re-reads a panel only where `PanelLocation.watched` answers
a path. That is not a special case bolted on for results panels: it is the same
rule that decides what is watched in the first place. A change is reported for a
directory or for an archive file under a panel, and nothing reports on a set of
hits, so a panel the watch cannot speak about has nothing to learn from the
message. A results panel is the one location that answers `None`, and it keeps
what it has.

What still verifies is what asks: the first showing, `Ctrl+R`, `Ctrl+H` and the
reload after an operation — the last of which is how deleted or moved hits leave
the listing, so it cannot be dropped. A copy or a move can never land in a
results panel, because `transferTarget` answers `None` for one, so `finish*`
never re-verifies a results panel that was merely the panel opposite.

Measured on a panel of 50,000 hits over a mock file system: verifying it took
265 ms, and an external change now leaves 17.7 ms, which is the directory panel
that actually changed. A real disk charges more for a stat than a mock does.

Three tests hold it. `a hit already gone when the results are shown is left out`
and the older `reading the panel again drops a hit that is gone` pin the two
moments that do check. `an external change leaves the hits of a results panel as
they were` pins that the moment in between does not. The third is the one the
task asked for: `Fakes.ExistenceWitness` counts what `File.Exists` was asked
about, and `an external change in the other panel asks nothing about the hits`
compares the count for a hit's path before and after the message.

One behaviour went quietly with it, and it was a wrong one. `readTabQuietly`
answered a failed listing by replanting the panel in the nearest directory that
is there — written for a panel whose directory had been deleted. A results panel
whose listing failed, which since P15.4 is any results panel holding a hit in a
damaged archive, was therefore thrown out to a directory by a change in the
panel beside it. It now stays where it is.

**P15.13 gave the archive reader a scope the compiler enforces, and an index
the panel does not read twice.** `ArchiveReader.items` answered a lazy sequence
whose `WriteTo` closed over an `IArchiveEntry` or an `IReader` that the `seq`
disposed as soon as it ended, and for a tar it was dead the moment the reader
stepped to the next entry. Every caller happened to use it in time; nothing in
the types said they had to, and `List.ofSeq` followed by a `WriteTo` compiled
and threw `ObjectDisposedException` (A5).

The record is now two. `ArchiveEntryInfo` — the segments, the size, the time
and an `ArchiveEntryKind` where two booleans used to be able to disagree — is
an ordinary record that a walk may collect and hand on. `ArchiveItem`, which
carries the `WriteTo` beside it, is a `[<Struct; IsByRefLike>]` record handed
to an `ArchiveStep` delegate by `ArchiveReader.withItems`, one entry at a time,
until the step answers `StopReading`. A ref struct cannot be put in a list,
stored in a field or captured by a closure that outlives the call, so keeping
an item past its scope is a build error rather than a run-time one; that is
what `an item handed to the walk may not outlive it` asserts, through
`Type.IsByRefLike`, since `typeof<ArchiveItem>` is itself an instantiation the
rules forbid. Where a caller needs the content further down — `FileSearch`
matching text inside an entry, `Archive` buffering a kept entry — it passes the
`WriteTo` itself, which is an ordinary function; where it needs a lambda over
the entry it binds `item.Info` first, because the compiler refuses the capture.

The second half is the index. `ArchiveListing.read` read the whole archive
through `List.ofSeq` and did it again for every level, so `Enter` inside a zip
of half a million entries parsed it each time; `SearchListing.heldIn`,
`DirectoryMeasure.scan` and `Archive.needsPassword` each read it once more for
their own purposes (perf P7). `ArchiveIndex.read` answers the entries of an
archive and remembers the last four it was asked about, keyed by path, length
and last write time, and all four of those callers now ask it. The cache hangs
off the `IFileSystem` it was read through, in a `ConditionalWeakTable`: the
application has one file system and every test has its own, so nothing leaks
between them, which `two file systems keep their own index of one name` pins.
A file written again under the same name is read again, because its stamp no
longer matches — `an archive written again under the same name is read again`.
The task's own "done when" is `an archive is read once for every level a panel
walks into`, over a new `Fakes.ReadCounting` that counts what `File.OpenRead`
was asked for.

`needsPassword` was the A9 silent failure P15.4 left for this task: it read a
damaged archive as "no password needed", so the damage surfaced later, from a
different path, under a different message. It answers a `Result` now, and both
its callers say what they want from a failure. `Archive.changeRefusal` reports
the read error as the refusal, so a broken zip explains itself instead of being
called unchangeable; `Shell.passwordFor` treats it as "do not ask", because
`extract` asks again a moment later and reports it there — one refusal, decided
in one place. `extract` still asks only when no password was given, which
matters for the kinds that encrypt their own headers: a 7z opened with the
right password must not be refused by an index read that had none.

`ArchiveNames.legacy` and `reading` became `lazy`, so the code-page provider is
registered and the OEM encoding resolved once per process instead of once per
archive open (perf P13).

Measured on a zip of 20,000 entries over a mock file system: reading the index
costs 80–100 ms, and every level change after it costs 4–8 ms, `needsPassword`
about 1 ms and `heldIn` about 0.2 ms — each of which used to be another 80–100
ms. What still opens the file per target is extraction, and it should: content
is only ever handed out inside a walk, and the streaming reader cannot go back.
