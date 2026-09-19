# Performance review

Date: 2026-09-19. Scope: every source file under `Marbas/`, read for
algorithmic cost, allocation per user action, I/O per entry, and work done on
the UI thread. Method: static analysis with line references. No profiler was
run; where a number appears it is a count derived from the code (calls per
entry, passes per keystroke), not a measured time. Each finding says what
input size makes it visible.

Severity: **High** = visible on inputs an ordinary user has (a directory of
tens of thousands of files, a search over a disk, a large archive);
**Medium** = visible on large inputs or under a specific key;
**Low** = constant-factor or memory hygiene.

## Verdict

At the sizes the test fixtures use nothing here is slow, which is why none of
it has surfaced. The hot paths scale badly in four places: every keystroke
re-formats both panels in full; the search window re-renders every hit it has
ever received ten times a second; operations on a search-results panel are
quadratic in the number of hits; and several walks pay three or four system
calls per entry where one enumeration would do. Two further items are
correctness-adjacent: renaming a directory copies it, and viewing or searching
an archive entry buffers the whole entry in memory.

## Findings

### P1. Every message re-renders both panels in full — High

`Runtime.Dispatch` (`Marbas/State/Runtime.fs:22-26`) calls `render` after
every message, including `CursorMoved`, which the panel raises for each arrow
key. `AppView.render` (`Marbas/Ui/App/AppView.fs:34-47`) then calls
`FilePanel.render` for both sides. Per panel, per keystroke
(`Marbas/Ui/Panels/FilePanel.fs:88-112`):

| Step | Cost for n entries |
| --- | --- |
| `PanelTitle.describe` | shortens the path (small) |
| `EntryFormat.format` for every entry (`:93-97`) | n × (`PadRight`, `Substring`, `DateTimeOffset.ToString`, size text) — ~5 string allocations each |
| `EntryColor.classify >> attribute` (`:99-102`) | n × `ToLowerInvariant` of the extension |
| `list.SetSource(ObservableCollection rows)` (`:104`) | new collection of n strings, Terminal.Gui re-measures |
| `SetMark` for every entry (`:106-107`) | n calls |
| `PanelFooter.describe` (`PanelFooter.fs:8-23`) | four passes over n (filter, filter, filter, sum) |
| `AppState.cursorEntry` (`AppState.fs:238`) | `List.tryItem` — O(cursor) on an F# list |

For a directory of 50,000 entries an arrow key costs on the order of 250,000
string allocations and two full collection rebuilds, on the UI thread, before
the cursor moves. The inactive panel pays the same although nothing in it
changed.

Fix. Memoise per side: keep the last rendered `PanelState` and skip
`SetSource`/`SetMark`/`RowColors` when `Entries` and `Selected` are the same
references; on a cursor-only change set `SelectedItem` and refresh the
footer. `AppState.PanelState.Entries` should become an array (or
`IReadOnlyList`), since every access is by index (`cursorEntry`,
`withCursorWithin`, `namesOf`, `List.tryItem` in `toggleSelectedThrough`).

### P2. The results window re-renders every hit on every tick — High

During a search `SearchRunner.gather` (`Marbas/Adapters/SearchRunner.fs:35-46`)
runs every 100 ms and calls `ResultsWindow.add` then `ResultsWindow.walking`,
each of which calls `refresh` (`Marbas/Ui/Find/ResultsWindow.fs:112-130`).
`refresh`:

1. `window.Rows` (`:55`) regroups *all* hits into a `Dictionary` and a list of
   rows (`ResultRow.rows`, `ResultRow.fs:16-31`);
2. `Describe` formats *every* row (`EntryFormat.format` per hit);
3. `standingRow` (`:103-110`) copies the whole current source with
   `Source.ToList()` to read one row;
4. `SetSource(ObservableCollection shown)` replaces the collection;
5. `Progress` → `Counted` (`:65-71`) scans all hits twice.

So each tick is O(H) for H hits so far, twice, and a search that finds H hits
over T ticks does O(H·T) work on the UI thread — for a whole-disk search with
200,000 hits the window is re-formatting 200,000 rows twenty times a second
by the end, which is where the panels stop responding. `rowUnderCursor`
(`:132-135`) also regroups all hits on every `Enter`/`F3`.

