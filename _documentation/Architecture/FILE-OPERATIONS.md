# File operations

**Operation targets are one rule, shared by every operation key.**
`AppState.operationTargets` answers with the marked entries in listing order,
or with the cursor entry when nothing is marked, and never with `..` — so a
function key pressed on the parent entry has nothing to do and asks nothing.
`F5`, `F6`, `F8` and `Shift+F1` all take their sources from it.

**Operations answer per item, and one bad item never stops the batch.**
`Operations` mirrors `DirectoryListing`: every function returns
`Result<unit, OperationError>` instead of throwing, and the `*All` functions map
a list of source paths to `ItemResult` values (entry name plus outcome) in the
input order, so the UI can report exactly which entries failed and why. A
single-item failure inside one *tree* does abort that tree — a half-copied
directory stops at the first error rather than pressing on blindly.

**Every question is answered before the batch starts.** `Operations.approve`
walks the sources, asks Yes / No / Yes to all / No to all / Cancel about each
one whose target already exists, and answers with the sources to actually work
on — `No` drops one, `Cancel` drops the lot, since nothing has happened yet.
The two *to all* answers become a `StandingAnswer` that stands in for the
question from then on, so a later collision is settled without asking while a
source that collides with nothing is still taken. What runs
afterwards is decision-free: `copy` and `move` carry no overwrite flag any more
and always replace, because whoever called them has already decided. The only
refusal left lives in `rename`, which checks its own target. (P5.1 had the
flag, P5.4 had the batch asking as it went; P5.6 made both untenable, because a
background thread cannot open a dialog and wait on it.)

**One question per item covers the whole tree.** `approve` asks only when the
*top-level* target already exists — and if it does not, nothing inside it can
exist either, so no conflict can appear deeper in the recursion. That is what
makes asking up front complete rather than merely convenient: one answer
settles the item as a whole, every file inside it included, and a refusal stops
it before anything is written. Sources that were dropped never appear in the
results, which is what keeps their marks in place.

**Copy and move are the same batch with a different verb.** `copyAll` and
`moveAll` are two lines over a shared private `allWatched`, which owns the walk
and the cancellation check; only the operation applied to each source differs.
A rename is not part of it: it touches one entry, so it stays a plain `Result`
carried by `RenameFinished` instead of a one-element batch.

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

**Moving a directory is copy-then-delete.** `IDirectory.Move` fails across
volumes, which is the normal case for a two-panel manager, so directories are
copied and then removed; files still use `IFile.Move`.

**Marks survive exactly what did not finish.** After a batch, the panel keeps
the marks of the entries whose `ItemResult` is not `Ok` (and that still exist);
everything that succeeded loses its mark. Delete, copy and move share the rule,
so a failed batch can be retried by pressing the key again.

**A confirmation is an effect; its wording belongs to Ui.** `ConfirmDelete`
carries a `DeleteRequest` — the target names, a `DeleteScope` saying
whether the delete stops at those entries (`NamedEntriesOnly`) or reaches
inside a directory as well (`WithDirectoryContents`), and the `DeleteMode`
asked for — and `DeleteConfirmation.describe` turns that into the question,
`DeleteConfirmation.title` into the title above it. `AppState.deleteScope`
decides the scope in the state layer, where the file system already is
(`Operations.isNonEmptyDirectory`); a directory that cannot be enumerated
counts as non-empty, so the warning errs towards showing up.

**Deleting has two meanings, and the key says which.** `DeleteMode` is
`ToTrash` or `Permanently`: `F8` and `Del` ask for the first, `Shift+F8` and
`Shift+Del` for the second. The mode rides the whole way through —
`DeleteRequested`, the `DeleteRequest` being confirmed, `DeleteConfirmed`, and
the `DeleteTargets` effect — so nothing has to remember which key started it,
and the question the user answers is the question that gets carried out: *Move
'docs' to the trash?* under **Delete**, or *Delete 'docs' for good?* under
**Delete for good**.

