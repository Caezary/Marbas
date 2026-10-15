# File operations

**Operation targets are one rule, shared by every operation key.**
`PanelState.operationTargets` answers with the marked entries in listing order,
or with the cursor entry when nothing is marked, and never with `..` — so a
function key pressed on the parent entry has nothing to do and asks nothing.
`F5`, `F6`, `F8`, `Shift+F1` and `Shift+F2` all take their sources from it.

**Operations answer per item, and one bad item never stops the batch.**
`Operations` mirrors `DirectoryListing`: every function returns
`Result<unit, OperationError>` instead of throwing, and the `*All` functions map
a list of source paths to `ItemResult` values in the input order, so the UI can
report exactly which entries failed and why. An `ItemResult` says where the item
came from — an `ItemSource`, which is a `FileSystemPath` on disk, the archive and
the entry's `ArchivePath` inside one, or the batch itself when the batch as a
whole failed — so two entries of one name in different places never trade
outcomes. A single-item failure inside one *tree* does abort that tree — a
half-copied directory stops at the first error rather than pressing on blindly.

**Nothing reaches the file system outside `attempting`.**
`Operations.attempting` is the one place an exception becomes a value: it runs
the work and maps whatever is raised through `mapException`, so
`UnauthorizedAccessException` is an `AccessDenied` and everything else an
`OperationFailed` carrying the reason. `attempt` is the same thing for work
answering nothing, and `Archive` builds on the same function rather than
keeping a copy. *Listing* a directory goes through it too, which is what makes
a directory the user may not read fail its own item and leave the rest of the
batch to run; before, it escaped the batch entirely.

**Every walk reads a directory once and carries what it read.**
`DirectoryListing.children` enumerates a directory as `IFileSystemInfo`s and
answers a `Child` per entry — where it is and the `Entry` a listing would show
for it, kind, size, time, attributes and link target all from that one read.
`Operations.delete`, `copyToPath` and `wipe`, `DirectoryMeasure`, `FileSearch`
and `Archive.add` all walk over it, and none of them asks the file system
anything more about an entry it has already enumerated. They used to: every one
of them enumerated *paths* and then asked again per entry — two `Exists` calls
for the kind, a `FileInfo` for the link target, another for the length — three
to four system calls for every file in the tree, each one a round trip on a
network share. `DirectoryListing.existingEntry` is the same answer for a single path,
and it is what `delete`, `wipe` and `Archive.add` ask at the root of a walk,
where there is nothing enumerated yet. Measured over a mock tree of 20,000
files in 200 directories, measuring cost 700–800 ms with a stat per entry and
400–480 ms from the one enumeration; a real disk charges far more for the
difference than a mock does.

**A batch that raises anyway still closes its window.** `Operations.guarded`
wraps the work a batch runs and turns anything that escapes into one
`ItemResult` whose source is the batch itself (`WholeBatch`), carrying
`BatchFailed` with the reason.
`BatchRunner` runs the work through it, so the promise that the progress window
closes and `finish` is called holds whatever happens on the batch thread — an
exception used to skip both, leaving a progress window that answered nothing
and a failure nobody saw. It is a net, not the mechanism: every operation is
expected to answer with `ItemResult`s, and a `BatchFailed` reaching the
failures dialog means one of them did not.

**Every question is answered before the batch starts.** `Operations.approve`
walks the sources, asks Yes / No / Yes to all / No to all / Cancel about each
one whose target already exists, and answers with the sources to actually work
on — `No` drops one, `Cancel` drops the lot, since nothing has happened yet.
The two *to all* answers become a `StandingAnswer` that stands in for the
question from then on, so a later collision is settled without asking while a
source that collides with nothing is still taken. What runs
afterwards is decision-free: `copy` and `move` carry no overwrite flag any more
and always replace, because whoever called them has already decided. The only
refusal left lives in `startRename`, which checks its own target. (P5.1 had the
flag, P5.4 had the batch asking as it went; P5.6 made both untenable, because a
background thread cannot open a dialog and wait on it.)

**A transfer into itself is refused before it is asked about.** The walk of
`copying` lists a directory's children only when it reaches them, so a
directory copied into its own subdirectory meets its fresh copy among them and
copies it again, one level deeper each time, until a path grows too long — or,
with a subdirectory listed before the files, until the stack runs out. A move
that cannot rename goes through the same copy. `Operations.landsWithinSource`
beside `approve` answers whether a target is one of the sources or lies under
one, by `FileSystemPath.atOrUnder` on the platform's rules, and
`Update.requestIfOutsideSource` puts every copy and move from the disk through
it before its effect is requested: `F5` and `F6` into the other panel, a typed
`F6` target, a paste of copied or cut files, and a copy or move into an archive,
where the target compared is the archive's file — it would be read while it is
written. A refusal is `TargetWithinSource` naming that target, the error and
sentence packing uses, and it comes before the unchangeable-kind refusal, so no
question is asked and no batch starts. Entries carried out of an archive have
no path on disk to land under and are not compared. The comparison is of paths:
a target that reaches a source only through a link is not caught, the same
limit `Archive.wouldPackItself` has, since it calls the same function.

**One question per item covers the whole tree.** `approve` asks only when the
*top-level* target already exists — and if it does not, nothing inside it can
exist either, so no conflict can appear deeper in the recursion. That is what
makes asking up front complete rather than merely convenient: one answer
settles the item as a whole, every file inside it included, and a refusal stops
it before anything is written. Sources that were dropped never appear in the
results, which is what keeps their marks in place.

**Copy, move, delete and the trash are the same batch with a different verb.**
`copyAll`, `moveAll`, `deleteAll`, `trashAll` and `wipeAll` are two lines each
over a shared `allWatched`, which owns the walk and the cancellation check; only
the operation applied to each source differs. Every one of them therefore names
each source on the progress window and stops where `Esc` was pressed, leaving
the sources it never reached out of the results and so with their marks. A
rename is not part of it: it touches one entry, so it stays a plain `Result`
carried by `RenameFinished` — `Operations.wholeOutcome` is what turns the
one-item batch its fallback does run into that `Result`.

**A delete walks the tree under the same control the batch carries.**
`Operations.delete` takes a `BatchControl`, names every entry it is about to
remove and answers `Cancelled` at the entry it stopped at, so `Esc` means the
same thing inside one large tree as it does between two targets. What it has
already removed stays removed; the item fails, its mark survives, and pressing
the key again carries on. The removal that ends a move which fell back to
copying is the exception: it runs under a control whose cancel flag is off,
because once the copy is through, stopping would leave the tree in both places.

**A bare name means rename, anything else means a directory.** `F6` suggests
the other panel's path, and what comes back decides the operation:
`FileSystemPath.isBareName` (no directory part and not rooted) plus exactly one
target means rename in place; otherwise the text is a directory — combined with
the source panel's path, so a relative name lands beside the entries and an
absolute one is taken as it stands. That keeps one edit box doing both jobs
without a mode switch, the way the orthodox managers do it.

**A rename moves the cursor with the entry.** `finishRename` reloads focusing
`ByName newName`, so the renamed entry keeps the cursor even though the listing
reorders around it. Every other reload focuses whatever the cursor was on.