Fix. Append the new hits' rows to the existing `ObservableCollection` (the
group header only when the location changes); keep the counts as they arrive
(`FindCounts` is already computed by `FileSearch.run` — have `report` carry
it); update the summary line alone on `walking`.

### P3. Operations on a results panel are quadratic in the number of hits — High

`SearchListing.hitFor` (`Marbas/FileSystem/SearchListing.fs:65-66`) finds a hit
by recomputing `displayName` — which calls `FindHit.spelled` and a
`Path.Combine` — for every hit until one matches. It is called once per entry
by `AppState.hitAt` (`AppState.fs:399-402`), which `entryPath` (`:481-487`),
`locationOf` (`:413-421`), `entryInArchive` (`:532-537`) and `bareName`
(`:369-375`) all go through. So:

- `operationPaths` on m marked entries: m × H path combines (`:489-490`);
- `keepUnfinished` after the batch: m × H again (`:377-385`);
- `transferSource` → `fromResults` (`:518-522`): m × H;
- `deleteScope` (`:589-602`): up to 2 × m × H.

Marking everything in a 50,000-hit panel (`+` with `*`) and pressing `F5`
performs ~2.5 × 10⁹ path combines before the overwrite question appears.

Fix. Build an index once when the `SearchResults` location is created:
`Map<EntryName, FindHit>` keyed by display name (or store the display name on
`FindHit` at `showResults` time), and make `hitFor` a lookup.

### P4. A results panel re-verifies every hit and re-opens every archive on each refresh — High

`SearchListing.read` → `surviving` (`SearchListing.fs:44-63`) does
`File.Exists`/`Directory.Exists` per hit and, for hits inside archives,
opens each archive and reads its full index (`heldIn`, `:33-42`). It runs
inside `AppState.loadLocationFocusing` (`AppState.fs:204-208`) for every
listing of a results panel: `Ctrl+R`, `toggleHidden`, every `finish*` after a
batch, and `readTabQuietly` (`:704-710`), which `ExternalChangeDetected`
(`Update.fs:338`) raises whenever *the other panel's* directory changes. A
results panel with 100,000 hits on disk costs 100,000 stat calls on the UI
thread each time a file is written into the opposite panel.

Fix. Verify on demand (`Ctrl+R`, and once when the panel is first shown) and
not on an external change of the other panel; cache archive indexes by
`(path, length, LastWriteTime)`.

### P5. Renaming or moving a directory copies the tree — High

`Operations.moveTo` (`Marbas/FileSystem/Operations.fs:283-295`) handles a
directory as `copyTo` followed by `delete`; `move` (`:297-300`) and `rename`
(`:302-310`) both go through it. Renaming a 50 GB directory in place therefore
reads and writes 50 GB, and `F6` between two directories on the same volume
does the same, whereas `Directory.Move` is a metadata operation there. The
copy-then-delete is also not atomic: a failure in the middle leaves both
trees. `FILE-OPERATIONS.md:76-78` records the cross-volume reason but not the
same-volume cost. `rename` additionally runs on the UI thread with no
progress window (`Shell.fs:478-481`).

Fix. Try `fs.Directory.Move` first and fall back to copy-then-delete only
when it throws `IOException` for a cross-volume move (the standard shape);
route `rename` of a directory through `RunBatch` when the fallback is taken.

### P6. Three to four system calls per entry in every walk — Medium

`DirectoryListing.read` (`DirectoryListing.fs:58-69`) does it right:
`EnumerateDirectories`/`EnumerateFiles` return `IFileSystemInfo` with kind,
size, attributes and link target from one enumeration. Every other walk
enumerates *paths* and then asks the file system again per entry:

| Walk | Per entry |
| --- | --- |
| `Operations.deleteTree` (`Operations.fs:140-149`) | `kind` (2 × `Exists`) + `isLink` (`FileInfo.New` + `LinkTarget`) |
| `Operations.copyTo` (`:239-267`) | `kind` + `linkTarget` |
| `Operations.wipe` (`:218-233`) | `IsCancelled` + `kind` + `isLink` |
| `DirectoryMeasure.sized` (`DirectoryMeasure.fs:36-41`) | `kind` + `isLink` + `FileInfo.New(...).Length` |
| `FileSearch.entryOf` (`FileSearch.fs:29-36`) | `kind` + `DirectoryInfo`/`FileInfo.New` + attributes + `LinkTarget` |
| `Archive.add` (`Archive.fs:154-176`) | `kind` + `isLink` + `FileInfo.New` for size and time |

