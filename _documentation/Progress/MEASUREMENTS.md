# Measurements

What was timed, against what, and what the numbers decided. A measurement lands
here when a task turned on it — a suspicion that had to be confirmed before a
rewrite, or a cost that had to be known before it could be called acceptable —
so that the reasoning behind the answer outlives the run that produced it. Each
entry names the task it belongs to and the day it was taken; a number read on
one machine on one day is evidence, not a constant.

## Unpacking a solid 7z (P15.17, 2026-09-24)

The performance review (P9) suspected that `Shift+F2` on a 7z is quadratic:
7z archives are solid, entries share one LZMA2 stream, and reaching entry *k*
through `ArchiveFactory.OpenArchive` could mean decompressing 1..*k*-1 again.
P15.17 timed it. Every archive holds *n* entries of about 400 bytes each,
written by Marbas itself and unpacked to a real disk through the same
`Archive.extractAll` the `ExtractTargets` effect calls; `7z x` from 7-Zip 25.01
is the reference; milliseconds, one run per cell on one Windows machine.

| Archive | Targets | n=1000 | n=2000 | n=4000 | n=8000 | n=16000 |
|---|---|---|---|---|---|---|
| 7z | 1 | 2 622 | 4 301 | 9 218 | 18 728 | 39 866 |
| zip | 1 | 2 438 | 4 555 | 9 198 | 18 043 | — |
| 7z | *n* | 4 847 | 15 006 | 49 894 | — | — |
| zip | *n* | 4 638 | 12 848 | 40 860 | — | — |
| `7z x` | — | 686 | 1 028 | 1 863 | 4 644 | 11 763 |

**The suspicion is wrong, so extraction stays on `ArchiveFactory`.** With one
target, doubling the entries doubles the time — 16× the entries cost 15.2× the
time from 1 000 to 16 000 — and a solid 7z costs the same as a zip, which is not
solid at all. SharpCompress keeps the solid stream open across sequential
`OpenEntryStream` calls inside one walk, so nothing is decompressed twice and
there is no reason to move extraction to the `IReader` path the tar kinds use.
What is left is a constant factor of about 3.4× against `7z x`, which is the
per-file `CreateDirectory` and `LastWriteTimeUtc` write that every unpacked
entry pays through `IFileSystem`.

**`Shift+F2` is quadratic anyway, for an unrelated reason.** The bottom two rows
of the table hold the same archives as the top two; only the number of *targets*
differs, because `Shift+F2` asks for one target per top-level entry. `Archive.extract`
walks the whole archive once per target (`unpackTarget` → `ArchiveReader.withItems`),
so *n* top-level entries mean *n* walks. It costs 5.4× at n=4000 and grows with
the square, and it does so identically for zip and for 7z — the container has
nothing to do with it. Unpacking the same content held under one directory is
linear, which is why the review's own measurement never saw it. The fix is one
walk that serves every target; it is not the fix P9 proposed, so P15.17 recorded
it and P15.25 carries it out.

## One walk for every target (P15.25, 2026-09-27)

P15.25 replaced the walk per target with one walk for all of them, and was done
when the *n*-target rows of the table above came out linear. The same kind of
archives as in P15.17 — *n* entries of 400 bytes, written by Marbas — unpacked to
a real disk through `Archive.extractAll`: *n* targets is the flat archive with
one target per entry, as `Shift+F2` asks for it; 1 target is the same content
under one directory. Milliseconds, one run per cell, the same Windows machine.

| Archive | Targets | n=1000 | n=2000 | n=4000 | n=8000 |
|---|---|---|---|---|---|
| 7z | *n* | 2 923 | 5 419 | 10 473 | 21 513 |
| 7z | 1 | 2 786 | 5 177 | 10 310 | 20 489 |
| zip | *n* | 2 969 | 5 810 | 10 847 | 20 870 |
| zip | 1 | 2 772 | 5 420 | 10 422 | 21 150 |

**The square is gone.** Doubling the entries doubles the time on every row, and
*n* targets now cost what one does, within 5 %: at n=4000 a flat 7z takes
10.5 s where it took 49.9 s, and a flat zip 10.8 s where it took 40.9 s. n=8000,
which P15.17 did not try with *n* targets, takes 21 s. The targets are found by
looking up each entry's own prefixes rather than by comparing it with every
target, so matching does not bring the square back either. What is left is the
constant factor against `7z x` that P15.17 put down to writing through
`IFileSystem`.

## The progress window flooded and drained (P21.3, 2026-10-09)

Until P21.3 every path a batch reported queued an `Invoke` that wrote it into
the progress window's line. P21.3 replaced that with one cell the worker
overwrites and a timer on the UI thread that shows what it holds every 100 ms.
A probe ran the real `BatchRunner.run` under a real `Application`, in a console
of its own, from a Release build, before and after the change: once with a
batch that only reports *n* paths and does nothing else — the limit a batch of
very small, very fast items approaches — and once with a batch that copies *n*
files of 100 bytes on the same disk with `File.Copy` and reports each. *work* is
when the batch's work returned on the worker, *closed* when `BatchRunner.run`
returned to its caller, which is when the window is gone and the panels can
move again. Milliseconds, one run per cell, on one Windows machine.

