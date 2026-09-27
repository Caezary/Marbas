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
passed it their whole body, each paying an indentation level for it (Q7).
Nothing in it depended on anything `transition` computes: the refusal
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

**P15.14 made every walk read a directory once and carry what it read.** Six
walks went over a tree — `Operations.deleteTree`, `copyTo` and `wipe`,
`DirectoryMeasure`, `FileSearch` and `Archive.add` — and every one of them
enumerated *paths* and then asked the file system again about each entry: two
`Exists` calls for the kind, a `FileInfo` for the link target, another for the
length. Three to four system calls per file, where the directory read had
already carried all of it, and on a network share each one is a round trip
(perf P6). `DirectoryListing.read` was the only walk doing it right, and it did
it with two enumerations of its own.

`DirectoryListing.children` is now the one enumeration: `EnumerateFileSystemInfos`
once, and a `Child` per entry — where it is and the `Entry` a listing would
show for it, built by `entryOf` from the `IFileSystemInfo` the enumeration
already handed over. `entryAt` is the same answer for a single path, for the
root of a walk where nothing has been enumerated yet, and it replaced
`FileSearch.entryOf` and the `kind`-then-`isLink` pairs at the head of `delete`,
`wipe`, `copyTo` and `Archive.add`. `read` builds on `entryOf` too, so what a
panel shows and what a walk decides on come from one function rather than two
that could drift (Q5). Each walk then matches on
`child.Entry.Kind, child.Entry.LinkTarget`, which is the same two questions it
used to ask the disk, asked of a value.

What pins it is `Fakes.WalkWitness`, a `MockFileSystem` that counts
enumerations and counts what each path was probed about — stopping while a mock
operation carries itself out, and while an enumeration builds the infos it
answers with, so what is left is what Marbas itself asked. Twelve tests, two
per walk: a tree of three directories is enumerated three times, and nothing is
asked about a file inside it. Measured over a mock tree of 20,000 files in 200
directories, measuring cost 700–800 ms with a stat per entry and 400–480 ms
from the one enumeration.

`Fakes.UnreadableDirectory` had to learn the new road: it denied
`MockDirectory.EnumerateFileSystemEntries`, which nothing walks through any
more, so a directory the user may not read was suddenly readable. It now denies
`MockDirectoryInfo.EnumerateFileSystemInfos` as well, and the four tests that
depend on a refusing directory pass unchanged.

The other half of the task was Q10, the 200-line `FileSearch.run`.
`ArchiveScan` is now a private module with the archive scan and the level
bookkeeping in it: `run` takes a `Scan` — the archive, its kind, the level and
how deep the walk goes — and one function called per entry, which reports the
hit and answers whether to keep reading. It answers `Levels` with the directory
levels it gathered, or `Locked`, `Cancelled` or `Unreadable`; the caller reads
that and decides what it means for `skipped` and `stopped`. The `while` loop
with its `locked` and `running` flags is gone, and so is the last boolean
carried as a field: how deep a walk goes is a `WalkDepth`, `ThisLevelOnly` or
`EveryLevel`, decided once by `depthOf`.

One behaviour went quietly with it. `consider` used to stat the child it had
just enumerated and count it skipped when the stat answered nothing — an entry
deleted between the enumeration and the stat. There is no stat to fail any
more, so such an entry is considered on what the enumeration said. That is the
same race every walk in the application already accepts, and one fewer syscall
to lose it in.

The third copy the task named went with them: `below`, "is this entry under
that level", written out by hand in `ArchiveListing`, `DirectoryMeasure` and
`FileSearch`, with `Archive.underPath` as the at-or-below variant.
`ArchivePath.strictlyUnder` and `atOrUnder` are the one rule now, named the way
`FileSystemPath`'s pair was named in P15.2, so whether the boundary is strict is
read off the call rather than out of the body.

**P15.15 put the last long walks behind the progress window and drew the
transition boundary by kind.** Three of them were left. A permanent delete and
a trash ran inside `RunBatch` since P15.7, but `Operations.removeAll` was a
plain `List.map` over the sources: it never named what it was working on, so
the window stood blank, and it never looked at the cancel flag, so `Esc` closed
a window that went on deleting behind it. `Operations.delete` had no
`BatchControl` at all, so a tree of half a million files was one uninterruptible
step. And a rename went through `Operations.rename` straight inside
`Shell.interpret`, on the UI thread, which since P15.3 matters only when
`IDirectory.Move` refuses — but that is exactly when a whole tree gets copied.

`delete` takes a control now and walks with it: `deleting` reports every entry
before it removes it and answers `Cancelled` at the one it stopped at, the same
shape `wipe` has had since P5.6, and errors name the child that failed rather
than the root of the walk. `removeAll` is gone; `deleteAll` and a new `trashAll`
are two lines each over `allWatched`, beside `copyAll`, `moveAll` and `wipeAll`,
so the five batches now differ only in the verb applied to a source. The one
removal that deliberately ignores the flag is the one that ends a move which
fell back to copying: `moveByCopying` runs it under a control whose
`IsCancelled` is off, because a stop between the copy and the removal would
leave the tree standing in both places.

The rename split in two. `Operations.startRename` checks the target, tries the
metadata rename and answers `RenameDone` or `CopyNeeded target`; `Shell`
dispatches `RenameFinished` there and then for the first, and hands
`moveByCopying` to `RunBatch` under `Renaming` for the second, turning the
one-item batch back into the `Result` the message carries with
`Operations.wholeOutcome`. So the ordinary rename keeps its instant answer and
no window flashes past, while the one that has to copy behaves like every other
batch.

`AppState.deleteScope` was the read A1 named that was neither on screen nor
bounded: it enumerated every marked directory while the transition built the
confirmation. It is two pieces now. `deleteQuestion` is pure — it answers a
`DeleteQuestion` holding the paths whose contents have to be looked at and
whether the targets already reach inside without looking, which is what a
directory entry inside an archive says by itself — and `deleteScope` takes that
question and asks the file system, short-circuiting when the answer is already
settled. `DeleteRequested` raises `InspectDelete`, the interpreter answers with
`DeleteScopeLoaded`, and that message is what `ConfirmDelete` is built from.

