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

## A screenful laid out twice (P21.15, 2026-10-11)

The viewer's content view kept the rows it had last laid out, keyed by the
encoding, the mode, the wrap, the top, the left and the size, so that the
percentage in the header and the draw after it shared one screenful. P21.15
gathers what a view keeps into one record behind one cell, and that memo was a
second cell. To know what taking it out costs, a probe built in Release timed
`ViewerState` over a real file on disk — 120 lines, 7.7 KB, a screen of 100 by
22, one page down from the start — 20 000 rounds a row, the range of three
runs. A refresh is what one key costs once the state has moved: the percentage,
which lays the screenful out, then the screenful again for the draw and the
marks on it.

| What | Microseconds |
|---|---|
| text, one screenful | 4.2–4.5 |
| text, a refresh | 13.7–15.2 |
| wrapped text, one screenful | 4.2–4.4 |
| wrapped text, a refresh | 16.7–16.8 |
| hex, one screenful | 221–222 |
| hex, a refresh | 383–388 |

**The memo saved one screenful a key, and that is what a key now costs more.**
Four microseconds in text and a fifth of a millisecond in the dump: at the
thirty keys a second a held key repeats, under one percent of the time between
two of them. The memo was not kept in any form. The draw used to ask for the
rows a second time to mark the match on them; it now marks the rows it has
just laid out, so the screenful is laid out twice a key and not three times.

**The dump costs fifty times the text.** A row of the dump is read from the
file when it is laid out, sixteen bytes and three on either side, where a line
of text comes out of the lines the index has already decoded. That difference
was seen, not taken apart: at a fifth of a millisecond a screenful nobody waits
on it.

## Where two slow loops spent their time (P21.16, 2026-10-11)

P21.14 left two numbers out of proportion to the work: `LineIndex.Index.ScanAll`
over a million lines, and `TextEncoding.detect` on a full probe. Each was taken
apart before anything in it was changed. A probe built in Release ran copies of
the two loops with their parts added one at a time, on the file of P21.14 — a
million lines of 72 bytes, 69 MB, UTF-8 with Polish letters — and on its first
8 KB as the probe of detection. Milliseconds, on one Windows machine; the range
is of the fastest of five runs over two runs of the probe.

| The scan of the index, a part added per row | Milliseconds |
|---|---|
| every chunk read, and nothing else | 16–17 |
| and every line feed in it found | 47–50 |
| and the source asked for its length at every line start | 2 815–2 839 |
| and every start noted, each sixty-fourth kept | 2 825–2 844 |
| the source asked for its length a million times, and nothing else | 2 746–2 761 |

**The index spent its time asking how long the file is.** Reading 69 MB takes
17 ms, finding a million line feeds in it 31 ms more, noting them about ten,
and the rest — 2.77 of 2.83 seconds, 98 of every hundred — is one comparison a
line start, `start < source.Length`, which keeps a feed at the very end of the
file from starting an empty line. `Source.Length` is the stream's length, and a
file opened for shared writing, as the viewer opens one, has the system asked
each time: 2.8 microseconds a question, a million questions. A stream over the
same file opened for shared reading only answers a million times in 2 ms, so it
is the sharing that makes the question dear, and the viewer keeps it: a file
another program is writing can still be read. The walk of the same file never
asks, which is why it read the same bytes in a twentieth of the time.

| One examination of the probe, by its parts, 20 000 times | Milliseconds |
|---|---|
| the three byte order marks | 2 |
| the test for a wide encoding | 5 300–5 336 |
| the search for a zero byte | 50 |
| the UTF-8 walk, 1 026 sequences longer than a byte | 530–537 |

**Detection spent its time counting zero bytes through a sequence.** The test
for UTF-16 without a mark counts the zeros on each side of every pair of bytes,
and did it with a sequence of the pair's bytes, filtered and measured, built
twice: 265 microseconds of the 295 an examination took. The UTF-8 walk cut a
slice out of the probe for every sequence longer than a byte to ask whether its
later bytes continue it, and came to 27. The marks and the zero byte are
nothing beside them.