**Moving a directory is a rename first, a copy only when it has to be.**
`Operations.moveToPath` asks `IDirectory.Move` and answers `Renamed`,
`RenameRefused` or `RenameFailed`. On one volume the rename is a metadata
operation whatever the tree weighs, so `F6` between two directories of one
drive and a rename in place no longer read and write the whole tree, and
nothing can leave two trees behind halfway. `IDirectory.Move` throws
`IOException` across volumes — and also when the target directory already
stands — and only that answer falls back to `moveByCopying`, which is how a
move onto an existing directory still merges into it. Any other exception is
the move's own failure and is reported as such rather than retried as a copy.
Files still use `IFile.Move`, which overwrites.

The fallback is the only path that can be stopped halfway, so `Esc` during a
same-volume move either arrives before the rename and leaves everything where
it was, or arrives after it and finds the work already done.

**A rename opens a batch only if the file system refuses to do it itself.**
`Operations.startRename` checks the target, tries the metadata rename and
answers `RenameDone` or `CopyNeeded target`; `Shell` dispatches `RenameFinished`
straight away for the first, and for the second hands `moveByCopying` to
`Runners.Batch` under `Renaming`. So the common rename is still one instant `Result`
with no window flashing past, and the one that has to copy a tree — a rename
across a mount point, a junction — runs off the UI thread with `Esc` meaning
what it means everywhere else. This is the second half of "a rename in place
must not read and write the tree": the first half stopped it copying at all
where the file system can rename, this one stops the remaining copy freezing the
screen.

**Marks survive exactly what did not finish.** After a batch, the panel keeps
the marks of the entries whose `ItemResult` is not `Ok` (and that still exist);
everything that succeeded loses its mark. The match is made on the item's
`ItemSource`, not on its bare name, so the panel asks what each marked entry's
source is and compares that. Delete, copy and move share the rule, so a failed
batch can be retried by pressing the key again.

**A confirmation is an effect; its wording belongs to Ui.** `ConfirmDelete`
carries a `DeleteRequest` — the target names, a `DeleteScope` saying
whether the delete stops at those entries (`NamedEntriesOnly`) or reaches
inside a directory as well (`WithDirectoryContents`), and the `DeleteMode`
asked for — and `DeleteConfirmation.describe` turns that into the question,
`DeleteConfirmation.title` into the title above it.

The scope is found in two steps, because half of it is a read. `DeleteRequested`
raises `InspectDelete` carrying an `AppState.DeleteQuestion`: the paths whose
contents have to be looked at, and whether the targets already reach into a
directory without looking — which is what a directory entry inside an archive
says by itself. The interpreter answers it with `AppState.deleteScope`
(`Operations.isNonEmptyDirectory` per path, and not even that when the question
is already settled) and dispatches `DeleteScopeLoaded`, which is the message the
`ConfirmDelete` is built from. A directory that cannot be enumerated counts as
non-empty, so the warning errs towards showing up.

**Deleting has three meanings, and the key says which.** `DeleteMode` is
`ToTrash`, `Permanently` or `ByWiping`: `F8` and `Del` ask for the first,
`Shift+F8` and `Shift+Del` for the second, `Alt+Del` for the third, which has a
section of its own below. The mode rides the whole way through —
`DeleteRequested`, the `DeleteRequest` being confirmed, `DeleteConfirmed`, and
the `DeletePlaces` effect — so nothing has to remember which key started it,
and the question the user answers is the question that gets carried out: *Move
'docs' to the trash?* under **Delete**, or *Delete 'docs' for good?* under
**Delete for good**.

**The trash is a port; a permanent delete is not.** `Shell.interpret` picks
`Operations.trashAll` over `deps.Processes.SendToTrash` for `ToTrash` and
`Operations.deleteAll` for `Permanently`; both are `allWatched`, so both name
their sources and stop at `Esc`. What the port does is the platform's business
and `Dependencies.create` decides it — the Windows recycle bin through
`RecycleBin.send` and `Microsoft.VisualBasic.FileIO.FileSystem`
(`SendToRecycleBin`), the freedesktop trash through `Trash.send` everywhere
else. Both answer `Result<unit, OperationError>`, so a refusal is reported
like any other item failure and the marks of what did not go survive.

**The freedesktop trash is ordinary file-system code.** `Trash.send` writes
`info/<name>.trashinfo` — `[Trash Info]`, the URL-encoded original `Path`, the
`DeletionDate` — and then moves the entry into `files/<name>`, taking
`name_2`, `name_3` … when the name is already there (both directories are
checked, since either one being taken makes the name unusable). The info file
is written first, as the spec asks, and removed again if the move fails, so a
failed delete leaves no phantom entry behind. Because every step goes through
`IFileSystem` — the move is `Operations.moveToPath`, which copies and deletes a
directory rather than renaming it across volumes — the whole thing is tested on
`MockFileSystem`, on both platforms, without a real trash anywhere.
`Trash.location` picks `$XDG_DATA_HOME/Trash` or, when the variable is not
set or blank, falls back to `$HOME/.local/share/Trash`, mirroring
`Settings.location`; `HostEnvironment` hands it over as an `option`.

**A session gets a directory to work in, and takes it with it when it goes.**
Some operations need somewhere to put a file that belongs to neither panel — an
entry on its way from one archive to another, above all. `Workspace.location`
answers `<temporary root>\Marbas\<session>`, where the root is the platform's
own (`%TEMP%`, `$TMPDIR`, `/tmp`, read once into `HostEnvironment`) and the
session is a fresh id per run, so two Marbas processes never share a directory.
`Workspace.claim` makes it when the session starts and writes who owns it,
`Workspace.ensure` makes it again should something have removed it since —
each answering an ordinary `OperationError` if it cannot, never an exception —
and `Workspace.release` removes it whole.

**The workspace is a path on `Dependencies`, not a port**, the same choice
`SettingsPath` made and for the same reason: `Workspace` is ordinary
`IFileSystem` code, so it runs against `MockFileSystem` in the tests, while an
opaque function port would have left it untestable. `Shell.interpret` releases
it on `Quit`, beside the settings write, which is what makes "gone after the
application quits" a test rather than a claim.

**What a session could not take with it, the next one removes.** `Quit` is the
only way out that releases the directory. A console window closed with its
button, a logoff, a killed or crashed process and `Ctrl+Q` pressed while a
dialog holds focus all skip it, and a handler for the console's close signal
would only narrow that: nothing runs when a process is killed. So the fix is
at the other end. `Startup.firstSteps` raises one more effect once the panels
stand, `SweepLeftovers`, and `Shell` hands the work to `Runners.Background`, off
the UI thread, so a large leftover cannot hold up the first screen:

1. `Workspace.claim` makes the session's own directory and writes the file
   `owner` into it: two lines, the process id and the moment the process
   started, as `SessionOwner.lines` spells them — the id in invariant digits,
   the moment as a `DateTimeOffset` in the round-trip format. The pair names
   one process even after the system has given the id to another.
2. `Workspace.sweep` reads the directories directly under
   `<temporary root>\Marbas` other than the session's own. One whose `owner`
   names a process `Processes.IsRunning` does not find is removed whole, with
   `Operations.delete`. One with no `owner`, or one that cannot be read as an
   owner — a session of a version from before P21.7, or a file half written —
   is removed once neither it nor anything under it has been written for a
   day, so an older Marbas still running beside a new one keeps its files.
   The walk for the latest write stops at a link, as the delete does.
