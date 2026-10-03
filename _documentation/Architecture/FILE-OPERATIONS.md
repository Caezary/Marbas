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
`Trash.location` picks `$XDG_DATA_HOME/Trash` or falls back to
`$HOME/.local/share/Trash`, mirroring `Settings.location`.

**A session gets a directory to work in, and takes it with it when it goes.**
Some operations need somewhere to put a file that belongs to neither panel — an
entry on its way from one archive to another, above all. `Workspace.location`
answers `<temporary root>\Marbas\<session>`, where the root is the platform's
own (`%TEMP%`, `$TMPDIR`, `/tmp`, read once into `HostEnvironment`) and the
session is a fresh id per run, so two Marbas processes never share a directory.
`Workspace.ensure` makes it the first time something asks — answering an
ordinary `OperationError` if it cannot, never an exception — and
`Workspace.release` removes it whole.

**The workspace is a path on `Dependencies`, not a port**, the same choice
`SettingsPath` made and for the same reason: `Workspace` is ordinary
`IFileSystem` code, so it runs against `MockFileSystem` in the tests, while an
opaque function port would have left it untestable. `Shell.interpret` releases
it on `Quit`, beside the settings write, which is what makes "gone after the
application quits" a test rather than a claim. The one way out that skips it is
`Ctrl+Q` pressed while a dialog holds focus, which does not pass through
`Update` at all — the same hole that already loses the session file; the
operating system sweeps its own temporary directory eventually.

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
`ReportItemFailures` effect renders them through
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
into `ReportOperationError`. An empty list is a directory with nothing in it,
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

**Formatting stays pure, and so does the scrolling.** `HexDump.row` renders one
row at an absolute offset, `PrintableText.describe` turns tabs and control
characters into something a terminal can draw, `LineWrap` cuts a line into the
rows it takes or the window shown of it, `ViewerScroll` clamps a move and
measures how many lines fill a page, and `ViewerHeader.describe` lays the top
bar out to a width. `FileViewer.ContentView` holds the mutable place — mode,
wrap, top row, left column — and exposes `Rows()`, the very list it draws, so
a test asserts on what the screen would show without a driver painting one.

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
`Processes.RunEditor` takes the path and answers
`Result<unit, Operations.OperationError>`; `EditorProcess.run` is the only
place that knows about `Process.Start`, `WaitForExit` and repainting the
screen afterwards, and it maps whatever the start threw through
`Operations.mapException`. That reuses the error type, the
`ReportOperationError` effect and `OperationErrorMessage` rather than inventing
a second failure vocabulary for one case — an editor that will not start failed
on a path, like every other operation error. The tests pass a `RunEditor` that
records the path and can write to the mock file system, which is what makes
"the panel shows what the editor left behind" an ordinary test.

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

**A finished edit re-reads the panel it started from.** `EditFinished Ok` runs
the same `AppState.refresh` that `Ctrl+R` does, so the cursor stays on the file
and the marks survive; only the active panel is re-read, because that is the
directory the editor was pointed into.

**Running a program is a second port, and its failures are their own
vocabulary.** `Processes.Launch` takes a `LaunchRequest` and answers
`Result<unit, LaunchError>`. It is not `RunEditor` and it does not reuse
`OperationError`, because nothing here is an operation on a file: the four ways
it fails — nothing associated with the name, the named program missing, the
system refusing, the elevation prompt turned down — say something about
*starting a program*, and three of them have no path-shaped answer at all. The
effect is `RunProgram`, the message back is `LaunchFinished`, and the wording
lives in `LaunchErrorMessage` beside every other message module.

**The launcher never waits, and that is the whole point of it.** `EditorProcess`
blocks on `WaitForExit` because `F4` promises the panel is re-read when the
editor closes; `LaunchProcess` promises nothing of the kind, so it starts the
program and returns. Nothing re-reads a panel afterwards either — there is no
"afterwards" to hook. On Windows every launch goes through the shell verb
(`UseShellExecute`), which is what gives a console program a console of its own
instead of letting it write over the panels, and `runas` is the same call with
one field set. On Unix there is no shell verb: an executable entry is started
directly with both its streams redirected and drained into `Stream.Null` — the
terminal belongs to the panels — and anything else is handed to `xdg-open`.
Which of the two an entry is comes from `EntryAttributes.executability` — the execute
bit and nothing else, because a name that looks like a Windows program is not
one a Linux kernel will run.

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

**Text is lines, and the line break belongs to the system.** The state answers
`PutOnClipboard` with the names as lines; the Windows adapter joins them with
the system line break and adds none after the last, so a single name pastes into
a field without a trailing newline. A name is never quoted and a directory
carries no separator after it. Where the text goes and what a refusal looks
like is in [State and effects](STATE-AND-EFFECTS.md).