**The trash is a port; a permanent delete is not.** `Shell.interpret` picks
`deps.SendToTrash` for `ToTrash` and `Operations.delete` for `Permanently`, and
hands either to `Operations.removeAll`, which is `deleteAll` with the removal
left open. What the port does is the platform's business and
`Dependencies.create` decides it — the Windows recycle bin through
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
`IFileSystem` — the move is `Operations.moveTo`, which copies and deletes a
directory rather than renaming it across volumes — the whole thing is tested on
`MockFileSystem`, on both platforms, without a real trash anywhere.
`Trash.location` picks `$XDG_DATA_HOME/Trash` or falls back to
`$HOME/.local/share/Trash`, mirroring `Settings.location`.

**Delete is an effect now, like copy and move.** It used to happen inside the
`AppState` transition for `DeleteConfirmed`, which left no room for a port that
the state layer must not know about. The walk is now `DeleteRequested mode` →
`ConfirmDelete` → `DeleteConfirmed mode` → `DeleteTargets` → the removal in
`Shell` → `DeleteFinished results` → `AppState.finishDelete`, which only
re-reads the active panel and keeps the marks of what did not finish. It still
runs on the UI thread rather than behind `RunBatch`: `Directory.Delete` and the
recycle bin report no progress, so there would be nothing to show.

**Per-item failures share one dialog.** `Operations.deleteAll` answers for every
target, `Operations.failures` keeps the ones that went wrong, and the
`ReportItemFailures` effect renders them through
`OperationErrorMessage.describeFailures` as one `name: reason` line each. The
panel is refreshed first, so what survived a partial failure is on screen
before the dialog appears.

## Viewing and editing

**`F3` and `F4` take the cursor entry, not the operation targets.** Copy, move
and delete work on `operationTargets` — the marks, or the cursor when nothing
is marked. Viewing and editing are a different act: each opens one file, so
both read `AppState.cursorFilePath`, which also filters out directories and
`..`. There is nothing to view or edit for a directory, so nothing happens.

**The viewer never reads a file whole.** `F3` opens a file, it does not load
one. `FileWindow.openFile` keeps the stream open behind a `Source` that answers
one byte range at a time, and the only thing read up front is a probe of
`TextEncoding.ProbeLength` bytes, which settles the encoding. Everything after
that is pulled as it is needed: the viewer asks for the rows its viewport shows
and nothing else, so a gigabyte opens as fast as a kilobyte and there is no size
limit and no truncation notice to write.

**Where a line begins is scanned forward, never indexed up front.**
`LineIndex.Index` holds the byte offset of every line start it has seen and
scans on in `ChunkLength` steps only when asked for a line it has not reached.
Opening a file of a hundred thousand lines therefore indexes one chunk, not the
file. `ScanAll` is the one call that reads to the end, and only `End` makes it.
The scan works in the encoding's own code units, so a line feed in UTF-16 is
`0A 00` and not a stray byte, and the byte order mark is skipped as a preamble
rather than shown as content.

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
`Dependencies.RunEditor` takes the path and answers
`Result<unit, Operations.OperationError>`; `EditorProcess.run` is the only
place that knows about `Process.Start`, `WaitForExit` and repainting the
screen afterwards, and it maps whatever the start threw through
`Operations.mapException`. That reuses the error type, the
`ReportOperationError` effect and `OperationErrorMessage` rather than inventing
a second failure vocabulary for one case — an editor that will not start failed
on a path, like every other operation error. The tests pass a `RunEditor` that
records the path and can write to the mock file system, which is what makes
"the panel shows what the editor left behind" an ordinary test.

**Which editor to start is a pure choice.** `EditorCommand.choose` takes
`$EDITOR` as an option and a `Platform` (`Windows`/`Unix`) and answers with the
command: what was configured, trimmed, or `notepad`/`vi`. `Adapters` only
reads the environment and asks. A blank `$EDITOR` counts as unset.

**A finished edit re-reads the panel it started from.** `EditFinished Ok` runs
the same `AppState.refresh` that `Ctrl+R` does, so the cursor stays on the file
and the marks survive; only the active panel is re-read, because that is the
directory the editor was pointed into.
