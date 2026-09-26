# Phase 15 — Answering the reviews

Goal: act on the three reviews of 2026-09-19 — [architecture](../reports/2026-09-19-architecture-review.md),
[code quality](../reports/2026-09-19-code-quality-review.md) and [performance](../reports/2026-09-19-performance-review.md) — in an order where
every task leaves the code easier to change for the tasks after it. Findings are
cited by their ids in those reports (`A…`, `Q…`, and `perf P…` for the
performance report, so they are not read as task numbers); the phase adds no
feature, so every task keeps the behaviour the suite already describes and adds
the tests that pin what it fixes. Stage 6 is the exception, and says so: it
holds the work an earlier answer in this phase opened rather than anything the
reviews asked for.

Three rules decide the order. Correctness comes first: the findings that can
lose or confuse a user's data are small and independent, and nothing else should
wait for them. The refactorings of `Update` and `Shell` come next, because
almost every later task passes through those two files, and a merged `Effect` or
a single gate turns a change made twice into a change made once. Performance
goes from the hottest path down — every keystroke, every search, then the large
inputs — and the I/O model follows once the archive reader has a scope that a
cache and a stream can lean on. Readability work comes last, so no arm is
polished that a structural task is about to delete.

## Stage 1 — Correctness

- **P15.1 An item result knows where it came from** — `Operations.ItemResult`
  identifies an item by its source (`FileSystemPath`, or the archive and the
  entry's `ArchivePath`) rather than by its bare `EntryName`, and
  `AppState.keepUnfinished`, `Archive.droppingArrived` and
  `Archive.carryingOver` key their maps on it (A6). A results panel already
  mixes directories, so two hits named alike must no longer trade outcomes.
  *Done when:* two marked hits named `x.txt` in different directories, one of
  which fails, leave exactly the failed one marked, and moving out of an archive
  drops only the entry that arrived.
- **P15.2 One rule for "this path lies under that one"** — a single
  `FileSystemPath.isUnder` that takes the platform's case rule replaces
  `Archive.within`, `Drives.holding`, `SearchListing.relativeTo`, the boundaries
  of `FileSearch.run`, `FindGround.sameRoot` and the comparison in
  `Links.volumeOf`, and `FileSearch.visited` compares with the same rule (A7,
  TD-020 in part). Whether the boundary is strict is in the name, not in the
  body (Q6 in part). *Done when:* `[<Theory>]` rows for both platforms cover
  equal, strictly-below and differently-cased paths, and packing a directory
  into a differently-cased sibling on Linux is no longer refused.
- **P15.3 Moving a directory is a move first** — `Operations.moveTo` tries
  `Directory.Move` and falls back to copy-then-delete only when the move is
  refused across volumes (perf P5, first half). Renaming a directory in place
  and `F6` on one volume become metadata operations and can no longer leave two
  trees behind. *Done when:* a fake that counts copies shows none for a
  same-volume move, and a fake that refuses the move shows the fallback.
- **P15.4 No silent failures** — `Settings.save` answers a `Result` that
  `QuitRequested` and `SessionSaveRequested` report; `BatchRunner.runMeasuring`
  and `SearchListing.heldIn` answer an error instead of an empty measurement or
  an unverified hit (A9, Q11); `BatchRunner.drive` and `SearchRunner.run`
  dispose what they create (A13). `Archive.needsPassword` waits for P15.13,
  which answers it from the index. *Done when:* each of the four failures is a
  test that sees a reported error.

## Stage 2 — `Update` and `Shell` made ready

- **P15.5 No primed aliases** — the forty `let x' = x fs` bindings at the top of
  `Update.transition` go, and each arm passes `fs` itself (Q1, perf P10);
  `sentToRoot` loses `loadPanel'` and `getRoot`. Mechanical, and first, so that
  every later diff in `Update` reads plainly. *Done when:* `Update.fs` has no
  primed name and the suite passes unchanged.
- **P15.6 The results gate is decided once** — the refusal that `guarding`
  computes today is a value derived from the message and the two panels before
  `transition` runs, and the seven arms lose their continuation and one level of
  indentation (Q7, TD-021). *Done when:* `guarding` is gone, the rule reads in
  one place, and every refusal from P14.12 still has its test.
- **P15.7 Three effects instead of thirteen** — `CopyPlaces`, `MovePlaces` and
  `DeletePlaces` stay and the ten single-place cases go; `Update` builds a
  one-group list where it built a special case, and `Shell.interpret` keeps only
  the `Piece` machinery (A2). The unchangeable-archive refusal becomes the one
  behaviour the places path has. *Done when:* `Effect` has no single-place
  transfer case, `CopyRequested` and `MoveRequested` have one arm per target
  kind, and an unchangeable archive is refused the same way from a directory
  panel and from a results panel.
- **P15.8 `Runtime` drains a queue** — `Dispatch` enqueues, and one loop runs
  update, render and interpret until the queue is empty, so an effect always
  sees the state it was raised from and a nested dispatch cannot overtake the
  outer one (A3). *Done when:* a test dispatching from inside an effect sees the
  outer effects run first and the `WatchLocations` of the latest state
  interpreted last.

## Stage 3 — The hot paths

- **P15.9 A keystroke renders what changed** — `PanelState.Entries` becomes an
  array, `FilePanel.render` remembers the last state per side and skips the
  rows, colours and marks when `Entries` and `Selected` are the same references,
  a cursor-only change moves `SelectedItem` and refreshes the footer, and a
  modifier change repaints the bottom bar alone (perf P1, A14, perf P14). The
  sort computes each key once (perf P12). The integration tests that read a
  listing by index are adjusted in the same task (Q14). *Done when:* a test over
  a counting renderer shows no row rebuilt for `CursorMoved` and none for the
  untouched panel, and the result is rendered and checked on screen.
- **P15.10 The results window appends** — a tick appends the new hits' rows to
  the existing collection, a group header only when the location changes; the
  counts arrive with the report from `FileSearch.run`; `walking` updates the
  summary line alone; the row under the cursor is kept by identity, not by its
  `ToString()` (perf P2, Q8 in part). *Done when:* a test feeding ten ticks
  formats each hit once, and a search is rendered and checked on screen.
- **P15.11 A hit is found by lookup** — the display name is stored on `FindHit`
  when the results are shown, and `SearchListing.hitFor` becomes a lookup, so
  the operation paths, `keepUnfinished`, `transferSource` and `deleteScope` are
  linear in the marks (perf P3). *Done when:* the results-panel tests of P14.12
  and P14.13 pass unchanged over the indexed lookup.
- **P15.12 The results are verified when asked** — a results panel checks its
  hits when it is first shown and on `Ctrl+R`, and not when the other panel's
  directory changes (perf P4). *Done when:* an external change in the opposite
  panel asks the file system nothing about the hits.

## Stage 4 — The I/O model

- **P15.13 An archive is read within a scope** — `ArchiveReader.withItems`
  replaces the lazily evaluated items: the metadata (`ArchiveEntryInfo`, with an
  `EntryKind` instead of two booleans) may be kept, the content is handed out
  only inside the scope (A5, Q8 in part), and the legacy encoding is resolved
  once per process (perf P13). The index of the archive under a panel is cached
  by path, length and last write time, and levels, `needsPassword` and `heldIn`
  are answered from it (perf P7 in part, A9 for `needsPassword`). *Done when:*
  keeping an item past its scope does not compile, and changing level inside an
  archive reads the file once.
- **P15.14 The walks enumerate once** — `FileSearch.run` is split so that the
  archive scan and the level bookkeeping take explicit inputs and answer
  explicit results (Q10); then one helper enumerates a directory's children as
  `IFileSystemInfo`, and `Operations.deleteTree`, `copyTo`, `wipe`,
  `DirectoryMeasure`, `FileSearch` and `Archive.add` walk over it (perf P6,
  TD-019, Q5 `below`). *Done when:* a counting fake shows one enumeration and no
  extra stat per entry for every walk.
- **P15.15 Nothing long runs on the UI thread** — permanent delete, trash and a
  directory rename that fell back to copying run through `RunBatch` with a
  progress window and `Esc` (perf P7, A8, perf P5 second half); `deleteScope`
  and the reads that scale with the data become effects answering with a
  `…Loaded` message (A1). The architecture documents then say which transitions
  may touch `IFileSystem` and which work is an effect, and `TESTING.md` stops
  contradicting itself (A12). *Done when:* each of those keys shows its progress
  window on a large tree, and `OVERVIEW.md` matches the code.
- **P15.16 Archive entries are streamed** — the viewer and the content search
  read an archive entry as a stream inside the scope of P15.13 instead of
  buffering it whole (perf P8). The rewrite of 7z and tar.gz keeps its
  documented buffering. *Done when:* viewing and searching an entry larger than
  the memory budget of a test stream succeed.
- **P15.17 Solid 7z extraction is measured** — `Shift+F2` on a 7z of a few
  thousand small entries is timed against the reference tool; only if the time
  grows quadratically does extraction move to the sequential reader path the tar
  kinds already use (perf P9). *Done when:* the measurement is recorded in
  [Measurements](../Progress/MEASUREMENTS.md), with the change or the reason none
  was needed.

## Stage 5 — Readability

- **P15.18 The ports are grouped and typed** — `Shell.Dependencies` splits into
  prompts, processes, watches and runners; each prompt takes a request record
  and answers a typed value, so two prompts can no longer be wired to each
  other; `FindChoice` moves next to `FindQuery`; `Prompts` runs every dialog
  through one helper (A4, Q5 `running`, Q8 prompt ports). *Done when:* no two
  prompts share a type and `Fakes.dependencies` overrides a field of a
  sub-record.
- **P15.19 Flat decisions** — file by file, three-deep `match` cascades become a
  match over a tuple or a computation expression, parenthesis chains become
  pipelines, and a chain of three binds or one nested match becomes `result {}`
  or `option {}` (Q2, Q3, Q4). The cut-off goes into
  [Development](../DEVELOPMENT.md). *Done when:* the sites listed in Q2-Q4 that
  survived the earlier tasks are rewritten and the rule is written down.
- **P15.20 Names that tell the boundary** — the remaining prepositional names
  and the `changing` / `opened` families say what they answer (Q6). *Done when:*
  no two helpers in one module differ only by a prime or a preposition.
- **P15.21 The remaining copies and flags** — one generic lookup of a dialog's
  sub-view by id, one `..` entry, one helper for a lazily replaced mock member
  in `Fakes` (Q5); `ArchiveCapabilities` as a set, `LinkDialog` keeping its
  kinds, `FindDialog` ids not built from `%O`, and the remaining booleans (Q8).
  *Done when:* each copy listed in Q5 has one definition.
- **P15.22 `PanelState` stands on its own** — the panel manipulation that never
  touches `fs` moves out of `AppState` into its own module with its own tests
  (A11). *Done when:* `AppState.fs` holds loaders, `finish*` transitions and the
  queries `Update` asks.
- **P15.23 The rest, as files are touched** — `Domain` free of `System.IO` types
  (A10), dense one-liners split into named predicates (Q9), partial access
  (Q12), the small style inconsistencies (Q13), the line index and the viewer on
  very large files (perf P11); the wipe's generator stays as documented (perf
  P15). `Platform` moves out of the root `Marbas` namespace into `Domain`, where
  a two-case union describing the host belongs — `Marbas/Platform.fs` becomes
  `Marbas/Domain/Platform.fs` and the `open Marbas` lines that only reached it go.
  The three theories that still take the expected answer as a `true`/`false`
  column — `Domain/FindQueryTests.fs`, `Domain/NamePatternTests.fs` and
  `Domain/SearchTextTests.fs` — split into an affirming and a denying theory
  each, the way §8 of the conventions now asks. *Done when:* every finding of
  the three reports is either done or written down with the reason it is not,
  no `[<Theory>]` in the suite carries a boolean expectation, and the three
  duplications the tech-debt notes describe — the repeated path helpers, the
  repeated drive and separator handling, and `guarding` — are gone.