Measuring or searching a tree of one million files issues three to four
million stat calls instead of one million directory reads. On a network share
each call is a round trip.

Fix. Walk over `EnumerateFileSystemInfos()` and pass the `IFileSystemInfo`
down; `FileSystemPath.linkTargetOf` already takes one.

### P7. Blocking work on the UI thread — Medium

These run inside `Runtime.Dispatch` with no progress window and no `Esc`:

- Permanent delete and trash (`Shell.fs:355-360`): `Operations.delete` walks
  the tree (P6); `RecycleBin.send` is a synchronous shell call that can take
  seconds on a large tree.
- Directory rename (`Shell.fs:478-481`, see P5).
- Opening an archive, changing level inside it, `Ctrl+R` there, and every
  external change to the archive file: `ArchiveListing.read`
  (`ArchiveListing.fs:50-57`) reads the whole index with `List.ofSeq`
  (`:55`) and re-reads it per level. `SearchListing.heldIn` and
  `DirectoryMeasure.scan` read it again for their own purposes. A zip with
  500,000 entries is parsed each time `Enter` is pressed inside it.
- `deleteScope` (`AppState.fs:589-602`) enumerates every marked directory
  before the confirmation opens (`Update.fs:362-366`).
- `Update.transition` → `changing` (`Update.fs:142-145`) and
  `Shell.changingArchive` → `Archive.changeRefusal` → `needsPassword`
  (`Archive.fs:307-314`) each read the archive's index once more to answer
  whether an entry is encrypted, before the batch starts.

Fix. Route delete/trash through `RunBatch` (the wipe already is); cache the
archive index for the archive under a panel, keyed by `(path, length,
LastWriteTime)`, and answer levels, `needsPassword` and `heldIn` from it.

### P8. Whole archive entries are buffered in memory — Medium

- `FileWindow.contentOf` (`Marbas/FileSystem/FileWindow.fs:37-51`) copies the
  entry into a `MemoryStream` to view it: `F3` on a 4 GB entry inside a zip
  allocates 4 GB.
- `FileSearch.contentOf` (`FileSearch.fs:116-120`) does the same for every
  archive entry a text search visits, even though `ContentMatch.inStream`
  reads a stream in 64 KiB buffers and stops at the first match.
- `Archive.buffered` (`Archive.fs:413-421`) buffers each kept entry while
  rewriting a 7z or tar.gz (documented in `PROGRESS.md` as the price of the
  rewrite; the largest entry bounds the memory).

Fix. For the viewer and the search, open the entry stream inside a scope
(`ArchiveReader.withItems`, see architecture A5) and hand the stream itself
to `Source`/`ContentMatch`; the tar readers already expose a forward-only
stream and the zip/7z readers seek.

### P9. 7z entries are opened one by one through the archive API — Medium, to verify

`ArchiveReader.items` (`ArchiveReader.fs:85-90`) reads zip, 7z and rar through
`ArchiveFactory.OpenArchive` and exposes each entry's content via
`OpenEntryStream`. 7z archives are solid by default: entries share one LZMA2
stream, and random access to entry k means decompressing entries 1..k-1
again. Extracting all n entries of a solid archive this way is O(n²) in
decompressed bytes unless the library keeps the solid stream open between
sequential calls. SharpCompress documents `IReader` (`ExtractAllEntries`) as
the sequential path. This needs one measurement: time `Shift+F2` on a 7z with
a few thousand small entries against `7z x`. If it is quadratic, use the
reader path for extraction of every kind, the way tar already does.

### P10. `Update.transition` allocates ~40 closures per message — Low

`Update.fs:291-330` partially applies forty helpers before matching, on every
message including `CursorMoved`. Negligible next to P1, but it is pure
overhead and disappears with the readability fix in the code-quality report
(Q1).

### P11. The line index and the viewer on very large files — Low

- `LineIndex.starts` (`LineIndex.fs:18`) keeps an `int64` per line: a
  100-million-line log costs 800 MB once `End` is pressed, because
  `FileViewer.lastRow` (`FileViewer.fs:81-86`) calls `ScanAll`.
- `LineIndex.scan` (`:30`) → `Source.Read` (`FileWindow.fs:16-26`)
  allocates a fresh 64 KiB array per chunk; a 4 GB file scanned to the end
  allocates 65,000 arrays.