**Each got the change its numbers pointed to.** The scan asks for the length
once a chunk, after reading it, and compares every start in the chunk with that
answer. The wide test counts with a recursion over the pairs, and the walk asks
the bytes where they lie; no array or sequence is made for either.
`ContentMatch`, which asked `detect` for the encoding and `contentOf` for
whether the file is binary and so examined every probe twice, takes both from
one `TextEncoding.examine`. The rows below are the probe of P21.14 with three
added, built once against the code before and once against the code after and
run alternately, three rounds; fastest of five runs, then the median.

| What | Fastest before | Median before | Fastest after | Median after |
|---|---|---|---|---|
| the index over the million lines | 2 801–2 823 | 2 810–2 829 | 67–68 | 68 |
| the same, and sixteen lines near the end read | 2 828–2 841 | 2 842–2 847 | 63 | 66–68 |
| the walk over the same file, every line | 140–142 | 142–144 | 142–151 | 222–230 |
| detection, 20 000 probes of UTF-8 with Polish letters | 5 820–5 832 | 5 823–5 858 | 399–405 | 403–407 |
| detection, 20 000 probes of plain ASCII | 5 682–5 718 | 5 705–5 727 | 444–448 | 447–452 |
| detection, 20 000 probes Latin-1 at byte 8 000 | 5 683–5 694 | 5 698–5 701 | 438–441 | 441–448 |
| a text searched for in 2 000 files of 16 KB, in none | 1 413–1 418 | 1 422–1 428 | 220–224 | 223–234 |

**The index is forty times faster and now quicker than the walk.** `End` in the
million lines waits 68 ms where it waited 2.8 seconds. P21.14 read 3.8 to 4.1
seconds for the same scan and 202 to 228 ms for the walk; in these runs the
same code gave 2.8 seconds and 142 ms, so the figures of the two entries are
not to be set side by side, only the rows of one table.

**Reading a line near the end no longer costs what a rescan cost.** A block of
sixty-four line starts is found again by reading a chunk from its checkpoint,
and the rescan asked for the length at every feed in that chunk, some nine
hundred of them. Sixteen such blocks cost about thirty milliseconds on top of
the scan before, and nothing that shows after.

**Detection costs 20 microseconds a probe, from 290, and a search pays it
once.** Over the two thousand files a search for text took 0.71 ms a file
and takes 0.11; the 0.58 ms the two examinations came to is 0.02 for the one.
Timed apart again after the change, the wide test takes 1.9 microseconds, the
zero byte 2.5 and the UTF-8 walk 15, or 17 over a probe that is all ASCII. The
walk is what is left, and it was not taken further.

**The walk was not touched, and its slower median is not the code.** Its
fastest run is what it was. After the change the walk starts half a second into
the process where it started seventeen seconds in, and with tiered compilation
switched off both binaries walk the file in 246 to 252 ms, so what differs is
how far the runtime had warmed up when the row began.

## A listing that asks for the mode and not for the link (P22.2, 2026-10-13)

P22.2 had to read the executable bit of every file a panel lists on Unix, and
the only way through `IFileSystem` is `fs.File.GetUnixFileMode` — one more
question to the system per file, on a path where that is forbidden. So the
listing was timed before the change and after it, and taken apart first. A
probe built in Release calls `DirectoryListing.listForDisplay` as a panel does,
`read` beneath it and `children`, which every walk reads a directory through,
on a directory of 10 000 files of a hundred bytes and 100 directories, every
tenth file with its executable bit: on ext4 under WSL 2 and on NTFS on the
same Windows machine, and on 2 000 files and 20 directories of the Windows
drive as WSL mounts it (9p), where every question costs about half a
millisecond. The binary before and the binary after ran alternately, five
times each (three on the mount), forty rounds a run after five to warm up;
milliseconds, the median of the medians.