3. A directory that will not go — a file in it held open — is left where it
   is, and `sweep` answers it as a `SweepRefusal` with the `OperationError`.
   `Shell` writes each as a `LeftoverKept` entry to the log and shows nothing:
   the user did nothing to cause it. The next start tries again.

Only directories directly under `Marbas` are looked at. A file beside them and
anything beside `Marbas` itself are never touched, and a `Marbas` directory
that is not there yet is no error.

**The directory is made at the start, not at first use.** Until P21.7 a session
that never needed its working directory never made one. Writing the owner file
where the directory is made would have meant every place that makes it:
`Workspace.scratch`, which `Archive.copyToArchive` calls with nothing but a
path, and `CommandProcess.writeList`, which makes the directory of a list file
on its own. One claim at the start covers all of them, at the price of an empty
directory and a file of two lines for the length of every session.

**Whether a process runs is a port.** `Processes.Owner` is the session's own
process and `Processes.IsRunning` answers whether an owner's process is still
there; `SessionProcess` in `Adapters` fills both from
`System.Diagnostics.Process`. A process id nobody has, and a process that ends
while it is being asked about, are not running. One whose start cannot be read
— Windows refusing access to a process of another account — is taken to be
running, because keeping a leftover costs a directory and removing a live
session's costs its files.