- `FileViewer` computes `visible()` twice per refresh — in `Rows()` for the
  draw and in `Percent` for the header (`FileViewer.fs:137`, `144-152`) — and
  wrapped mode appends with `rows @ …` per line (`:68`).

Fix. Reuse one read buffer; index every k-th line start (checkpointing) and
rescan between checkpoints; compute `visible()` once per refresh.

### P12. Sort comparator recomputes keys per comparison — Low

`DirectoryListing.order` (`DirectoryListing.fs:111-116`) sorts with a
comparator that calls `EntryName.extension` (a `Substring`) and
`String.Compare(…, InvariantCultureIgnoreCase)` on every comparison. Sorting
100,000 entries by extension performs ~1.7 million culture-aware compares and
as many substring allocations. `List.sortBy` with a precomputed key tuple
`(groupRank, key, name)` computes each key once; a `StringComparer` instance
held once avoids the per-call lookup.

### P13. `Encoding.RegisterProvider` on every archive open — Low

`ArchiveNames.legacyEncoding` (`ArchiveNames.fs:17-27`) registers the code-page
provider and resolves the OEM encoding each time `ArchiveReader.items` builds
its options (`ArchiveReader.fs:40-45` → `ArchiveNames.reading ()`). Both
should be computed once per process.

### P14. Held-modifier changes re-render everything — Low

`ModifierWatch.poll` (`ModifierWatch.fs:31-45`) fires every 100 ms; on a change
`MainWindow` calls `runtime.Refresh()` (`MainWindow.fs:52-54`), which is the
full P1 render, though only the bottom bar's labels changed.

### P15. Wiping is bound by the CSPRNG — Low, by design

`Operations.overwrite` (`Operations.fs:165-190`) fills each 64 KiB block with
`RandomNumberGenerator.Fill`. Cryptographic random generation runs at a few
hundred MB/s on typical hardware, so wiping is slower than copying by that
bound. One pass is the documented choice; a non-cryptographic generator would
make it disk-bound, if that is ever wanted.

## What is already right

- `DirectoryListing.read` enumerates once and reads no entry twice.
- `ContentMatch.scan` reads 64 KiB at a time with a bounded carry
  (`ContentMatch.fs:39-68`) and never holds a file whole.
- `LineIndex` scans forward on demand and caches 256 decoded lines
  (`LineIndex.fs:75-96`); the viewer reads only the rows it shows.
- `DirectoryWatch.collapsing` (`DirectoryWatch.fs:42-59`) turns a burst of
  events into one listing, and the poll asks `Exists` for two paths every two
  seconds and nothing more.
- `SearchListing.surviving` opens each archive once per verification
  (`SearchListing.fs:45-53`) — the problem in P4 is how often verification
  runs, not how it runs.
- Batches, packing, extraction, measuring and the search walk run on a
  worker thread and marshal progress through `app.Invoke`.

## Suggested order

1. P1 — memoised panel render and array-backed entries (every user, every
   keystroke).
2. P2 — incremental results window (every search).
3. P3 + P4 — indexed hits and on-demand verification (every results panel).
4. P5 — `Directory.Move` first (every rename of a directory).
5. P6 + P7 — `IFileSystemInfo` walks, delete/trash under `RunBatch`, archive
   index cache.
6. P8, P9 — streaming archive entries; measure 7z extraction.
7. P10-P15 as the files are touched.

## Summary table

| # | Finding | Severity |
| --- | --- | --- |
| P1 | Full re-render of both panels per message | High |
| P2 | Results window re-renders all hits per tick | High |
| P3 | Results-panel operations quadratic in hits | High |
| P4 | Results panel re-verified on every refresh | High |
| P5 | Directory rename/move copies the tree | High |
| P6 | Three to four syscalls per entry in every walk | Medium |
| P7 | Blocking work on the UI thread | Medium |
| P8 | Archive entries buffered whole | Medium |
| P9 | 7z solid extraction via per-entry streams (verify) | Medium |
| P10 | Closures per message in `Update` | Low |
| P11 | Line index memory and allocation on huge files | Low |
| P12 | Sort keys recomputed per comparison | Low |
| P13 | `RegisterProvider` per archive open | Low |
| P14 | Modifier change triggers a full render | Low |
| P15 | Wipe bound by CSPRNG (by design) | Low |