The listing taken apart, each part added to the one above:

| What is asked of each entry | ext4, 10 000 | 9p mount, 2 000 | NTFS, 10 000 |
|---|---|---|---|
| the enumeration alone | 12.8 | 1 138 | 4.2 |
| + `Attributes` | 11.9 | 1 124 | 4.2 |
| + `LastWriteTime` | 13.0 | 1 133 | 5.8 |
| + `Length` | 12.5 | 1 142 | 7.2 |
| + `LinkTarget` | 16.4 | 1 989 | 541 |
| + `LinkTarget`, only where `Attributes` say `ReparsePoint` | 12.6 | 1 144 | 7.3 |
| `GetUnixFileMode` of every file, by itself | 7.4 | 1 935 | — |

**The link was the cost nobody had timed.** `FileSystemInfo.LinkTarget` asks
the system every time, for a plain file as for a link: `readlink` on Unix, and
on Windows a handle opened on the entry, a control code sent to it and the
handle closed. On NTFS that was 98 % of a listing — 0.05 ms an entry, over half
a second for ten thousand. The attributes an enumeration has already read say
whether an entry is a reparse point, which every symbolic link and junction is
on both systems, so `FileSystemPath.linkTargetOfInfo` now asks for the target
of those alone.

| | `listForDisplay` before | after | `read` before | after | `children` before | after |
|---|---|---|---|---|---|---|
| NTFS, 10 000 | 561 | 26 | 541 | 7.0 | 544 | 7.4 |
| ext4, 10 000 | 36.9 | 38.3 | 18.9 | 20.6 | 19.8 | 16.3 |
| 9p mount, 2 000 | 1 936 | 2 211 | 1 936 | 2 213 | 1 942 | 1 116 |

**On Windows a directory of ten thousand is listed in a twentieth of the
time**, and a walk reads one in a seventieth: copying, deleting, measuring and
searching all read a directory through `children`. Nothing asks for a mode
there.

**On Linux the mode is paid for by the link, nearly.** With the mode asked and
the link still asked of every entry, the listing took 41.9 ms on ext4 and
3 110 ms on the mount — an eighth and more than half on top. With both changes
it takes 1.4 ms more than before on ext4, 0.14 microseconds a file, which is
inside the spread of the five runs for `listForDisplay` and outside it for
`read`; on the mount it takes 275 ms more, a seventh. A walk asks for no mode
and is a sixth faster on ext4 and takes little more than half the time on the
mount.

**The price was taken as in proportion.** A panel on Linux shows which files
are programs for a millisecond and a half in ten thousand files. The mount is
the worst case there is — two seconds for two thousand files before anything
was changed — and a quarter of a second more buys the same answer; what is
left of it is the second `stat` the wrapper of System.IO.Abstractions forces,
since the runtime's own `FileInfo` holds the mode from the one the enumeration
already made. Reading the mode for the rows on screen alone, or reaching the
runtime's enumeration past the wrapper, would take that back; neither was
done.

## A directory dated as it is packed and unpacked (P22.3, 2026-10-13)

P22.3 hands every directory's time to the archive writers and sets it again on
every directory unpacked, once the rest is written. Packing asks nothing new —
the time is the one the enumeration already read — and unpacking gains one
call per directory. A probe built in Release packed and unpacked a tree of
1 020 directories and 3 000 files of a hundred bytes through `Archive.write`
and `Archive.extract` on a real disk, the binary before and the binary after
alternately: three times each on NTFS, five on ext4 under WSL 2.
Milliseconds, the median.

| | zip, pack | unpack | tar.gz, pack | unpack | 7z, pack | unpack |
|---|---|---|---|---|---|---|
| NTFS before | 527 | 7 102 | 270 | 6 405 | 864 | 6 309 |
| NTFS after | 526 | 6 800 | 261 | 6 692 | 914 | 6 365 |
| ext4 before | 203 | 336 | 69 | 511 | 895 | 233 |
| ext4 after | 199 | 334 | 67 | 514 | 919 | 238 |