The rule the documents state is now the rule the code follows, which is A12 and
the honest half of A1: a transition may read and write through the
`IFileSystem` it was given and may reach no other port, and work that outlasts a
keystroke is an effect. `OVERVIEW.md` and `CLAUDE.md` say that instead of
"transitions are pure", `STATE-AND-EFFECTS.md` spells out both halves with
`InspectDelete` as the worked example, `FILE-OPERATIONS.md` records the delete
control, the two-step scope and the rename that opens a batch only when it must,
and `TESTING.md` no longer says on line 6 what it contradicts on line 15 —
`Integration/Disk/` is named as the one folder that reaches the disk.

**P15.16 turned an archive entry from a block of memory into a stream.**
`ArchiveItem` carried a `WriteTo: Stream -> unit`, and both readers of one
answered it with a `MemoryStream`: `FileWindow.contentOf` copied the whole entry
in to view it, so `F3` on a 4 GB entry inside a zip allocated 4 GB, and
`FileSearch.contentOf` did the same for *every* entry a text search visited,
although `ContentMatch` reads 64 KiB at a time and stops at the first match
(perf P8). The item now carries `OpenContent: unit -> Stream` — the reader's own
entry stream, opened inside the step and read a buffer at a time. Unpacking
copies it into the file it is writing, the search hands it straight to
`ContentMatch.inStream`, and the rewrite of a 7z or a tar.gz is the one caller
that still buffers, which was always its documented price.

The search needed one thing removed first. `ContentMatch.answered` read an
8 KiB probe to settle the encoding and then *wound the stream back* to just past
the byte order mark to scan it again — which a forward-only entry stream cannot
do. The probe is now handed to `scan` as the first buffer it works on, minus the
preamble, so nothing is read twice and nothing seeks. A file on disk gains the
saved read; an entry inside an archive gains the ability to be searched at all
without being copied first.

The viewer was the harder half, because a `Source` answers byte ranges in
whatever order the user scrolls in and has to outlive any one walk.
`ArchiveReader` grew two functions beside `withItems`: `entryInfo`, which walks
to one entry and answers its `ArchiveEntryInfo`, and `openContent`, which opens
the archive and answers a `Stream` over one entry that *owns* the archive
underneath it — an `OwningStream`, so closing the viewer closes the entry, the
archive and the file, in that order. `RewindingStream` then makes a forward-only
stream look like one that seeks: it holds the entry's length and a way to open
it from the start, skips forward through a 64 KiB scratch buffer for a read
ahead of where it stands, and disposes and re-opens for a read behind it. It
opens nothing until the first read, so an entry the viewer never reads is never
decompressed. `FileWindow.openEntry` keeps the signature it had; `Source`, the
`LineIndex` above it and the whole viewer are untouched.

What pins it is `Fakes.ByteCounting`, a `MockFileSystem` counting the bytes read
out of every file it opens and the streams closed again. Over a zip holding one
*stored* 4 MiB entry — stored, so the bytes on the file are the bytes of the
entry and compression cannot flatter the count — viewing the first sixteen bytes
reads 82 KB, and a content search whose text sits at the head of that entry
answers after the same 82 KB, where both used to read the lot. `RewindingStream`
has its own tests for the forward skip, the backward re-open and the count of
opens each costs. Rendered over a real zip on disk, `F3` shows the entry, `End`
reaches line 40, `F4` switches to hex and `Home` comes back to offset 0 with the
right bytes — the re-open the design rests on, driven through the real viewer.

**P15.17 measured the solid 7z and cleared it, and found the quadratic
somewhere else.** The performance review's P9 was the one finding it would not
commit to: 7z archives are solid, every entry shares one LZMA2 stream, and
reaching entry *k* through `ArchiveFactory.OpenArchive` could mean decompressing
everything before it again — which would make `Shift+F2` on a few thousand small
entries O(n²) in decompressed bytes. The review asked for a measurement before
any change, and the change it had in mind was moving extraction to the
sequential `IReader` path the tar kinds already take.

