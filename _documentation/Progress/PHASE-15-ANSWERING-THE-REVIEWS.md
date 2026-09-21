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