**Nothing moved outside the spread.** On NTFS one unpacking differs from the
next by up to a tenth, and the three after lie among the three before. The
one column that reads slower on both systems, packing a 7z, was run ten more
times each way on ext4: 864 to 966 ms before with a median of 918, 826 to 958
after with a median of 869.

**The dating itself, timed apart**, is 44 ms for the 1 021 directories on
NTFS and 2.5 ms on ext4 — less than a hundredth of the unpacking on either.
The directories unpacked by the binary before were dated at the unpacking,
and by the binary after at the times the tree was given.

## A zip that keeps the moment (P22.4, 2026-10-14)

P22.4 writes the extended timestamp on every entry of a zip, and reads the
exact moments of a zip out of its central directory before SharpCompress
opens it. The first is on the path of packing, the second on the path of every
walk of a zip. A probe built in Release ran `Archive.write` and
`Archive.extract` over the tree of P22.3 — 1 020 directories and 3 000 files
of a hundred bytes — and `ArchiveIndex.read` and `ArchiveReader.entryInfo`
over three zips of 20 000 empty entries made for it: one with the local time
alone, one with the timestamp on every entry, one with the NTFS field. The
binary before and the binary after ran alternately on a real disk, three
launches each on NTFS — six for the unpacking — and five on ext4 under WSL 2.
Milliseconds, the median of the launches.

| | pack before | after | unpack before | after |
|---|---|---|---|---|
| NTFS | 474 | 474 | 7 300 | 7 310 |
| ext4 | 180 | 184 | 267 | 291 |

**Packing pays a microsecond an entry.** On ext4 the five launches after lie
between 182 and 187 ms and the five before between 179 and 183: four
milliseconds for 4 051 entries, spent in SharpZipLib writing a field of nine
bytes into two headers. On NTFS it does not show.

**A zip that carries the field takes longer to unpack, whoever unpacks it.**
Each binary unpacked the zip it had packed, so the column says two things at
once. Taken apart on ext4 — each binary over each zip, three launches of five
rounds:

| unpacked by | the zip packed before | the zip packed after |
|---|---|---|
| the binary before | 268 | 291 |
| the binary after | 273 | 285 |

The twenty milliseconds belong to the zip: about five microseconds an entry,
spent inside SharpCompress whichever binary calls it, and not taken further
apart. A zip of `zip`'s or of `tar`'s carries the same field, and the tree is
the worst case there is, a hundred bytes a file. What the reader of P22.4
adds for itself is inside the spread of either row. On NTFS, where unpacking
this tree takes seven seconds, one unpacking differs from the next by more
than any of it: 6 950 to 7 710 ms before, 7 140 to 8 710 after.

| a zip of 20 000 entries, ext4 | local time alone | the timestamp | the NTFS field |
|---|---|---|---|
| index before | 95.5 | 110.6 | 108.0 |
| index after | 95.8 | 109.9 | 109.0 |
| a walk that stops at the fourth entry, before | 0.04 | 0.04 | 0.04 |
| the same, after | 0.26 | 0.69 | 1.13 |

**A whole walk costs what it cost.** The central directory is read once more
and walked for the two fields, which by itself takes 0.24, 0.35 and 0.42 ms
for the three zips on NTFS once the code is warm; and an entry that has an
exact moment no longer asks for the one the library made. On NTFS the
launches of either binary lie between 57 and 100 ms and say nothing finer.

**A walk that stops early pays for the whole directory.** `entryInfo` — what
`F3` on an entry asks — used to read as far as the entry it wanted; it now
reads the central directory first, 1.3, 1.5 and 2.0 MB for the three zips,
and the cost follows that size: a quarter of a millisecond to a millisecond on
ext4, 0.4 to 1.4 ms on NTFS, where it was 0.07. It is paid once for a file
viewed or one file copied out, and not per entry.