**The same start is the same moment within two seconds.** Windows hands every
reader the same start time for a process, to the tick. Linux does not: .NET
adds the ticks since boot from `/proc/<pid>/stat` to a boot time each process
works out for itself, and two processes reading the start of a third came out
up to 2.8 ms apart
([Measurements](../Progress/MEASUREMENTS.md#one-start-read-by-two-processes-p217-2026-10-10)).
Compared to the tick, every session on Linux would have looked dead to the
next one. `SessionProcess.sameStart` therefore allows two seconds either way.
The allowance errs towards keeping: a process that took over the id within two
seconds of the owner's start would be mistaken for it, and the leftover would
wait for that process to end.

The process id is an `int` on purpose: it is the system's number, written and
compared and never computed with.

**Delete is an effect now, like copy and move.** It used to happen inside the
`AppState` transition for `DeleteConfirmed`, which left no room for a port that
the state layer must not know about. The walk is now `DeleteRequested mode` →
`ConfirmDelete` → `DeleteConfirmed mode` → `DeletePlaces` → the removal in
`Shell` → `DeleteFinished results` → `AppState.finishDelete`, which only
re-reads the active panel and keeps the marks of what did not finish. Since
P15.7 every mode goes through `Runners.Batch`, because the places path always did and
the two paths became one: the trash and a permanent delete run off the UI
thread behind the progress window, and since P15.15 they name what they are
working on and stop where `Esc` was pressed, like the wipe beside them.

**Per-item failures share one dialog.** `Operations.deleteAll` answers for every
target, `Operations.failures` keeps the ones that went wrong, and the
`Report` effect carries them as `Problem.ItemFailures`, worded by
`OperationErrorMessage.describeFailures` as one `name: reason` line each. The
panel is refreshed first, so what survived a partial failure is on screen
before the dialog appears.

## Wiping

**A wiped file is overwritten, shrunk, renamed and only then deleted.**
`Operations.wipe` clears the read-only attribute, writes one pass of random
bytes over the whole length of the file through `IFileSystem` in buffers of
64 KiB — reporting and asking the batch control about cancellation after each —
flushes, truncates the file to nothing, renames it and deletes it. A directory
is walked depth-first and removed after what was in it; `wipeAll` runs the
targets as a batch the way `copyAll` does. The order is proved on the mock, not
inferred: `Fakes.WipeWitness` records what the file held at every flush and
every rename.

**One pass, not three.** On a solid-state disk with `TRIM`, on a copy-on-write
file system and on one with a journal, further passes buy nothing the first did
not — the controller or the file system decides where the bytes land, not the
writer — and each costs the whole length of the file again.

**The pass is cryptographic random, and that sets its speed.** Each 64 KiB
block is filled by `RandomNumberGenerator.Fill`, which runs at a few hundred
megabytes a second, so a wipe is slower than a copy of the same file by that
bound (perf P15 of the 2026-09-19 review). A non-cryptographic generator would
make the wipe as fast as the disk, and would write a pattern that can be
predicted from a few blocks of it; the one pass there is should not be the
predictable one, so the generator stays as it is.

**The name is as much of a leak as the contents.** A deleted file's name can
outlive it in the directory, so before the delete the file is renamed to a
random name of its own length in the same directory, and nothing about the old
name is left but how long it was. The name is drawn from lowercase
hexadecimal digits, which can never spell a device name Windows reserves; if a
hundred draws all collide with names already there, the file keeps its name
rather than failing the wipe.

**What it cannot promise is said before it starts.** `Alt+Del` asks under
*Wipe*, with two sentences the other deletes do not carry: that each file is
overwritten with random bytes before it is deleted, and that on a solid-state
disk or a copy-on-write or journaling file system the old blocks may outlive the
file anyway. The second is broken by hand, because a message box is as wide as
its longest line and the one-line version came out wider than an 80-column
terminal. Wiping is not a guarantee on modern storage, and a confirmation that
implied otherwise would be the one untrue sentence in the application.

**A run stopped inside a file leaves that file and names it.** `Esc` answers
`Cancelled` with the path of the file it was in; the file stays where it was,
already overwritten, because its contents are gone whatever `Esc` says and
pretending otherwise would be untrue. Unlike the other deletes, wiping has
something to report, so it leaves the synchronous path and runs through
`Runners.Batch` under *Wiping*; what comes back is the same `DeleteFinished`, so the
panel is read again and the marks of what was not wiped stay.

**A link is removed, never written through.** Overwriting a link would destroy
what it points at, so a link to a file or to a directory is removed unwritten —
see [Links](LINKS.md). Inside an archive there is no disk block to overwrite,
only an archive to rewrite, so `Alt+Del` there asks the question `F8` asks.

## Measuring

**`F3` on a directory measures it.** `F3` views the file under the cursor, as
before; anywhere else it measures the operation targets — the marks, or the
directory under the cursor — through `MeasureTargets`. `..` measures the
directory it leads to, which inside an archive at its root is the directory
holding the archive.

**A size knows whether it is the whole truth.** `DirectorySize` is `Exact` or
`AtLeast`. Three things make a walk answer a lower bound rather than a lie: a
subdirectory that refuses to be read, a walk stopped part way, and an archive
that cannot be opened at all. `DirectorySize.add` keeps `Exact` only while both
sides are. The type is in `Domain`, because a listing entry carries it long
before anything is measured; the walk is `DirectoryMeasure` in `FileSystem`.

**One walk for a disk and for an archive.** `DirectoryMeasure` takes a
`PanelLocation`, so it measures a directory on disk or a level inside an
archive. An archive is measured from its index, read once and summed below the
level asked for — reading it level by level would re-open the file once per
directory in it. A directory link is counted as nothing and never entered.

**It runs behind the progress window, through a port of its own.** Each
directory entered is reported through the `BatchControl` copying uses, so the
window has something to show and `Esc` something to answer. The port is
`Runners.Measure`, not `Runners.Batch`, because the batch runner speaks in the item
results copying and deleting answer with, and measuring answers with sizes.
A walk that falls over answers `Error` rather than an empty measurement:
`BatchRunner.measured` wraps the work the way `Operations.guarded` wraps a
batch, `MeasureFinished` carries a `Result`, and `Update` turns the `Error`
into `Report(Problem.Operation …)`. An empty list is a directory with nothing in it,
not a walk that failed, and the two used to look alike.

**A stopped measurement changes nothing, and a re-read forgets a finished one.**
`Esc` leaves the panel as it was, since a size stopped half way is worth less
than the `<DIR>` it would replace. A measurement belongs to the listing it was
taken over: `Ctrl+R`, an operation that finishes, the watch of phase 12 — any
re-read — drops it, which falls out of the listing being read afresh rather than
being a rule of its own. How the answer is shown and sorted is in
[UI and input](UI-AND-INPUT.md).

## Viewing and editing

**`F3` and `F4` take the cursor entry, not the operation targets.** Copy, move
and delete work on `operationTargets` — the marks, or the cursor when nothing
is marked. Viewing and editing are a different act: each opens one file, so
both read `AppState.cursorFilePath`, which also filters out directories and
`..`. There is nothing to view for a directory, so `F3` measures it instead
(see *Measuring* above).

**The viewer never reads a file whole.** `F3` opens a file, it does not load
one. `FileWindow.openFile` keeps the stream open behind a `Source` that answers
one byte range at a time, and the only thing read up front is a probe of
`TextEncoding.ProbeLength` bytes, which settles the encoding. Everything after
that is pulled as it is needed: the viewer asks for the rows its viewport shows
and nothing else, so a gigabyte opens as fast as a kilobyte and there is no size
limit and no truncation notice to write. An entry inside an archive holds to the
same promise since P15.16, through a `RewindingStream` that re-opens the entry
whenever the viewer reads behind where the decompressor stands;
[Archives](ARCHIVES.md) explains what that costs.

**Where a line begins is scanned forward, never indexed up front.**
`LineIndex.Index` scans on in `ChunkLength` steps only when asked for a line it
has not reached. Opening a file of a hundred thousand lines therefore indexes
one chunk, not the file. `ScanAll` is the one call that reads to the end, and
only `End` makes it. The scan works in the encoding's own code units, so a line
feed in UTF-16 is `0A 00` and not a stray byte, and the byte order mark is
skipped as a preamble rather than shown as content. A feed that ends the file
ends the last line too, so that line is shown without it.

**The index keeps one line start in every `LinesPerCheckpoint`.** Keeping an
`int64` for every line cost 800 MB on a log of a hundred million lines once
`End` was pressed. Since P15.23 the scan counts every line but remembers only
the start of every sixty-fourth — a checkpoint — and the starts between two
checkpoints are found again by reading forward from the earlier one when a line
there is asked for. The viewer asks for the rows on screen, so the block they
fall in is rescanned once and kept; sixteen such blocks are cached, and a block
still being scanned is not cached until it is whole. The same memory now holds
sixty-four times as many lines, and a line anywhere costs at most one read of
sixty-four lines. Both the scan and the rescans read into one buffer the index
owns (`Source.ReadInto`) instead of allocating a fresh 64 KiB array per chunk.

**The index asks how long the file is once a chunk.** A line feed at the very
end of a file starts no line, so every start the scan finds is compared with
the length of the file. `Source.Length` is the stream's own, and for a file
opened so that others may write to it, which is how the viewer opens one, the
system is asked each time. Asked at every line start, that question was nearly
all of what indexing cost;
[Measurements](../Progress/MEASUREMENTS.md#where-two-slow-loops-spent-their-time-p2116-2026-10-11)
has the numbers. The scan and the rescan of a block now ask after each chunk
they read and use that one answer for every start in it, and for whether the
scan is complete. A file that grows while it is indexed is therefore seen as
long as it was when its last chunk was read, the same as before to within a
chunk.

**The encoding decides, the content does not.** The old rule — a zero byte
anywhere means binary — turned every UTF-16 file into a hex dump, which is what
Windows PowerShell's `>` writes. So the order is now the other way round:
`TextEncoding.detect` reads the mark, falls back to a zero-byte pattern that
only UTF-16 makes, then to whether the probe is valid UTF-8, and last to
Latin-1, which maps every byte to a character and so can render anything. A
file always opens as text; `F4` switches to the hex dump and back, and the
switch keeps the place by going through the byte offset — the line's start
offset becomes a dump row, a dump row's offset becomes the line holding it.
Nothing guesses on the user's behalf and nothing is unreachable.

**A guess can be overruled, and the code pages need no registration.** Since
P19.2 `F8` lets the user read the file in another charset; the choice and its
place are in [UI and input](UI-AND-INPUT.md). `Charset` holds the four detection
can answer and five single-byte code pages besides — Windows-1250, CP852,
ISO-8859-2, Windows-1252 and CP437 — each answering its line feed (`0A`), its
unit (one byte) and the absence of a mark. `TextEncoding.chosen` builds the
encoding for a charset the user picked, keeping the mark only when the file
starts with that charset's own. The five code pages are not in .NET by default;
`TextEncoding` asks `CodePagesEncodingProvider.Instance` for each one, once, and
keeps the answer. The plan had the composition root register the provider for
the whole process instead. Asking the provider directly reaches the same tables
without a process-wide switch, so `TextEncoding` works the same whoever calls
it — a test included — and no start-up order has to hold for it. `ArchiveNames`
still registers the provider for itself, because it asks `Encoding.GetEncoding`
for whatever OEM code page the culture names.

`TextEncoding.characterLengthAt` answers how many bytes the character at a
position takes in an encoding — the lead byte's count in UTF-8, a unit or a
surrogate pair in UTF-16, one byte in the rest — or nothing when no character
starts there: a continuation byte, a lone surrogate, a sequence the end of the
bytes cuts short. The hex dump lays its character column out by it.

**A search walks the lines the way the screen reads them.** Since P19.3
`LineIndex.walkFrom` answers the lines from a line start onwards as a lazy
sequence of steps: `Reading offset` before each window of `ChunkLength` bytes
is read, then a `LineRead` for every line that window finishes, its start
offset and its text. A line is cut at `MaxLineLength`, loses a carriage return
before its feed, and a feed that ends the file ends the last line — the rules
`Index.Text` follows, and the tests hold the two to the same lines over the
same bytes. The walk keeps no index: it needs the start of the line it begins
on, which the viewer's own index already knows, and nothing behind it.

**The walk goes back as well, by finding where lines start.** Since P19.4
`LineIndex.walkBackFrom` answers the lines before a line start, the nearest
first: it reads the window of bytes that ends where it has got to, announces it
with `Reading` like the forward walk, and looks for feeds from the end of the
window towards its start in the encoding's own units. Each feed found starts a
line that ends where the one after it began, and what is left when the
content's start is reached — past the mark — is the first line. A line is cut
at `MaxLineLength` from its start, the end a forward reading sees, so it is
sliced from the window when it lies wholly in it and read again from the
`Source` when it began in an earlier window or ends in a later one. The tests
hold the backward walk to the index's lines too, reversed.

`WindowSearch.run` takes a walk, a `Query` — what is `Wanted`, a `LetterCase`
and a `SearchDirection` — the encoding the viewer reads in and a
`Place`, the start of a line and a column in it, and answers `Found` with the
match — the start of its line, the column in the decoded line and the length —
`NotFound`, `Stopped`, or `Unreadable` with a `ContentError`, the one place an
exception met on the way becomes a value. Forwards it looks in the line at the
place from the column on, then in every line after it, and takes the first
match; backwards it looks in that line before the column, then in every line
before it, and takes the last match of each — the line at the place is read
forwards to its end first, then the backward walk takes over. A match never
crosses a line break. The case compares by `LetterCase.comparison`, ordinal
either way, and whole words keep a match whose neighbours are not word
characters — letters, digits and `_`, `WordMatch.isWordCharacter`, the rule
*Find files* applies to file contents, now shared by both. Every occurrence is
tried, so a match that is part of a word does not hide a whole word later in
the line. A `Control` beside it carries `Reached`, told the offset at every
`Reading`, and `IsCancelled`, asked at the same step, so a stop is noticed
within one window of bytes however long the lines are.

**What is wanted is a text or a pattern, never both and never unchecked.**
Since P19.5 `Wanted` is `Literal` — a `FindText` and its `WordMatch` — or
`Pattern`, a `SearchPattern`. A pattern carries no `WordMatch`, because whole
words are not offered with a regular expression: a pattern says where its
words end itself, with `\b`. `SearchPattern.tryCreate` is the one place a
regular expression is parsed and the one place its exception becomes a value:
`PatternDoesNotParse` for a `RegexParseException`, `PatternNotSupported` for
what the engine refuses, each with the sentence .NET gave. So a `Query` that
reaches the search always holds a pattern that parses; nothing is read for one
that does not.

The engine is `RegexOptions.NonBacktracking`. It runs in time linear in the
line, so no pattern can hold the walk on one line for long, and a stop is
still noticed within a window of bytes; the backtracking engine could spend
minutes on one line of a megabyte, and its only remedy, a match timeout, would
have ended such a search with an error rather than a stop. The price is what
the linear engine cannot do — look-arounds, back-references, atomic groups —
and those are refused when the popup is confirmed, the same as a pattern that
does not parse. `SearchPattern.regex` builds the expression with the case the
query asks for, once per search, culture-invariant. The pattern is matched
against one decoded line at a time, so `^` and `$` are the line's ends and a
match never runs past it. Every position where a match starts is tried, the
way the literal search tries every occurrence, and a match of no characters is
passed over: it has nothing to mark, and `Shift+F7` would stand still on it.
So `^$` finds nothing, and `x*` finds the first run of `x`.

**Bytes are searched as bytes, window by window, with no lines at all.**
Since P19.6 `ByteSearch.run` looks for a `WantedBytes` from an offset.
`WantedBytes.tryParse` reads what the hex field holds: tokens separated by
spaces or tabs, each an even run of hexadecimal digits read in pairs — so
`4D 5A` and `4D5A` are the same two bytes — or a single digit, which is a
byte of its own (`A` is `0A`). A token of any other shape, an odd run longer
than one digit included, is `NotAByte` with the token named, and a field of
spaces is `NoBytes`; `spelled` writes the bytes back as `4D 5A`.

Forwards the search reads a window of `ChunkLength` bytes at the offset, looks
for the run in it (`MemoryExtensions.IndexOf` over a span) and moves on by the
window less one byte short of the run, so a run split by the edge of a window
is whole in the next one; a window that comes back short is the end of the
file. Backwards it reads the window that ends where a run starting just before
the offset would end, takes the last occurrence in it (`LastIndexOf`), and
steps back the same way, so a run counts when it starts before the offset even
if it ends after it — the rule the text search follows with its column. Both
announce each window to `Reached` and ask `IsCancelled` before reading it, and
answer the same `WindowSearch.Answer`, made generic for this: `Found` of a
`ByteSearch.Match`, the offset and the length, here, and of a
`WindowSearch.Match`, a line, a column and a length, there. Both are called
`Match` because both are what a search found, and the module says which kind.
Nothing is decoded, so the code page the viewer reads in has no say.

**A `Source` can be read from two threads.** The search runs off the UI thread
while the viewer under the progress window may still draw, and both read
through one stream: `Read` and `ReadInto` take a lock on it, since a seek and
the read after it must not interleave. The search walks the file with its own
buffer and builds no `LineIndex` of its own, so the viewer's index is touched
only on the UI thread.

**Formatting stays pure, and so does the scrolling.** `HexDump.row` renders one
row at an absolute offset, `PrintableText.describe` turns tabs and control
characters into something a terminal can draw, `LineWrap` cuts a line into the
rows it takes or the window shown of it, `ViewerScroll` clamps a move and
measures how many lines fill a page, and `ViewerHeader.describe` lays the top
bar out to a width. Where the viewer stands — mode, wrap, top row, left
column, and what a search marked — is a `ViewerState`, and every move is a
function of `ViewerState` that answers the next one;
`FileViewer.ContentView` holds the current one and exposes `Rows()`, the very
list it draws, so a test asserts on what the screen would show without a
driver painting one
([What a view remembers](UI-AND-INPUT.md#what-a-view-remembers)).

**Closing the viewer is injected, because `RequestStop` needs a running app.**
`Runnable.RequestStop` does nothing unless the window is being run by an
application, so a test cannot observe it. `FileViewer.create` therefore takes
the closing action as its first argument — `Prompts.showFile` passes
`_.RequestStop()`, the tests pass a recorder — which leaves "F3 and Esc close
the viewer, other keys do not" as an ordinary assertion. The key handler sits
on the content view because that is the view holding focus, and it marks the
key handled so the `F3` binding on the main window cannot reopen a viewer on
top of the one being closed.

**The editor is a port, and its failure is an operation failure.**
`Processes.StartEditor` takes the path and answers
`Result<unit, Operations.OperationError>`; `EditorProcess.run` is the only
place that knows about `Process.Start`, `WaitForExit` and repainting the
screen afterwards, and it maps whatever the start threw through
`Operations.mapException`. That reuses the error type, the
`Problem.Operation` case and `OperationErrorMessage` rather than inventing
a second failure vocabulary for one case — an editor that will not start failed
on a path, like every other operation error. The tests pass a `StartEditor`
that records the path and can write to the mock file system, which is what
makes "the panel is not re-read when the editor starts" an ordinary test.

**Both start in the directory the panel stands in.** A `LaunchRequest` carries
its `WorkingDirectory` as a field, and `EditorProcess` derives one from the path
it was handed. Neither used to set it, so a program inherited whatever directory
Marbas itself was started from — a relative path typed into an editor's own
dialog, or written by a program started with `Enter`, would have landed
somewhere the user was never looking.

**Which editor to start is a pure choice.** `EditorCommand.choose` takes
`$EDITOR` as an option and a `Platform` (`Windows`/`Unix`) and answers with the
command: what was configured, trimmed, or `notepad`/`vi`. `Adapters` only
reads the environment and asks. A blank `$EDITOR` counts as unset.

**On Windows the editor is not waited for; on Unix it is.** Since P20.1
`EditorProcess.describe` answers an `EditorStart` — the `ProcessStartInfo` and
an `EditorWait` — and `run` only carries it out. On Windows the wait is
`StartsDetached` and the start goes through the shell (`UseShellExecute`), so a
console editor gets a console window of its own instead of drawing over the
panels, and `F4` hands the keyboard back at once; a window editor is started
from its file with its streams drained instead, as the launcher below explains. On Unix the wait is
`TakesTheTerminal` and the editor is started directly: a console editor such as
`vi` has no window to open, so it is given the terminal the panels are drawn in
and waited for. The choice is the platform's and nothing else's, and
`EditorProcessTests` prove both descriptions, and that `run` hands the terminal
over on Unix and never on Windows, with a fake hand-over and an editor that is
not there.

**On Unix the driver lets go of the terminal while the editor runs.** Until
P21.5 the editor shared the terminal with a Terminal.Gui driver still in raw
mode, on the alternate screen, with mouse reporting on and an input thread
polling standard input, so a full-screen editor lost keys to the panels and
left them half drawn. `TerminalHandOver.lend` now runs the start and the wait
between a stop and a start of the driver. The stop is
`IMainLoopCoordinator.Stop`: the input thread ends, the input gives raw mode
back and the output leaves the alternate screen. The start is what `Init` does
— a new coordinator with its own input thread, output and driver, its events
subscribed — and then the terminal's size, read through `Console.WindowWidth`
and `WindowHeight`, and a forced `LayoutAndDraw`. The views, the timers and
the session stack belong to the application and are untouched, so the loop
that was running goes on with the new driver on its next iteration. The start
is in a `finally`, so an editor that fails to start leaves the panels working
and its error is reported as any other.

Terminal.Gui 2.4.17 has no public way to do this — `IDriver.Suspend` sends
`SIGTSTP` to the whole process, and `Dispose` and `Init` would reset the
application along with the driver — so `lend` reaches three non-public members
of `ApplicationImpl` by reflection: the `Coordinator` property, `CreateDriver`
and `SubscribeDriverEvents`. `TerminalHandOverTests` assert that the version in
use has them, so an upgrade that renames one fails the suite rather than the
user's `F4`; should they be missing at run time anyway, `lend` falls back to
what Unix did before — the editor shares the terminal and a redraw is forced
when it returns. The size is set by hand because a rebuilt driver asks the
terminal for it with an ANSI query that is answered only on a later iteration;
until then the screen measures 0×0, and the error dialog of an editor that
could not be started threw on it. The spike that settled all this is in
[the phase 21 journal](../Progress/PHASE-21-THE-LOG-AND-THE-DEBTS.md).

**A started editor re-reads nothing.** `EditorStarted Ok` leaves the state as
it is. Until P20.1 it ran the refresh `Ctrl+R` runs, which made sense only
while `F4` waited for the editor to close; a detached editor has no moment of
closing to hook, and the directory watch of
[Auto-refresh](AUTO-REFRESH.md) already follows the file as it is saved —
on Unix too, where the editor is still waited for.

**Running a program is a second port, and its failures are their own
vocabulary.** `Processes.Launch` takes a `LaunchRequest` and answers
`Result<unit, LaunchError>`. It is not `StartEditor` and it does not reuse
`OperationError`, because nothing here is an operation on a file: the four ways
it fails — nothing associated with the name, the named program missing, the
system refusing, the elevation prompt turned down — say something about
*starting a program*, and three of them have no path-shaped answer at all. The
effect is `RunProgram`, the message back is `LaunchFinished`, and the wording
lives in `LaunchErrorMessage` beside every other message module.

**The launcher never waits, and that is the whole point of it.** `LaunchProcess`
starts the program and returns; since P20.1 `EditorProcess` does the same on
Windows and blocks on `WaitForExit` only on Unix, where a console editor has
nowhere to run but the terminal the panels are drawn in. Nothing re-reads a panel afterwards either — there is no
"afterwards" to hook. On Windows a launch goes through the shell verb
(`UseShellExecute`), which is what gives a console program a console of its own
instead of letting it write over the panels, and `runas` is the same call with
one field set — unless the program it names is a window program, below. On
Unix there is no shell verb: an executable entry is started
directly with both its streams redirected and drained into `Stream.Null` — the
terminal belongs to the panels — and anything else is handed to `xdg-open`.
Which of the two an entry is comes from `EntryAttributes.executability` — the execute
bit and nothing else, because a name that looks like a Windows program is not
one a Linux kernel will run.

**On Windows a window program is started from its file, with its streams
drained.** The shell verb gives a console program a console of its own and a
window program none, and an Electron program such as `Code.exe`, started that
way from a console, attaches to the console of its parent and writes its log
there. Under ConEmu (cmder) a fresh VS Code wrote some twenty lines over the
panels; under Windows Terminal and conhost it wrote nothing, which is why the
first spike of P21.6 saw nothing. Started without the verb and with both
streams redirected and drained, as Unix starts everything, it holds valid
standard handles and writes nowhere. So every start that names its program
asks first what kind of program that is: a user menu entry run `Directly`,
`Enter` on an executable or with a program chosen for the extension, and `F4`.
`ProgramResolution.resolve` in `Adapters` finds the file the way the shell
would — the name as written, against the panel's directory, then each
directory of `PATH`, then the App Paths of the registry (current user, then the
machine); a name without an extension is tried with each extension of
`PATHEXT` in order and never bare, and only a bare name is looked up on `PATH`
and in App Paths. `ProgramImage.subsystem` in `FileSystem` reads the PE header
over `IFileSystem` and answers `WindowSubsystem`, `ConsoleSubsystem`,
`OtherSubsystem`, `NotAnImage` or `UnreadableImage`. Only a resolved
`WindowSubsystem` is started directly, by its full path; a console program, a
script (`code` resolves to `code.cmd`), anything else and anything not found go
through the shell as before, and so does every run as administrator, since
`runas` exists only as a verb. A shell run of the user menu is `cmd` whatever
its line starts with, and a file opened through its association, where the
system picks the program, cannot be redirected and keeps the verb. The three
describers take the lookup as an argument — `Dependencies` binds it once, with
`PATH` and `PATHEXT` read at start — so their tests describe each start with a
fake that knows one window program, and no test starts anything.

**What the system already knows is a port too, and the parsing is not.**
`Processes.HandlersFor` takes an `EntryName` and answers a `Handler list` —
never an error, because "this platform knows nothing about `.zzq`" and "there is
no registry here at all" are the same answer to the caller: an empty list. What
sits behind it is deliberately split. The *reading* is platform code and lives in
`Adapters`: `RegistryHandlers` opens registry keys, and the Unix side hands
`DesktopHandlers.read` an `IFileSystem` and the resolved XDG directories. The
*parsing* is pure and lives in `FileSystem`, where the suite can reach it —
`ProgramCommand.programOf` for a command line, `DesktopEntry.parse` for one
`.desktop` file, `MimeDatabase` for `globs` and `mimeapps.list`. That is why the
Unix half is covered end to end over a `MockFileSystem` while the Windows half
is covered only as far as its parsing goes: the registry is the one source that
cannot be handed in.

**One command-line parser serves both systems.** A `shell\open\command` value
and a `.desktop` `Exec=` line are the same shape — a program, then arguments —
so `ProgramCommand.programOf` reads both. Quoted, it is what stands between the
quotes. Unquoted, it grows word by word until the text ends in something
`Executability` calls a program, which is what pulls `C:\Program Files\App\
app.exe %1` out whole, and falls back to the first word, which is what leaves
`firefox %u` as `firefox`. Environment variables are expanded by the Windows
adapter before the parse, because `%SystemRoot%` is the environment's word and
not the parser's.

**A launch failure is read off the exception, because that is the only place it
is written.** `Process.Start` reports a refusal as a `Win32Exception` and
nothing else, so `LaunchProcess` maps three native codes — 1155 (no
association), 1223 (a prompt the user cancelled) and 2 (not found, which is the
named program when one was named) — and calls everything else `LaunchRefused`
with the message the system gave. `Update` turns `ElevationDeclined` into
silence rather than a dialog: refusing an elevation prompt is an answer, not a
failure.

## The clipboard

**The clipboard takes the operation targets, as `F5` would.** `Ctrl+Ins` and
`Alt+C` raise `NamesCopyRequested`, and the transition reads
`PanelState.operationNames` — the operation targets, each under its own name.
That is the entry's name everywhere but in a panel of search results, where the
listing shows a hit under a path relative to where the search started
(`a\one.txt`) and the name copied is the hit's own (`one.txt`). Nothing marked
and `..` under the cursor is an empty list, which `ClipboardText.tryCreate`
turns down, so nothing is asked of the clipboard at all. The marks are left as
they were: copying text changes nothing on the disk, so there is nothing to
clear them for.

**A full path is spelled the way the panel title spells a place.**
`Alt+Shift+Ins` and `Alt+P` raise `PathsCopyRequested`, which reads
`AppState.fullPathsToCopy`: a target that is a search hit is spelled by
`FindHit.spelled`, and any other by `PanelLocation.spelledChild` of the place
the panel shows — a path in the directory on disk, or the archive's own path
with the entry's segments below it, as `PanelLocation.spelled` does for a level.
`FindHit.spelled` is now `spelledChild` of the hit's own location, so the two
cannot drift apart. A spelling through an archive is no path on the disk, and
it is not meant to be one: it names the entry the way the title and the status
line already do, and P18.5 is the task that reads such a path back. That is
also why the copy does not go through `operationPaths`, which answers only what
exists on disk and leaves a hit inside an archive out.

**`..` is the one entry the full path takes that is no operation target.** With
nothing marked and `..` under the cursor, the names and the places links lead
to copy nothing, but `fullPathsToCopy` answers the place the panel shows,
through `PanelLocation.spelled` — the directory, or the archive's level as the
title spells it — because that is the path a user standing there wants to hand
on, as the classic managers do, rather than the parent `..` leads to. A panel
of search results shows no one place, so there `..` still copies nothing. With anything marked, `..` under
the cursor changes nothing, as for every other command.

**Where a link leads is the entry's own question, answered once per target.**
`Ctrl+Alt+Ins` and `Alt+L` raise `LinkTargetsCopyRequested`, which reads
`AppState.placesLedTo`. A target with a `LinkTarget` — a symbolic link or a
junction, which `FileSystemInfo.LinkTarget` reads alike and the listing already
carries for the status line — gives that target, passed through
`FileSystemPath.resolvedFrom` the directory the link is listed in, so a relative
link comes out whole and an absolute one unchanged. A file on disk named
`*.lnk` gives what `Shortcut.target` reads from it. Anything else, and either of
them when nothing can be read, gives its own spelling from `operationSpellings`,
so the clipboard holds one line per target whatever happened. The directory a
link is listed in is the one the panel shows, not the one the operating system
would reach by following the links above it: the plan asked for the entry to be
resolved and nothing more, and the path the user sees is the one the answer
builds on. An entry inside an archive is never a link here — the archive
listing carries no `LinkTarget` — and a shortcut inside one is not extracted to
be read.

**A shortcut is parsed, not asked about.** `Shortcut.parse` reads the Shell
Link format from bytes, so it is tested byte by byte on `MockFileSystem` and
works wherever the file can be read, without COM or the Windows shell. It reads
the header and its class identifier, steps over the list of shell items, and
takes the target from the link information — the local base path and the common
suffix, in UTF-16 when the header carries the wide offsets, or the share name
and the suffix of a network path — and, failing that, the relative path from
the string data, resolved from the shortcut's own directory by
`Shortcut.target`. A path held only in the system's ANSI code page is accepted
when every byte of it is ASCII; anything else gives way to the relative path,
which Windows writes in UTF-16, because the code page a shortcut was written in
is not recorded in it. Every read is bounds-checked and answers an option, so a
truncated or foreign file is no target rather than an exception; a shortcut
larger than a megabyte is not read at all. A file that cannot be read is mapped
through `ContentError.mapException` and then, like an unparsable one, falls
back to the shortcut's own path, which is what the plan asked for. A shortcut
Windows itself wrote through `WScript.Shell` is read in a test on the real disk.

**A path read from the clipboard is forgiven in `FileSystem` and landed on in
the state.** `Ctrl+Shift+Ins` and `Alt+J` raise `ClipboardPathRequested`, and
the state answers `ReadClipboardPath` with the directory relative text will be
taken from — `PanelLocation.holding`, so a panel inside an archive or over
search results lends the directory on disk that holds it. `Shell` reads the
clipboard and hands the text to `PastedPath.read`, which keeps one line or
nothing (trimmed, a pair of quotes around it dropped), reads a `file:` URI
through `Uri.LocalPath` and anything else through the home directory and the
environment variables of the platform `FileSystemPath.platformOf` names,
refuses a character the file system says no path may hold, resolves the rest
with `FileSystemPath.resolvedFrom` and drops a trailing separator. Expansion
stops at the platform's own syntax on purpose: `C:\$Recycle.Bin` is a name on
Windows, not a variable, and `%` is an ordinary character on Unix. What
`PastedPath.read` answers is dispatched as `ClipboardPathRead`, and the state
asks `Landing.ofPath` where it leads: `Into` a location, or `Beside` an entry
in one. Nothing at all is passed over without an effect, which is the silence
the plan asked for. `Landing` is the one answer to where a panel goes by name:
`Landing.inside` and `Landing.beside` make one of a `Place` kept in the history
or the favourites, a search's group header answers one, and
`Update.landingOn` is the only caller of `AppState.standInside` and
`standBeside`. The conversions sit in `Landing` rather than in `Place` because
`Place` is compiled first — `History` and `Favorites` are built on it — and
`Landing.ofPath` needs the archive listing, which comes after them. Several lines, blank text and a clipboard with no text
never reach the state. The landing is a change of place like any other, so
`Update.remembering` puts it in the folder history without a line of its own —
a level inside an archive as a `Place.InArchive`, which the history already
knew how to keep.

**A path through an archive is found by walking up to what stands.** Nothing
in the file system answers `C:\work\backup.zip\docs\notes.txt` — the archive is
a file, and nothing below it exists on the disk — so `Landing.ofPath` asks
`FileSystemPath.kind` of the path and, while the answer is `NoEntry`, of its
parent, keeping the names it climbed past. A directory or a file reached with
no names left over is the plain case: entered, or stood beside. A file reached
with names left over is an archive only when `ArchiveReader.kindOf` says the
capability table can read its kind; then the names are an `ArchivePath`, the
level above the last one is read with `ArchiveListing.read` — the cached index,
so a large archive is not walked twice — and the last name is looked up there:
a directory opens the archive at that level, a file opens the level holding it
with the cursor on it. Everything else lands nowhere: a directory reached with
names left over (a path that is not there), a file that is no readable archive,
a name the archive does not hold, a name below a file inside the archive, and
an archive that cannot be read at all. The walk compares names exactly as the
archive stores them. The path to the archive itself is a file like any other,
so it is stood beside, not opened; `Enter` is one key away.

**Files go on the clipboard as the transfer source `F5` would take.**
`Alt+Shift+C` and `Ctrl+Shift+C` raise `FilesOnClipboardRequested ForCopying`,
`Alt+Shift+X` and `Ctrl+Shift+X` the same with `ForMoving`, and the transition
reads `AppState.transferSource` and flattens it into its groups, so a panel of
search results splits into the files on disk and one group per archive exactly
as a copy would. Groups on disk alone become a `ClipboardFiles` — the intent
and a list of paths that cannot be empty — answered as `PutFilesOnClipboard`.
Any entry inside an archive changes the answer: to be moved, it is refused with
`ArchiveEntryNotCut` and the spelling of the first such entry, because a
program pasting a move would delete the unpacked copy, not the entry, and the
user would believe the archive changed; to be copied, the state answers
`ExtractForClipboard` with all the groups.

**What a program outside Marbas pastes must exist on the disk.** `Shell`
answers `ExtractForClipboard` by making a fresh directory with
`Workspace.scratch` in the session's working directory, planning each archive
group into it through the same `planFor` that `F5` uses — so a locked archive
asks for its password and one whose password is not given is left out — and
running `Archive.extractAll` through `Runners.Batch` under the `Unpacking`
title. The scratch directory is new, so there is nothing to ask about
overwriting. The results come back as `ClipboardExtractFinished` with the
groups and the scratch directory; the state finds where each entry that
unpacked landed with `Archive.landedAs`, the same function the archive-to-archive
copy uses, puts the files on disk first and the unpacked copies after them, and
reports what failed item by item beside the files it still hands over. The
scratch directories are not released until Marbas quits, when the whole working
directory goes — a paste in another program may come at any time before that.

**Pasting is `F5` or `F6` with the clipboard as the source and the active
panel as the target.** `Alt+Shift+V` and `Ctrl+Shift+V` raise
`FilesPasteRequested`. The state settles the target first — the directory the
active panel shows, or the level of the archive it is inside, refused by name
when the kind cannot be changed, and refused as `Receiving` over search
results, the same refusal a copy into such a panel meets — and answers
`ReadClipboardFiles` with it, so a refusal never waits on the clipboard.
`Shell` reads the files and dispatches `ClipboardFilesRead` with the target,
or nothing when the clipboard holds no files. Files to be copied become
`CopyPlaces` with one `FromDisk` group, exactly what `F5` sends. Files to be
moved become `MovePastedFiles`, which `Shell` runs through the same `movePiece`
as `MovePlaces` — the overwrite question, the progress window, an archive
target added to and the sources removed — and, once the batch is over, empties
the clipboard when anything moved and the clipboard still holds the same
files. Both conditions matter: a move whose every collision was declined left
the files where they were and worth pasting again, and a clipboard another
program refilled during a long move is not Marbas's to clear. A move pasted
into the other panel's directory takes no target prompt, unlike `F6`: the
place to land in is the panel the user pasted in. The answer to the move is
the ordinary `MoveFinished`, so the panels are read again as after `F6`.

**The intent is read as Explorer writes it.** The Windows adapter takes the
paths with `DragQueryFileW`, which reads both the wide and the narrow
`DROPFILES` layouts, and the intent from `Preferred DropEffect`: the move bit
without the copy bit is a move, and everything else — Explorer's copy writes
copy and link together, and a program that writes no effect at all means a
copy — is a copy. An effect that cannot be read is taken for a copy, the
reading that loses nothing. What the clipboard offers only as virtual files
(see below) has no `CF_HDROP` and is read as no files.

**How the system file manager cuts from an archive, and why Marbas does not
yet.** Explorer offers Cut on an entry inside a zip — its menu there reads
Open, Cut, Copy, Delete, Properties — and a paste elsewhere moves the entry out
of the archive. What follows is the documented protocol of the Windows shell
("Handling Shell Data Transfer Scenarios" in the Win32 documentation); which
formats the zip handler (`zipfldr.dll`) offers in particular was not measured.
Marbas writes plain blocks to the clipboard and is gone from the exchange the
moment they are written. Explorer instead puts a live OLE data object there
(`OleSetClipboard`), which stays in Explorer's process and answers whoever
pastes:

- **Virtual files.** The object need not name a file on the disk. It can offer
  `FileGroupDescriptorW` — the names, sizes and times of the files — and
  `FileContents`, a stream per file that the pasting program reads the data
  from, so nothing is unpacked before the paste and nothing is left behind
  after it. A program that reads only `CF_HDROP` sees no files in such an
  object.
- **Delete on paste.** For a move that is not a rename on one volume, the
  program that pastes copies the data, then calls `SetData` on the object with
  `Paste Succeeded` holding `DROPEFFECT_MOVE` (and `Performed DropEffect`).
  Only then does the source remove the originals — here, Explorer rewrites the
  archive without the entry — and clears the clipboard. An optimized move, a
  rename the target can do alone, needs no answer and leaves the source with
  nothing to do.

That protocol is the answer to three things P18.6 left: cutting from an
archive (the entry is removed by the one who understands the archive, and only
after a paste succeeded), copies unpacked into the working directory ahead of
time (a stream is read when the paste asks for it), and a clipboard that still
names cut files after Explorer moved them (the answer to `Paste Succeeded`
clears it). The price is an `IDataObject` implemented in F# over COM, a thread
in the single-threaded apartment with `OleInitialize` and a message loop —
which the Terminal.Gui loop is not — and Marbas alive until the paste; at quit,
`OleFlushClipboard` keeps the data only by rendering it, which for virtual
files means unpacking them after all. It is the same mechanism read the other
way round in P18.7: files cut in Explorer from inside a zip probably reach the
clipboard as virtual files with no `CF_HDROP`, so a paste that reads only that
format would find nothing there.

To measure it later: invoking the Cut verb through `Shell.Application` from
PowerShell put nothing on the clipboard and failed with `CO_E_NOTINITIALIZED`
once called through the verb object, because the OLE clipboard needs
`OleInitialize` on the calling thread. A small program that initializes OLE,
invokes the verb, and lists the formats of `OleGetClipboard` would show what
the zip handler offers.

**Text is lines, and the line break belongs to the system.** The state answers
`PutOnClipboard` with the names as lines; the Windows adapter joins them with
the system line break and adds none after the last, so a single name pastes into
a field without a trailing newline. A name is never quoted and a directory
carries no separator after it. Where the text goes and what a refusal looks
like is in [State and effects](STATE-AND-EFFECTS.md).