## Stage 6 — What an answer opened

- **P15.24 A damaged archive loses its own hits, not the whole listing** —
  P15.4 made `SearchListing` answer an error when an archive's index cannot be
  read, which is right about the silence and wrong about the blast radius: one
  damaged archive now keeps the entire results panel from loading. The listing
  answers the hits it *could* verify, and says beside them which archives it
  could not read. `SearchListing.read` keeps its `Result` for a caller that
  wants the strict answer; `listForDisplay` grows a second half — the entries
  plus the archives that refused — and the hits inside a refusing archive are
  shown rather than dropped, marked as unverified so nobody reads them as
  checked. Where the mark lives is the design question: an `Entry` field that
  `EntryColor` paints in the warning colour reads best on screen, a count in the
  panel footer reads best in a narrow panel, and both may be wanted. Whatever
  is chosen is verified by rendering the screen, not by reading properties.
  *Done when:* a results panel holding one hit on disk, one in a readable
  archive and one in a damaged archive lists all three, the damaged one is
  visibly marked on a rendered screen, and no listing error is raised; the
  archive error still reaches the user once, not per hit.
- **P15.25 An unpack walks the archive once, not once per target** —
  P15.17 went looking for the quadratic perf P9 predicted in the solid 7z
  decompressor, did not find it there, and found it in `Archive.extract`
  instead: `unpackTarget` walks the whole archive through
  `ArchiveReader.withItems` once for *every* target, and `Shift+F2` asks for one
  target per top-level entry. A flat archive of *n* entries is therefore walked
  *n* times — 4 000 entries cost 5.4× what the same content costs under one
  directory, and the curve is the square — identically for zip and for 7z, so
  the container has nothing to do with it (the numbers are in
  [Measurements](../Progress/MEASUREMENTS.md#unpacking-a-solid-7z-p1517-2026-09-24)). One walk
  serves every target: each entry is matched against the targets it falls under
  and unpacked once, and the per-target `ItemResult`s are built at the end from
  what that walk saw, in the order the targets were given. Two behaviours have
  to survive the change, and they are the reason it is its own task rather than
  part of P15.17. A target that matched no entry still answers `SourceNotFound`
  under its own spelled name. And `Esc` still leaves the targets it never
  reached out of the results, so their marks survive — which now has to be
  decided from entries arriving in the archive's own order rather than from a
  loop over targets, and a target half unpacked when the walk stopped is a
  failure, not a success. *Done when:* a counting fake shows one archive read
  for an unpack of many targets, the timing above is linear over the same
  sizes, and the `SourceNotFound` and the surviving marks after `Esc` are the
  tests they are today.
- **P15.26 An unused value does not build** — P15.22 moved every panel
  function whose body never touched `fs`, and left three behind that declared
  an `fs` they only handed on; nothing flagged them, because the F# compiler
  says nothing about an unused parameter unless warning FS1182 is turned on.
  `Directory.Build.props` turns it on (`--warnon:1182`), and with
  `TreatWarningsAsErrors` already set, every unused parameter, binding and
  pattern variable becomes a build failure. What it finds is fixed where it
  is: a parameter the body does not use is dropped from the signature and from
  every caller, a value bound and never read is removed, and a name the code
  keeps on purpose — a handler signature Terminal.Gui dictates, a pattern that
  must bind — is spelled `_` or `_name`. Where dropping `fs` leaves a function
  that no longer needs its layer, it moves the way P15.22's did. *Done when:*
  `dotnet build Marbas.slnx` is clean with the warning on for both projects,
  the suite passes unchanged, and [Coding style](../coding-style-conventions.md)
  §6 names the warning next to `TreatWarningsAsErrors`.