The measurement is the table in [Measurements](MEASUREMENTS.md#unpacking-a-solid-7z-p1517-2026-09-24).
It was taken over archives Marbas wrote itself, unpacked to a real disk through
the same `Archive.extractAll` the `ExtractTargets` effect calls, against `7z x`
as the reference. Two variables were crossed rather than one: the kind, zip or
7z, and the number of targets the unpack is asked for — every entry at the top
level, which is what `Shift+F2` produces, or every entry under one directory,
which is one target.

**The suspicion is wrong.** With one target, 16 000 entries cost 15.2× what
1 000 cost: linear, and a solid 7z costs the same as a zip, which is not solid
at all. SharpCompress keeps the solid stream open across sequential
`OpenEntryStream` calls within one walk, so nothing is decompressed twice.
Extraction stays on `ArchiveFactory`, and the `IReader` rewrite is not written.
What the measurement does show is a constant factor of about 3.4× against
`7z x` — the per-file `CreateDirectory` and `LastWriteTimeUtc` that every
unpacked entry pays through `IFileSystem`, not an algorithm.

**`Shift+F2` is quadratic all the same, and the container has nothing to do with
it.** `Archive.extract` walks the whole archive once per target, and `Shift+F2`
asks for one target per top-level entry, so a flat archive of *n* entries is
walked *n* times: 4 000 entries cost 5.4× what the same content costs under one
directory, and the curve is the square. Zip shows exactly the same numbers as
7z, which is what rules solidity out. The review never saw it because it was
looking for it in the decompressor. The fix is one walk serving every target,
with the per-target `ItemResult`s built from what that walk saw; it is a
different change from the one P9 proposed, so P15.17 records it and P15.25,
added to stage 6, carries it out.

**P15.18 split the one port record into four and gave every prompt a type of
its own.** `Shell.Dependencies` was a flat record of 36 fields that mixed the
file system and three paths with eleven prompts, three process ports, two
watches and two runners (A4). Four prompts had the type
`string -> string -> string option` and three more were `string -> string -> …`,
so wiring `AskTarget` where `AskName` was meant compiled, and `AskFind` answered
a `FindDialog.FindChoice` — a port whose contract was owned by a view module.

The record now lives in `Ui/App/Ports.fs`, which holds nothing else: the file
system, the start, settings and workspace paths and `OnQuit` at the top, then
`Prompts`, `Processes`, `Watches` and `Runners`. The grouping is by what a port
does to the world rather than by who calls it — `Prompts` asks the person
something, `Processes` reaches the operating system, `Watches` subscribes to it,
`Runners` puts work on a background task — and `Shell.fs` is left with the
interpreter. A field inside a sub-record no longer repeats it: `WatchModifiers`
and `WatchLocations` are `Watches.Modifiers` and `Watches.Locations`, `RunBatch`,
`RunMeasure` and `RunSearch` are `Runners.Batch`, `Runners.Measure` and
`Runners.Search`.

**No two prompts share a type any more.** Each takes a request record named
after the question — `ErrorReport`, `Confirmation`, `OverwritePrompt`,
`PatternPrompt`, `NamePrompt`, `TargetPrompt`, `PasswordPrompt`, `PackPrompt`,
`FindPrompt`, `LinkPrompt`, `HandlerPrompt`, `ProgramsPrompt` — and answers a
domain value: `AskName` an `EntryName option`, `AskPattern` a `NamePattern
option`, `AskPassword` an `ArchivePassword option`. Records are nominal in F#,
so `AskTarget`, the one prompt that still answers a bare `string option`, and
`ReportError` and `Confirm`, the two that still take a title and a message,
cannot be swapped either. The requests that used to be assembled as text in
`Shell` now carry what the question is about:
`AskOverwrite` takes the `FileSystemPath` it would replace and `AskPassword` the
`EntryName` of the archive, and `OverwriteQuestion` and `PasswordQuestion` do
the phrasing in the adapter, where the `MessageBox` and the dialog are.

**The parse went to the dialog, not to the adapter.** A typed answer has to be
built somewhere, and putting `EntryName.tryCreate` in `Adapters/Prompts.fs` would
have moved the blank-name guard into the one folder no test reaches. It went one
step lower instead: `PromptDialog.name` answers an `EntryName option`,
`PromptDialog.password` an `ArchivePassword option`, `PatternDialog.pattern` a
`NamePattern option`, all in `Ui.Dialogs` where the integration tests already
lay dialogs out. Three tests moved with it — the trimmed name, the blank name
and the empty pattern are now asserted against the dialog that read them — and
`PasswordQuestion` gained the tests it never had, since its phrasing is no
longer visible from `Shell`.

**`FindChoice` moved to `Domain/FindQuery.fs`,** beside the query it carries, so
`FindDialog` reads a domain union rather than defining the port's answer.

**One helper runs every popup.** `Adapters/Prompts.fs` had eleven copies of
`use d = create (); app.Run d; read d` (Q5). It now has `running app create
read` once, and each prompt is the two functions it passes — the `Ui.Dialogs`
builder and the answer function beside it. The generic is constrained to
`IRunnable and IDisposable`, which is what `app.Run` and the `use` need; the
three message boxes are not dialogs of ours and still call `MessageBox`
directly.

**The tests name the sub-record.** F# 8's nested copy-and-update makes the
override as short as it was: `{ Fakes.dependencies fs start with
Prompts.AskName = fun _ -> Some name }`. Around 220 override sites across 34
test files changed shape; the suite is 2323 passing, 11 skipped.

**P15.19 flattened the decisions and wrote the cut-off down.** The quality
review's Q2, Q3 and Q4 were three views of one habit: a decision written as a
`match` inside a `match` inside a `match` (Q2), a value built by nesting three
constructors so it reads inside-out (Q3), and `Result`/`Option` chains written
as `bind` lambdas where a computation expression says the same in one line per
step (Q4). Six `result { }` blocks existed; the rest of the code had not caught
up with them.

**The cut-off is three binds, or one nested match.** It is in
[Flat decisions](../DEVELOPMENT.md#flat-decisions), with the sites it was first
applied to, and the style checklist points at it. Two binds still read fine as
a pipeline, so `finishDelete` and `moveOut` were left alone; the rule fires at
the third step or as soon as a `match` on a `Result` or an `Option` contains
another one.

**`Links.create` is what the rule looks like.** Three levels — the two path
kinds, the refusal crossed with the kind, then the junction the platform may
not have — became a `result { }` of three guards and one `return!`, with the
last level lifted into `make` as a flat match over `kind, native.MakeJunction`.
`Result.requireTrue` and `Result.requireFalse` from FsToolkit carry the two
path checks, and `refusing` turns the refusal option into a `Result`.
`Archive.copyAcross` went the same way: the walk into the scratch directory is
`carriedInto` as a `result { }`, the entry list it refuses with is
`refusedEntries`, and what is left is the `try/finally` around the scratch and
two arms. `Workspace.scratch`, `AppState.finishTransfer`, `finishPack`,
`finishLinks` and `makeFile` are each a `result { }` of two `let!`s and a
`return`, which also killed the 130-character `Result.bind` line `finishPack`
carried.

**A tuple match is not always the answer.** Two questions become one `match`
over the pair only when both are cheap to ask, because a tuple is evaluated
eagerly. `copyAcross` keeps its two levels for exactly that reason: asking
`Workspace.scratch` before `changeRefusal` has answered would create a
temporary directory nobody then releases. That caveat is in the rule.

**`Update` gained `reporting` and lost a level everywhere.** `request state
(ReportOperationError …)` appeared thirteen times, six of them wrapping two
more constructors. One `let private reporting state error` collapsed the pair,
and the six that nested now read
`ArchivePath.spelled fs archive entry |> Operations.ArchiveEntryNotRunnable |> reporting state`.
The `EditRequested` arm — a three-deep cascade written inline in `transition` —
is `editing` beside `opened`, `openedWith` and `runElevated`, with its
properties arm named `showingProperties`; `openedWith` and `runElevated` lost
their innermost `match` to `Option.map`/`Option.defaultValue`.

**Every prompt in `Shell.interpret` is `Option.iter` now.** Eleven arms were
`match ask () with | Some x -> dispatch … | None -> ()`; the ones with a single
`Some` arm are a pipeline into `Option.iter`, and only `PromptForFind` and
`PromptForRoot`, whose answers are unions of their own, keep the `match`.
The password cascade that `showHit` and the `ViewArchiveEntry` arm each spelled
out is one `withPassword` — a Q5 copy that fell out of the flattening.

**The password outcome lost a case instead of gaining a wrapper.** The first
attempt kept the three-case `NotNeeded | NotGiven | Given` and mapped it to
`ArchivePassword option option` so that `Option.iter` and `option { }` could
consume it — which is `Some None` at the call site, a nested option wearing a
function name. Nothing needed the distinction between "no password is needed"
and "this password was given": both mean *open it with this
`ArchivePassword option`*. So the union says that instead —
`OpenWith of password: ArchivePassword option | NotGiven` — and both consumers
are a two-arm match with no wrapping at all. `planFor` is a match over the two
cases with one `Option.map` over the approved targets, which is why `Shell`
needs no computation expression and no `FsToolkit` open.

The smaller sites went with them: `Operations.approve` names its `occupied`
predicate, `Trash.taken` asks one `holds` twice instead of spelling the
combine-then-kind chain twice, `Settings` has one `numberOf` that both
`numberIn` and the current-tab lookup use, and the two
`Error(Operations.SourceNotFound(ArchivePath.spelled …))` chains in `Archive`
are pipelines. `DirectoryMeasure.fs:18`, the one Q3 site that needed nothing,
had already gone with P15.14's single-pass walk. No behaviour changed and no
test changed: 2323 passing, 11 skipped, before and after.

**P15.20 named every boundary it found.** The quality review's Q6 listed the
archive family — `below`, `under`, `beneath`, `within`, `at`, `keyIn`, `into`,
`insideArchive` — and the verb families `changing` and `opened`, each a word
that says where something sits and not which rule decides it. P15.2 had already
put `atOrUnder` and `strictlyUnder` on `FileSystemPath` and `ArchivePath`, and
P15.6 had removed `guarding`; what was left went in one pass, and the pass was
extended to the rule the conventions state in general — no two helpers in one
module differ only by a prime or a preposition — by listing every `let` per
file with the prepositions and primes stripped and reading each collision.

**The review's own list.** In `Archive`, `keyIn` is `keyAtLevel`, `at` is
`sinkAtLevel`, `under` is `entryAtOrUnder` and `beneath` is
`segmentsFromTargetName` — not the `pathAfterTarget` the conventions table had
proposed, because the segments kept include the target's own name, and the
table now says so. `changeWith` is `withArchiveOpenForChange`, handing its work
an `OpenForChange` of `Held` and `Sink` instead of a `Changing` with a field
called `Into`; `rewriting` and `rewritingInto`, which differed by a preposition,
are `rewritingWhole` and `writingFreshCopy`. `PanelLocation.into` is
`PanelLocation.child`, `AppState.insideArchive` is `archivePlace` — and
`entryInArchive` now binds `placeOf` instead of spelling it again. In `Update`,
`changing` is the `refuseUnchangeable` the conventions already named, `opened`,
`opening` and `openedWith` are `openCursorEntry`, `openFile` and
`openWithHandler`, `refusing` and `refusingInArchive` are
`reportResultsRefusal` and `reportNotRunnable`, and `standingOn` is
`rereadAndSave`.

**A direction names both ends.** `Archive.moveIn`, `moveOut`, `moveAcross` and
`copyAcross` differed only by their prepositions; they are `moveFromDisk`,
`moveToDirectory`, `moveToArchive` and `copyToArchive`, in the `FromDisk` /
`ToDirectory` / `ToArchive` vocabulary the transfer types use. `Shell`'s five
`approved…` helpers say what is approved and for which target:
`entriesApprovedForDirectory`, `pathsApprovedForArchive`,
`entriesApprovedForArchive`, `pieceApprovedForDirectory` and
`pieceApprovedForArchive`, with `acrossPlan` as `archiveToArchivePlan` and the
pattern variable `into` as `targetArchive`. `Operations.approveInto` beside
`approve` is `approveLandings`, `copyTo` and `moveTo` beside `copy` and `move`
are `copyToPath` and `moveToPath`, and `inDirectory` is `landingInDirectory`.

**A name that lied now says what it does.** `toggleSelectionThrough` stopped
short of its target — the test that pins it is called "marking through an entry
stops short of it" — so it is `toggleSelectionShortOf`, over the private
`toggleRunShortOf` and `runShortOf`; `withCursorWithin` clamps, and is
`withCursorClamped`.

**Pairs across the layers.** `DirectoryListing.entryOf` / `entryAt` /
`childOf` are `entryFromInfo`, `existingEntry` and `childFromInfo`;
`FileSystemPath.linkTargetOf` beside `linkTarget` is `linkTargetOfInfo`;
`Links.refusalFor` beside `refusal` is `refusalForAbsolute`;
`SearchListing.heldIn` beside the memoising `held` is `indexedPrefixes`;
`LineIndex.scan` / `scanThrough` are `scanChunk` / `scanUntilKnown`;
`Settings.numberOf` / `numberIn` are `parsedNumber` / `tabNumberInKey`, and its
`panelOn` beside `panelFrom` is `defaultPanelAt`, its `association` beside
`associationFrom` is `validAssociation`; `ArchiveFormat.ofKind` beside `kind` is
`tryCreate`, the name §3 of the conventions gives a validating constructor;
`EntryProperties.flagsOf` beside `flags` is `raisedFlags`. In `AppState`,
`fromSettings` beside `settings` is `restore` with `restoredPanel` and
`restoredTab` under it, `locationAbove` / `locationOf` / `locationOfName` are
`parentLocation` / `enteredLocation` / `childLocation`, `navigateUp` /
`navigateInto` are `navigateToParent` / `navigateToChild`, `focusOn` is
`focusByNameOrTop` and `searchFor` beside `withoutSearch` is `focusingSearch`.
`KeyMap.onBar` / `offBar` / `commandOn` are `labelled` / `unlabelled` /
`labelledCommand`, `PanelWiring.overPage` beside `page` is `pageMessage` and its
locals `at` and `cursorAt` are `pageCursor` and `panelCursor`,
`Shell.passwordFor` beside `withPassword` is `passwordOutcome`, and the results
window's `opened` / `openedBy` are `lastLocation` / `lastLocationAfter`.

**The bare prepositions and the rest of the `opened` family.** `Zipper.at` is
`Zipper.refocus`, `ContentMatch.inStream` is `searchStream` beside `search`,
`FindHit.under` is `relativeOrFull`, `Links`' local `inside` is
`linkInDirectory` and `FileSearch`'s `fileInside` is `archiveFileHit`.
`DirectoryWatch.opened` / `opening` are `startedWatcher` / `rewatch`,
`RegistryHandlers.opened` is `subKey` and its `ofProgId` / `ofApplication` are
`commandOfProgId` / `commandOfApplication`, `FileWindow.opening` is
`entryStream`, and `RewindingStream`'s `opening` / `opened` are `openSource` /
`currentStream`.

**The primes went too.** `Operations` bound `let copyToTarget fs' source' =
copyFile fs' true source' target` in one walk and the same shape twice for
moving; two named helpers, `copyReplacing target` and `moveReplacing target`,
replaced all three, and the `fs'` in `anonymize` is a shadowing `fs`. The one
primed name in the suite, `SearchKeyTests.type'`, is `typeInto`.

**What was left standing, and why.** Two shapes collide under the check and are
not two helpers. A function and the value it answers bound under its noun —
`let probe = probeOf stream`, `let depth = depthOf query`,
`let arrived = arrivedOf taken` — names one thing twice. And `withX` is the
record-copy idiom: `withCursor` is the panel with its cursor replaced, and stands
beside `cursor` the way `{ p with Cursor = … }` does. `DriveRow.kind` and
`Drives.kindOf` share a file but not a module. Test helpers such as
`window` / `windowWith` were not renamed: they are builders whose second form
takes one more argument, the review did not reach them, and the rule's reason —
a preposition hiding a boundary — does not apply to a fixture. The rules and
their examples are in
[Names that tell the boundary](../DEVELOPMENT.md#names-that-tell-the-boundary),
and §2 of the conventions points there. No behaviour changed; the only test
edits are the renamed names. 2323 passing, 11 skipped, before and after.

**P15.21 left every copy the quality review counted with one definition.** Q5
listed nine helpers written more than once; five had already gone with earlier
tasks — the strictly-below test with P15.2, the child enumeration with P15.14,
the dialog runner with P15.18, the password-then-show pair with P15.19 — and
this task took the rest, together with the Q8 booleans and strings still
standing.

**One lookup for a sub-view.** Thirty accessors across thirteen `Ui` files
each spelled `dialog.SubViews |> Seq.pick (function :? T as v when v.Id = id
-> Some v | _ -> None)`. `Marbas.Ui.SubView` holds it once, as
`SubView.byId<T> id parent` and — for the seven accessors that find the one
view of its type, a panel's list or a viewer's content — `SubView.byType<T>
parent`; every accessor is now one line over it, and `Gui.panel` in the tests
uses the same. Its tests build a plain `View` with two children sharing an id
and show that the type tells them apart.

**One `..` entry.** `DirectoryListing`, `ArchiveListing` and `SearchListing`
each built the same eight-field record for the entry that leads up, differing
only in the date. `DirectoryListing.parentEntry modified` is the record, and
the three listings supply the date they have — the parent directory's
(`parentIfAny`, which answers nothing at a root), the archive file's
(`archiveParent`) and the directory the search stood in (`resultsParent`).

**Two copies in `Shell`.** `showPath` and `showEntry` opened a source and
showed it, or reported the error, word for word; `showOpened` is the one
`match`, and each of them is the open piped into it with its own error title
and message. The three approval helpers of P15.20 shared a walk — spell the
archive entries, approve the spelled paths, map the approved paths back — and
a landing check: `entriesApprovedBySpelling` is the walk, and
`entriesApprovedForArchive` turned out to be exactly that walk over
`pathsApprovedForArchive`, so its twenty lines became one.

**One helper for a replaced mock member.** Twelve fakes in `Fakes` swapped
seventeen `MockFileSystem` ports through a `let mutable x = null` and an
`if isNull x then x <- … ; x` override. `Fakes.once` does it, over a
`Replaced<'port>` that remembers what was made. The first version held the
replacement in an ordinary object and every fake of `Directory` threw a
`NullReferenceException`: `MockFileSystem`'s constructor reads `Directory`
before the subclass has any fields, which is the reason the originals checked
for `null` at all. The helper now answers a fresh replacement while its own
holder is still `null`, and [Testing](../Architecture/TESTING.md) says why.

**The capabilities are a set.** `ArchiveCapabilities` was a record of three
booleans; `ArchiveKind.capabilities` answers a `Set<ArchiveCapability>` of
`Readable`, `Creatable` and `Changeable`, and the three places that ask it read
`ArchiveKind.capabilities >> Set.contains Changeable`. The test of a kind that
can only be read asserts `Readable` in and `Changeable` out, with its act on a
line of its own.

**The last booleans of Q8.** `EntryProperties.setFlag flag wanted` is
`withFlag` and `withoutFlag`, and the properties dialog folds the check boxes
through a `marking` that picks one by the box's state; the one test that set
and cleared in the same assertion is two tests. `OpenWithChoice.Remember: bool`
is `Use: HandlerUse` — `AlwaysThisWay` or `ThisTimeOnly`, after the check box's
own label — and `Update.chosen` matches the extension and the use as a pair.
`ArchiveItem.IsDirectory` and `IsEncrypted` had already become an `EntryKind`
with P15.13, and `ResultsWindow.standingRow` had gone with P15.10.

**A dialog keeps what it was built from, and an id is not a rendering.**
`LinkDialog` recovered its kinds by looking each selector label up among all
three kinds, so a choice depended on how its label was spelled. The dialog is
now a `LinkView` carrying the `Kinds` it was given, the way `FindView` carries
its count, and `choice` reads the chosen kind off that list by position.
`FindDialog.boxId` was `$"find%O{box}"`; it is a match with one literal per
box, public beside a `boxes` list, and a test holds that no two boxes share an
id.

**Left for later.** Two booleans outside Q8's list remain —
`TextEncoding.HasBom` and `DeleteQuestion.ReachesContents` — and fall under the
general rule of §3 that P15.23 applies as files are touched. 2330 passing, 11
skipped; seven of the tests are new.

**P15.22 gave the panel a module of its own.** A11 found three roles in the 812
lines of `AppState.fs`: panel manipulation that never mentions `fs`, the
`IFileSystem`-backed loaders and `finish*` transitions, and the queries `Update`
asks before raising an effect. The first role is now `State/PanelState.fs` —
the `PanelState` record, `FocusTarget` beside it because `cursorFocus` answers
one, and a companion `PanelState` module of functions from one panel to the
next: the cursor and its focus, the incremental search, sorting and the
measured sizes a sort by size reads, marking a run short of its target,
patterns, the marks that survive a reload, and the pure questions —
`cursorEntry`, `selectedEntries`, `operationTargets`, `showsResults`,
`markedGround`, `directory`, `standingName`.

**It has tests of its own, and they need no file system.** `PanelStateTests`
builds one panel by hand — `..`, a directory and two files — and asks nineteen
questions of it: the cursor entry and the empty panel, a search that finds and
one that does not, sorting that keeps the cursor on its entry, a run that stops
short and never marks `..`, patterns both ways, the operation targets with and
without marks, the surviving marks, the search ground, the remembered name, a
cleared panel and a measured directory. Before this, every one of those rules
was reached only through an `AppState` and a `MockFileSystem`.

**`AppState` keeps a one-line face for each panel function.** `setCursor`,
`typeIntoSearch`, `sortBy`, `toggleSelection…`, `applyPattern` and
`applyMeasurements` are each `updatePanel side (PanelState.…)`: the message and
the two hundred test lines that address a panel by its side still read as one
call, and the rule behind them lives in one place. What is left in
`AppState.fs` — 640 lines — is the tabs, the loaders, the `finish*`
transitions, the queries and `enter`, which is what the task asked it to hold.
The queries that moved are called as `PanelState.cursorEntry` and so on from
`Update`, `AppView`, `PanelFooter` and the tests.

**Moving a type out of a module changes what an `open` shadows.** Defined
inside `AppState`, `FocusTarget.ByName` and the `PanelState` labels were the
last names in scope and won. From the namespace, they lost: `ByName` resolved
to `SortKey.ByName` from `Marbas.Domain`, and `{ Path = … }` to
`DirectoryListing.Child`. `AppState` therefore opens `Marbas.State` last, and
the twenty-three test files that build a panel or name a focus open it before
`Marbas.State.AppState` — found by removing the line everywhere and adding it
back where the compiler asked, so no file opens it for nothing. 2349 passing,
11 skipped; nineteen of the tests are new.

**Three queries stayed behind for an `fs` they never used.** The first pass
moved what did not *mention* `fs`; `entryInArchive` declared it and only handed
it on, and with it `cursorEntryInArchive`, `cursorFileInArchive` and the
delete question's `directoryEntriesInArchive`, so `Update` passed a file system
four times to functions that read none. They are `PanelState` queries now, the
last one named for what it answers, `targetsDirectoryInArchive`. The place of
a name inside an archive, which they and `transferSource` share, belongs to
neither module: `PanelLocation.archivePlace` answers it for a location and a
name, `FindHit.archivePlace` for a hit, beside `FindHit.spelled`. Twelve tests
pin them — four for the two places, eight for the panel queries, on a panel
standing inside an archive and one on disk. The compiler did not flag the
unused parameter because FS1182 is off; turning it on is P15.26. 2361 passing,
11 skipped.

**P15.23 took what was left, file by file, and closed the ledger.** Stage 5
had four named tasks and this one for everything the reviews listed as "worth
fixing when the file is next touched". It touched them.

**`Domain` speaks no `System.IO` (A10).** `EntryVisibility.classify` took a
`FileAttributes`, `Executability.classify` a `UnixFileMode`, and
`NamePattern.matches` called `FileSystemName.MatchesSimpleExpression`, so the
innermost layer knew the BCL's file model and `ArchiveListing` had to pass
`FileAttributes.None` and `UnixFileMode.None` for entries that have neither.
What the attributes and the mode bits say is now read in `FileSystem`, by the
new `EntryAttributes.visibility` and `executability`, and handed down as the
`Domain`'s own `EntryVisibility` and `Executability`; `classify` combines that
answer with what the name says, and `ofName` answers from the name alone, which
is what an archive entry and `ProgramCommand` use. `LaunchProcess` asks
`EntryAttributes.executability` where it asked `Executability.ofMode`.
`NamePattern` matches `*`, `?` and a `\` that escapes the next character on its
own — a token list and a greedy walk that backtracks to the last star — and its
theories gained rows for several stars, `??`, a double extension and both
escapes, the way `FileSystemName` answers them.

**`Platform` lives in `Domain`.** `Marbas/Platform.fs` is
`Marbas/Domain/Platform.fs`, in `namespace Marbas.Domain`, first in that folder.
Nine files opened the root `Marbas` namespace for it and nothing else; the ones
that already opened `Marbas.Domain` lost the line, the rest had it replaced.
Three of them were missed by a first search for `^open Marbas$` because their
lines end in CRLF, so the check that no bare `open Marbas` is left is a search
that allows the `\r`.

**No theory takes its answer as a column (§8).** `FindQueryTests`,
`NamePatternTests` and `SearchTextTests` each had one `[<Theory>]` whose last
column was `true` or `false`; each is now an affirming and a denying theory,
named for what it claims — "a mask matches the names it names" beside "a mask
passes over the names it does not name". The facts in those files and in
`HiddenEntriesTests` that folded the act into `Assert.Equal`, or asserted two
things at once, were split into one act on its own line each.

**Dense one-liners got a name per clause (Q9).** `FileSearch.consider` read
`depth = EveryLevel && follows entry && not (crosses …) && entered (…)`, the
last clause adding to the visited set; it is `stepsInto child && firstVisit …`,
with the side effect last and named for what it records. `archiveFileHit`
names `atSearchedDepth`, the archive switch is `looksIntoArchives`,
`PanelWiring.printable` is `unmodified && visibleCharacter`, `Drives` names
`holdsStorage`, `ContentMatch` names `runsIntoUnreadText`, `SearchListing`
binds the segments it looks up before piping into `Set.contains`, and
`Update`'s `TargetEntered` decides `renamesInPlace` once instead of in two
guards.

**The last two booleans of Q8.** `TextEncoding.HasBom` is
`Bom: ByteOrderMark` — `WithBom` or `WithoutBom` — and `preambleLength` a match
over the pair. `DeleteQuestion.ReachesContents` is `AtLeast: DeleteScope`: the
scope the targets reach without the file system being asked, which probing may
widen and never narrow, so `deleteScope` asks only when the answer is
`NamedEntriesOnly`. `PanelState.targetsDirectory` gives the archive case its
name, and three tests pin it.

**Partial access (Q12).** `DriveDialog.choice` and `TabDialog.choice` read
`SelectedItem.Value`; both answer `None` now when nothing is under the cursor,
and `DriveDialog.create []` no longer throws setting a cursor on an empty list —
three tests, all red first. `Links.sharedRefusal` matches its first refusal
instead of taking `List.head`, `ProgramCommand` falls back to `firstWord` rather
than `words[0]`, and `PathText.shortenings` binds the root in the pattern that
knows the list is long enough. Left standing, each beside what guarantees it:
`ArchiveFormat.initial = List.head all`, one line below the `all` built from a
fixed list whose first kind is creatable; `String.Split` indexed or taken last
in `Settings` and `Handler`, which always answers at least one piece;
`FindDialog.lastResults` and `PackDialog.chosen`, over buttons and options the
same file creates; `SubView.byId`, whose failing loudly on a missing view is
the contract §9 states. `Zipper.refocus` still clamps rather than answering
`None`, on purpose: its callers are a tab number read from a config file, which
is tolerated rather than refused, and the tab dialog's own row, and the test
"moving to a position clamps to the ends" says so.

**Style (Q13).** Two lines carried trailing whitespace and are clean;
`EditorProcess` had the one `|> fun … ->` left in the code and binds `editor`
instead. The other Q13 items had gone with earlier tasks — the double blank
line, `getRoot` with P15.5, the parenthesised lambdas in `Fakes` — and the
`module` / `namespace` rule is §6 of the conventions.

**The line index and the viewer (perf P11).** `LineIndex` kept an `int64` per
line; it now counts every line and keeps the start of every
`LinesPerCheckpoint`-th (64), and finds the starts in between by reading forward
from the checkpoint before them — once per block, which is then cached, sixteen
blocks at a time, and only when the block is whole. `Source.ReadInto` fills a
buffer the index owns, so neither the scan nor a rescan allocates a chunk. New
tests cover starts, texts and offsets on both sides of a checkpoint, in UTF-8
and UTF-16, and the count of checkpoints. `FileViewer` computes its rows once
per place — mode, wrap, top, left and size — so the header's percentage and the
draw that follows share them, and wrapping gathers each line's segments into a
list of lists rather than appending with `@` per line. The memo has no test of
its own: the second computation it saves never reached the file, because
`LineIndex` already caches the decoded lines.

**A defect the new tests found.** `Text` of the last line ran to the end of the
file, so a file ending in a line feed showed that feed as the `.` placeholder on
its last line. The index now knows where the content ends, and two tests pin it
in both encodings. The render probe showed it before and after on the same file:

```
before                 after
first                  first
second word word …     second word word …
d word … ENDS          d word … ENDS
third.                 third
```

**The drive and the separator, once each.** `Drives.volumeOf` answers
the deepest mount holding a path over every mount the system reports, and
`Drives.holding` the same over the usable drives, through one private
`deepestHolding`; `Links` no longer enumerates drives. `FindGround`'s own
separator list and trim went for `FileSystemPath.distinctEntries`, which drops
a `PATH` entry that is the same entry as an earlier one by the platform's rule
— and so folds case on Windows, which it did not (a Windows-only test, red
first). `ProgramCommand` takes the file name through `Handler.fileName` instead
of its own copy of the two separators. With `guarding` gone in P15.6 and the
path helpers in P15.14, the three duplications the task named are gone.

**Every finding, and where it went.**

| Finding | Answered by |
|---------|-------------|
| A1 I/O inside transitions | P15.15 — the documents say what a transition may do, and `deleteScope` became an effect. The archive index and the hit verification stay in transitions: P15.13 caches the index by path, length and write time, and P15.12 verifies only when asked, so neither scales with a keystroke. |
| A2 ten special-case effects | P15.7 |
| A3 re-entrant dispatch | P15.8 |
| A4 the port record | P15.18 |
| A5 `WriteTo` outliving its scope | P15.13 |
| A6 `ItemResult` by bare name | P15.1 |
| A7 six "lies under" rules | P15.2 |
| A8 long work on the UI thread | P15.3, P15.15 |
| A9 silent failures | P15.4, P15.13 |
| A10 `Domain` on `System.IO` | P15.23 |
| A11 three roles in `AppState` | P15.22 |
| A12 documentation drift | P15.15 |
| A13 undisposed runners | P15.4 |
| A14 render coupled to every message | P15.9 |
| Q1 primed aliases | P15.5 |
| Q2–Q4 cascades, parentheses, CEs | P15.19 |
| Q5 copies | P15.2, P15.14, P15.18, P15.19, P15.21 |
| Q6 prepositional names | P15.20 |
| Q7 `guarding` | P15.6 |
| Q8 booleans and strings | P15.10, P15.13, P15.18, P15.21, P15.23 |
| Q9 dense one-liners | P15.23 |
| Q10 `FileSearch.run` | P15.14 |
| Q11 broad `with _ ->` | P15.4, P15.13; what one damaged archive does to a results panel is P15.24 |
| Q12 partial access | P15.23; what stands is listed above with its guarantee |
| Q13 small inconsistencies | P15.5, P15.18, P15.22, P15.23 |
| Q14 tests | P15.9, P15.21 |
| perf P1 full re-render | P15.9 |
| perf P2 results window | P15.10 |
| perf P3 quadratic hit lookup | P15.11 |
| perf P4 re-verification | P15.12, P15.13 |
| perf P5 a move that copies | P15.3, P15.15 |
| perf P6 stat calls per entry | P15.14 |
| perf P7 blocking work | P15.13, P15.15 |
| perf P8 buffered entries | P15.16; the 7z and tar.gz rewrite keeps its documented buffering |
| perf P9 solid 7z | P15.17 measured it linear; the quadratic it found in `Archive.extract` is P15.25 |
| perf P10 closures per message | P15.5 |
| perf P11 line index and viewer | P15.23 |
| perf P12 sort keys | P15.9 |
| perf P13 encoding per open | P15.13 |
| perf P14 modifier repaint | P15.9 |
| perf P15 wipe bound by the CSPRNG | not done, by design: why the one pass is cryptographic is now written in [File operations](../Architecture/FILE-OPERATIONS.md) |

2411 passing, 11 skipped, from 2361; the build is clean.

**P15.24 let a damaged archive lose its own hits' certainty, not the whole
listing.** Stage 6 opened with the one answer of stage 1 that was right about
the silence and wrong about the blast radius: since P15.4 a results panel with
one hit inside an archive whose index could not be read did not load at all.
`SearchListing` now decides each hit's survival once — standing, gone, or
unconfirmed with the `ArchiveError` that stopped it — and reads that list two
ways. `read` stays strict and answers the first refusal as an error;
`listForDisplay` answers a `ResultsListing` that cannot fail: the entries, the
unconfirmed ones among them carrying `Verification = Unverified`, and in
`Refused` each archive that could not be read, once however many hits it holds.
`EntryVerification` is a new two-case union in `Domain`, and every `Entry` a
directory or an archive lists is `Verified`.

Where the mark lives was the design question the plan left open, and the answer
is both places it named. `EntryColor.classify` answers `UnverifiedColor` —
`ClassicScheme.unverifiedEntry`, bright red on blue — before it asks about the
kind, so an unverified archive or link reads as unverified first; the footer
adds `2 unverified` after the counts, where a narrow panel still shows it. When
the user hears about it was the second question: once per listing they asked
for — the hits sent to the panel and `Ctrl+R` — through one
`ReportUnverifiedHits` message that names every refusing archive and says the
hits inside are listed unverified. The reloads nobody asked for, after an
operation, a hidden-entries toggle or an edit, drop the refusals, since the
colour and the footer already say it. `AppState.loadLocationWithRefusals` is
the load that keeps them; `loadLocationFocusing` is it with the refusals dropped,
and `showResults` and `refreshWithRefusals` hand them to `Update`.

The render probe drove the real window — `Alt+F7`, a runner answering four hits
into the panel — and read the cells back:

```
╔╡Find: *.txt in C:\...\tree\start [name ↑]╞═══════════════╗
║..                                  <DIR> 2026-09-27 01:44║   Black on Aqua (cursor)
║broken.zip\lost.txt                     2 2026-09-27 01:44║   BrightRed on Blue
║broken.zip\other.txt                    2 2026-09-27 01:44║   BrightRed on Blue
║data.zip\inner.txt                      2 2026-09-27 01:44║   BrightBlue on Blue
║readme.txt                              2 2026-09-27 01:44║   BrightBlue on Blue
║──────────────────────────────────────────────────────────║
║4 files, 0 folders, 2 unverified   8 bytes                ║
```

and one error reported, under `Archive`: the path, the reader's reason, and
"The hits inside are listed unverified." The same scene is a pair of
integration tests in `PanelPaintingTests`, which read the painted rows and count
the reports. The rough edge it answered is gone from [Rough edges](../ROUGH-EDGES.md).

2430 passing, 11 skipped, from 2411; the build is clean.

**P15.25 made one walk serve every target of an unpack.** P15.17 found the
square that perf P9 predicted, but not where the review put it: `unpackTarget`
opened a walk of the whole archive for each target, and `Shift+F2` asks for one
target per top-level entry. `Archive.unpackingEach` now opens one walk. Each
entry looks its own prefixes up in the targets grouped by their segments, is
unpacked under every target it falls under that has not failed, and advances
that target's `TargetProgress` — not reached, unpacking, whole or failed — in
one `UnpackWalk` record threaded through the walk. The record also counts the
targets not yet settled, and the walk stops when none is left, so taking one
file out of a large archive no longer reads past it. The `ItemResult`s are
built at the end, in the order the targets were given.

The two behaviours the plan named kept their tests. A target no entry answered
to is still `SourceNotFound` under its spelled name, now beside targets that
were found in the same walk. `Esc` still leaves out what it never reached, but
what counts as reached is decided by the archive's order now: with `docs` and
`notes.txt` asked for in that order and `notes.txt` stored first, a stop after
the first file leaves `notes.txt` unpacked and `docs` out, where the loop over
targets answered the opposite. A directory target the walk had entered when it
stopped is `Cancelled` under its landing place rather than a success, since the
rest of its tree may have been ahead; a file target whose one entry was written
is whole and answers `Ok`. `eachTarget` stays for `removeEntries`, which checks
targets against a list it already holds and reads nothing per target.

`Fakes.ReadCounting` pins the point of the task: an unpack of three targets
opens a zip or a `tar.gz` once. The timing is in
[Measurements](MEASUREMENTS.md#one-walk-for-every-target-p1525-2026-09-27):
at n=4000 a flat 7z unpacks in 10.5 s instead of 49.9 s, *n* targets cost what
one does, and every row doubles with the entries. Matching an entry against
every target would have brought the square back through the comparisons, which
is why the targets are looked up by prefix. The rough edge is gone from
[Rough edges](../ROUGH-EDGES.md), and [Archives](../Architecture/ARCHIVES.md)
describes the walk.

2438 passing, 11 skipped, from 2430; the build is clean.