| Batch | n | work before | closed before | work after | closed after |
|---|---|---|---|---|---|
| reports only | 10 000 | 29 | 612 | 25 | 91 |
| reports only | 100 000 | 1 709 | 3 217 | 3 | 65 |
| reports only | 1 000 000 | 68 718 | 103 854 | 25 | 67 |
| copies | 2 000 | 2 262 | 2 324 | 2 189 | 2 239 |
| copies | 10 000 | 7 527 | 7 586 | 8 104 | 8 167 |
| copies | 40 000 | 29 194 | 29 248 | 34 031 | 34 102 |

**A fast batch was held up by its own progress, twice over.** Before, the
window stayed open long after the work was done — 583 ms for 10 000 reports,
35 s for a million — while the UI thread worked through one callback per path,
and the work itself slowed down, from 29 ms for 10 000 reports to 69 s for a
million, as each report queued a callback the UI thread was busy taking off
the same queue. After, both are flat: the work costs what the work costs, and the
window closes within a tick of it.

**A batch that touches the disk did not flood, and does not change.** Copying a
file takes about a millisecond here, slower than the UI thread drains a callback, so
the queue never grew and the window closed some 60 ms after the work, before as
after. The copy rows differ by up to 17 % in the work itself, in both
directions across sizes, which is the disk and not the window: the runner
does nothing between two copies that it did not do before, except write a
cell instead of queuing a callback. What the change buys a copy is the bound
— a batch of entries the disk answers quickly, such as a move inside one
volume, a delete or an archive's small entries, can no longer outrun the
window.

## One start read by two processes (P21.7, 2026-10-10)

P21.7 names the owner of a working directory by its process id and the moment
the process started, and a later session asks whether a process with that id,
started at that moment, still runs. The plan read that as an equality. Before
it was written as one, a script printed its own id and
`Process.GetCurrentProcess().StartTime` and slept, and three more runs of the
script read `Process.GetProcessById(id).StartTime` of the sleeping one, on .NET
10 under WSL 2 (Ubuntu 24.04, kernel 6.6):

| Reader | Start it read | Apart from what the process wrote |
|---|---|---|
| the process itself | `03:16:43.3317007` | — |
| second process | `03:16:43.3298761` | −1.82 ms |
| third process | `03:16:43.3318333` | +0.13 ms |
| fourth process | `03:16:43.3289356` | −2.77 ms |

**On Linux no two processes agree on a start to the tick.** The kernel keeps a
process's start as ticks since boot, and .NET turns that into a time of day by
adding the moment of the boot, which every process that asks has to work out
for itself. The spread is what that leaves; where exactly it comes from was not
taken apart, only measured. Windows keeps the start as a time of
day and gave every reader the same value: the owner file of a session started
by the probe of this task and `Get-Process` in another process both read
`03:17:12.4615735`.

**So the comparison allows two seconds.** An equality would have let every
session on Linux sweep the working directory of every other one still running.
`SessionProcess.sameStart` takes two starts within two seconds of each other
for one — three orders of magnitude over the spread measured, and still far
under the time in which an id comes round to another process. What the
allowance does not cover is a wall clock stepped by more than that while a
session runs, which moves the start a later process computes by the size of the
step; that is an entry in [Rough edges](../ROUGH-EDGES.md).

## Who a held clipboard keeps out (P21.8, 2026-10-10)

P21.8 was written against one observation from phase 18: a process that had
opened the clipboard with `OpenClipboard(NULL)` did not keep a second one from
opening it. Before the adapter was changed, a throwaway program compiled into
PowerShell 7 through `Add-Type` asked the system each question in turn, on
Windows 11 (10.0.26200). The first table is what `OpenClipboard` answers a
second caller while a first holds the clipboard open:

| The holder opened with | The second brings no window | The second brings a window of its own |
|---|---|---|
| no window | opens | refused, access denied |
| a window | refused, access denied | refused, access denied |

**An open clipboard is known by its window, not by who opened it.** A second
`OpenClipboard` with the same window succeeds, and no window is the same as no
window: across two processes, and across two threads of one. The `CloseClipboard`
that follows closes the clipboard for both, so the holder goes on writing to a
clipboard that is no longer held — its own `CloseClipboard` answered `false`.
The observation of phase 18 was this row: the second process there was the
adapter, which brought no window either. A program that opens with a window was
kept out even then, so the gap was narrower than the plan read it, and wider in
one direction: two threads of Marbas itself were never kept apart.

The second table is what decides where the window lives. A message-only window
was made on a thread of its own, the clipboard was opened with it from other
threads, and another program then replaced what the window owned:

| The window's thread | Opening with it from another thread | Another program's write, once the window owns the clipboard |
|---|---|---|
| has ended | — the window is gone with its thread | — |
| sleeps | opens, writes, the window is the owner | 5014 ms raw, 5019 ms through Windows Forms |
| runs a message loop | opens, writes, the window is the owner | 4 ms |

**So the window has a thread of its own that does nothing but answer it.** The
system sends the owner `WM_DESTROYCLIPBOARD` when someone else empties the
clipboard, and waits five seconds for a thread that never looks. Neither the UI
thread nor a batch's can be that thread: the first runs Terminal.Gui's loop,
not a Windows one, and the second ends and would take the window with it. Two
threads opening with the one window were let in together, as the first table
says they would be, so a lock stands around every request. Opening right after
a write was refused once and let in on the second attempt ten milliseconds
later — presumably something that listens for a change of the clipboard had
opened it in between, which was not looked into; the ten attempts the adapter
already made are what that needs.

What was written stayed after the owner was gone: text read by another process
after the window was destroyed on its own thread, and after the process that
made it had exited, with `GetClipboardOwner` answering no window both times.
An attempt to destroy the window from another thread was refused, access
denied, which is why nothing tries to at exit.

## The walks without their counters (P21.14, 2026-10-11)

P21.14 took the mutable counters and the set of visited places out of
`FileSearch.run`, the two dictionaries and two flags out of its scan of an
archive, the mutable record out of the unpack walk, and turned
`ArchiveReader`'s callback walk into a fold, so that each of them threads a
value and answers it. All four are paths a user waits on, so they were timed
before and after: a probe built in Release once against the source of the
last commit and once against the changed tree, run alternately on the same
data on a real disk. The data: a tree of 200 directories of 100 one-byte files;
that tree packed by Marbas into a stored zip of 20 201 entries; and two flat
zips of 2 000 and of 8 000 files of 400 bytes. Milliseconds, on one Windows
machine. Each cell is the range, over the rounds, of the fastest run and of the
median run — three rounds of fifteen runs for the first two rows, two rounds of
seven for the last two.

| What | Fastest before | Median before | Fastest after | Median after |
|---|---|---|---|---|
| the tree walked, every file a hit (20 000) | 698–709 | 1 028–1 066 | 697–718 | 1 026–1 043 |
| the three archives searched into (30 204 hits) | 99–131 | 145–186 | 120–126 | 187–190 |
| 2 000 targets unpacked into memory | 77–83 | 89–90 | 80–82 | 90–95 |
| 8 000 targets unpacked into memory | 156–175 | 229–248 | 181–210 | 232–262 |

**Nothing a user waits on got slower.** Every row overlaps its own spread:
the walk of a tree is the directory listings, the search inside an archive is
the reader, and what either does with a count between two entries does not show
beside them. Unpacking was timed into a `MockFileSystem` on purpose. To a real
disk the same 8 000 targets took 5.7 to 6.4 s before and 5.9 to 7.4 s after,
across runs that each wrote into a directory tree larger than the one before —
a spread several times wider than the quarter of a second the whole read and
its bookkeeping come to in memory, so the disk rows say nothing about the
change either way. Reading the index of the 20 201 entries cold took 99 to
116 ms before and 76 to 101 ms after.

**One version on the way was slower, and the probe is why it was caught.** The
first immutable bookkeeping of an archive's directory levels wrote the map on
every entry, where the dictionaries it replaced were written only when an entry
was newer than what was held. The search into the archives took 224 and 254 ms
with it, against 197 to 228 ms before. Leaving the map alone when an entry
changes nothing — the same test the dictionary version made — brought it back
to the row above.

**The loops that stayed mutable were timed as they stand.**
[Coding style](../coding-style-conventions.md) lets a hot loop keep a mutable
binding where it was timed, so the four that keep one have a number now. None
of them was changed, and the same binaries gave the same figures before and
after; the ranges are across seven runs of the whole probe.

| What | Milliseconds |
|---|---|
| `LineIndex.Index.ScanAll` over a text of 1 000 000 lines, 69 MB | 3 814–4 082 |
| `LineIndex.walkFrom` over the same file, every line | 202–228 |
| `TextEncoding.detect` on an 8 KB probe of UTF-8, 20 000 times | 6 074–6 387 |
| `RewindingStream`, 69 MB read through and a seek back to the middle | 22–25 |

**Two of them are slow, and not because of how they hold a variable.** The
index takes nineteen times as long as the walk over the same bytes, and
detection costs 0.31 ms a probe, 38 ns a byte. Both loops are exactly as
mutable as the walk that reads 69 MB in a fifth of a second, so the binding is
not what the time goes to; what it does go to was not looked into in this task.
Detection is more than its one mutable loop — the probe is also examined for a
wide encoding and for a zero byte — and a search for text runs all of it twice
per file, once for the encoding and once for whether the file is binary.
Both are entries in [Rough edges](../ROUGH-EDGES.md) now.
