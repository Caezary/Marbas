# File operations

**Operation targets are one rule, shared by every operation key.**
`AppState.operationTargets` answers with the marked entries in listing order,
or with the cursor entry when nothing is marked, and never with `..` — so a
function key pressed on the parent entry has nothing to do and asks nothing.
`F5`, `F6` and `F8` all take their sources from it.

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
carries a `DeleteRequest` — the target names plus a `DeleteScope` saying
whether the delete stops at those entries (`NamedEntriesOnly`) or reaches
inside a directory as well (`WithDirectoryContents`) — and
`DeleteConfirmation.describe` turns that into the question. `AppState.deleteScope`
decides it in the state layer, where the file system already is
(`Operations.isNonEmptyDirectory`); a directory that cannot be enumerated
counts as non-empty, so the warning errs towards showing up.

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

**Reading a file answers what it is, not what it looks like.**
`FileContent.read` hands back a `Content` — a `ContentBody` that is either
`Text` lines or the raw `Binary` bytes, plus a `ContentExtent` saying whether
the file fits in what was read. Which of the two it is comes from one rule: a
zero byte anywhere in what was read means binary, because text encodings do not
carry one. Turning that into rows on a screen is Ui's job — `HexDump.describe`
formats bytes as offset / hex / text, `FileView.lines` picks between the two
bodies and appends the truncation notice. `FileSystem` therefore stays free of
formatting and the hex layout can be tested without a terminal.

**The viewer reads at most a megabyte.** A file manager is pointed at whatever
is under the cursor, disk images included, so `read` opens a stream, takes
`FileContent.MaxBytes` at most and answers `Truncated` when there was more.
That keeps `F3` from pulling gigabytes into memory on the UI thread. The
notice on the last line is what tells the user they are not seeing everything.

**Closing the viewer is injected, because `RequestStop` needs a running app.**
`Runnable.RequestStop` does nothing unless the dialog is being run by an
application, so a test cannot observe it. `FileViewer.create` therefore takes
the closing action as its first argument — `Program.fs` passes
`_.RequestStop()`, the tests pass a recorder — which leaves "F3 and Esc close
the viewer, other keys do not" as an ordinary assertion. The key handler sits
on the `TextView` because that is the view holding focus, and it marks the key
handled so the `F3` shortcut on the main window's status bar cannot reopen a
viewer on top of the one being closed.

**The editor is a port, and its failure is an operation failure.**
`Dependencies.RunEditor` takes the path and answers
`Result<unit, Operations.OperationError>`; `Program.fs` is the only place that
knows about `Process.Start`, `WaitForExit` and repainting the screen
afterwards, and it maps whatever the start threw through
`Operations.mapException`. That reuses the error type, the
`ReportOperationError` effect and `OperationErrorMessage` rather than inventing
a second failure vocabulary for one case — an editor that will not start failed
on a path, like every other operation error. The tests pass a `RunEditor` that
records the path and can write to the mock file system, which is what makes
"the panel shows what the editor left behind" an ordinary test.

**Which editor to start is a pure choice.** `EditorCommand.choose` takes
`$EDITOR` as an option and a `Platform` (`Windows`/`Unix`) and answers with the
command: what was configured, trimmed, or `notepad`/`vi`. `Program.fs` only
reads the environment and asks. A blank `$EDITOR` counts as unset.

**A finished edit re-reads the panel it started from.** `EditFinished Ok` runs
the same `AppState.refresh` that `Ctrl+R` does, so the cursor stays on the file
and the marks survive; only the active panel is re-read, because that is the
directory the editor was pointed into.
