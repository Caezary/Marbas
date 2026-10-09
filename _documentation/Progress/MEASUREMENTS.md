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
