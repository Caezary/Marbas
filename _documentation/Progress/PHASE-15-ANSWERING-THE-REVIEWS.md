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
