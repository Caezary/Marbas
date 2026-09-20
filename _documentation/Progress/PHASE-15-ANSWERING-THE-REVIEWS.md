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