The read was taken apart on the way, on NTFS, by the walk that stops early
over the zip with the timestamp. As first written it took 4.4 ms.

- Turning every moment into a local time as it was read was a third of that.
  The moments are kept as ticks, and `ZipMoments.at` makes a `DateTimeOffset`
  of one when its entry is reached, in place of the conversion the library's
  own time needed: 3.0 ms.
- The fields were looked up through pipelines of `ValueOption.bind` and
  `filter`, a closure a stage and an entry. As matches: 1.5 ms.
- The central directory was read into a new array each time, a megabyte and
  more. From the shared pool the scan by itself, in memory and warm, takes
  0.26 ms where it took 0.49; with that, and the numbers read by plain
  arithmetic on the bytes and not through a span, the walk took 1.0.
- Each field was searched for with a pass of its own over the extra fields
  of a record. With one pass for both: 0.75 ms.

**The price was taken as in proportion.** Packing a zip costs a fiftieth more
on the faster of the two systems, and unpacking one that carries the moment a
twelfth more for a tree of the smallest files — the price of the field, which
is what the task was to write, and which every other tool's zip already
carries. The reader costs a walk nothing that can be told from the spread,
and an early stop under a millisecond and a half in a zip of twenty thousand.

## A zip entry named and marked by the platform (P22.5, 2026-10-14)

P22.5 changes what is done for every entry packed into a zip: the stream is
handed no name transform, `ZipEntries` makes the entry — marked as made on
Unix there, with a mode — and the walk asks the sink whether it takes a file
before it adds one. The probe of P22.4, built in Release against the commit
before and against the tree after, ran `Archive.write` over the tree of
P22.3 — 1 020 directories and 3 000 files of a hundred bytes — and
`Archive.addEntries` of the same tree into a zip of one file, which is the
update. The two binaries ran alternately on a real disk, five launches each,
on NTFS and on ext4 under WSL 2. Milliseconds, the median of the launches.

| | pack before | after | add before | after |
|---|---|---|---|---|
| NTFS | 460 | 434 | 474 | 472 |
| ext4 | 165 | 163 | 185 | 184 |

**Nothing got slower, and packing on NTFS a little faster.** There the five
launches after lie between 433 and 441 ms and the five before between 450 and
468, so the twenty-six milliseconds are outside the spread: six microseconds
an entry. It is what the transform no longer run would account for — it went
over every name several times — and it was not taken further apart. On ext4
one launch differs from the next by more than the two columns do, 101 to 185
ms before and 157 to 165 after, and the update is the same on both systems.

On Windows the zip itself is the same: the tree packed and a small one added
to it by each binary gave two files of 652 090 bytes that do not differ in
one.

## A cursor moved through a transition read as a table (P22.11, 2026-10-15)

P22.11 took thirty-four arms of `Update.transition` out into private functions
and turned the short ones into pipelines. A pipe is inlined and a named
function is a direct call, so nothing should have changed, and §10 asks for
the number all the same: a key in a panel runs the transition. A probe built
in Release against the commit before and against the tree after loaded a
directory of 10 000 files and 100 directories on NTFS through `loadPanel` on
the real file system, then sent `Update.update` a hundred thousand
`CursorMoved` in a row, the cursor walking down the listing — so the
watching, the remembering and the marks that run after every transition are
in the number too. The two binaries ran alternately, five launches each,
forty rounds a launch after five to warm up. Microseconds a move, the median
of the rounds.

| launch | before | after |
|---|---|---|
| 1 | 0.503 | 0.500 |
| 2 | 0.497 | 0.497 |
| 3 | 0.497 | 0.495 |
| 4 | 0.501 | 0.502 |
| 5 | 0.496 | 0.515 |

**A move costs half a microsecond, before and after.** The median of the five
launches is 0.497 before and 0.500 after; one launch after is the slowest of
the ten, and the other four lie inside the spread of the five before.
