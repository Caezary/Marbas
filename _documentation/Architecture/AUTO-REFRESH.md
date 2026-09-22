# Watching the directories on screen

Phase 12. What the panels show is what the disk holds: a file written by another
program, a directory removed from a shell, the whole directory a panel stands in
deleted, a memory stick pulled out — each of them reaches the screen on its own,
without `Ctrl+R`.

**The loop does not change shape: a watch is an effect and a change is a
message.** `ExternalChangeDetected` answers with the watched panels of the tab in
front read again — `AppState.readTabQuietly`, which is `refresh` over each of
them with the cursor kept and the marks that survived — and `WatchLocations`
carries the paths the adapter should be watching. A panel `PanelLocation.watched`
answers nothing for is left alone, which is one panel in practice: a results
panel, whose hits are checked when it is shown and when the user asks, never
because something happened in the directory beside it. Neither the state nor `Update` knows that
a `FileSystemWatcher` exists; `Shell.Dependencies.WatchLocations` is the port,
`Adapters/DirectoryWatch.fs` the only implementation, and the callback it is
handed dispatches that one message.

**The effect is raised by comparing, not by remembering to raise it.** `update`
is `transition` — the whole former body — followed by `watching`, which asks
`AppState.watchedLocations` of the state before and after and appends
`WatchLocations` when the pair moved. Listing the transitions that move a panel
would have meant seven arms to keep in step, and `EntryOpened` alone moves a
panel four different ways. The comparison also buys the silence: a cursor, a
sort, a copy and `ExternalChangeDetected` itself answer with no watch at all,
because the panels are standing where they stood. A tab *opened* is the case
that surprises: the new tab is a clone of the one it was opened from, so the
pair on screen has not moved and nothing is re-armed.

**A panel inside an archive is watched by the archive file**, since that is what
the operating system can report on, and `PanelLocation.watched` is where that is
decided. The adapter then has one translation of its own to make: a
`FileSystemWatcher` only ever opens a *directory*, so `DirectoryWatch.target`
turns a file into the directory holding it plus a filter on the name —
`EveryEntryIn` or `OneEntryIn`. A path that is not there takes the same arm,
which is how its return is noticed.

**The tabs in the background are left alone.** Switching to a tab already
re-reads both of its panels (P11.4), so a watch on one would pay for a listing
nobody is looking at. Both panels standing in one directory contribute it once.

**One copy is one listing.** A single `cp` of two hundred files raises hundreds
of events, so `DirectoryWatch.collapsing` arms a timeout on the first and lets
the rest only push its deadline along; the tick keeps answering "not yet" until
two hundred milliseconds of quiet have passed, and then reports once. Measured
on 2026-09-11: two hundred files written in one burst cost one listing on Linux
and one or two on Windows, depending on whether the burst outran the window.
Events arrive on a thread-pool thread and reach the loop through `app.Invoke`,
the way the batch runner's progress already does.

**An error is not something to show but a reason to look again.** Three things
the watcher cannot tell us: the directory it watches deleted underneath it, its
internal buffer overflowing under heavy traffic, and the drive removed, which no
console application can hear at all — the device notification Windows offers
wants a window handle. The first two arrive as `Error` and leave the watcher
dead, so the handler raises a flag rather than a dialog, and a two-second poll is
the net beneath all three: it asks only whether the watched paths still stand,
and when the answer changed — or the flag is up — it opens the watchers again and
asks for one re-read.

**A panel whose place is gone stands somewhere rather than reporting a loss.**
`FileSystemPath.nearest` walks up to the first directory that is still there, and
`Drives.list` supplies a root when the whole chain has gone with its drive. The
user did nothing to move that panel, so a popup would be an answer to a question
nobody asked; the panel simply stands in the parent, or on `C:\`, with the cursor
at the top and no marks, because it is a different place. `Ctrl+R` keeps the old
behaviour and *does* report: there the user asked, and an answer is owed.

**Nothing is re-read under a dialog.** Terminal.Gui runs a popup in a nested
loop, so timeouts keep firing while one is up, and a re-read underneath would
move the listing the prompt is about to act on. Holding and collapsing turn out
to be the same mechanism: the quiet tick also answers "not yet" while
`app.SessionStack` is deeper than the main window, so whatever happened during a
prompt, the viewer or a batch's progress window becomes a single re-read once the
screen is the panels' own again. An operation that finishes with a listing of its
own keeps it, and the held re-read that follows is one more listing, which is
idempotent and cheap.

## What it costs, and what it does not cover

- The poll asks `Directory.Exists` for at most two paths every two seconds. It
  notices a path that went or came back; it does **not** read the directory. On
  a file system where the watcher reports nothing at all — an SMB or NFS share
  is the usual one — entries added or removed inside the directory are therefore
  still invisible until `Ctrl+R`.
- A removed drive takes up to the poll interval to show, not the debounce
  interval.
- Nothing is watched recursively: `IncludeSubdirectories` is off, because a
  panel shows one level.
- Marks are dropped when a panel is walked out of a vanished directory. They
  belong to the place they were made in.

## How it is tested

`MockFileSystem` has no `FileSystemWatcher` — its factory throws and says to
supply your own — so the adapter keeps the operating system at its edge and
everything above it is ordinary tested code: `collapsing` takes a clock, a
scheduler and a "may I report" predicate, `target` and `standing` take an
`IFileSystem`, and the state and effect sides are unit tests like any other
transition. What no test can reach was measured with a probe instead, on both
systems, on 2026-09-11 — see
[Cross-platform](CROSS-PLATFORM.md), *Watching the directories on screen*.
