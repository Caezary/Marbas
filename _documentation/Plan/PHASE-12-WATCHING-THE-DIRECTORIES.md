# Phase 12 — Watching the directories on screen

Goal: what the panels show is what the disk holds. A file written by another
program, a directory removed from a shell, a renamed archive, the whole
directory a panel stands in deleted, a memory stick pulled out — each of them
reaches the screen on its own, without `Ctrl+R`. Only the tab in front is
watched: a tab in the background is already re-read when it is switched to
(P11.4), so a watch on it would pay for a listing nobody is looking at. The
system tells us most of this through `FileSystemWatcher`, which
`System.IO.Abstractions` hands over as `fs.FileSystemWatcher.New`; the rest —
the watched directory dying under the watcher, and the drive under the
directory — it does not say, and a poll has to ask.

- **P12.1 The re-read and the message that asks for it** — `ExternalChangeDetected`
  answers with both panels of the tab in front read again, each cursor kept on
  the entry it stood on and the marks that survived, which is `readTabAgain`
  already. It differs from `RefreshRequested` in two ways, and that is why it is
  a message of its own rather than a second sender for that one: `Ctrl+R`
  refreshes the active panel and this refreshes the pair, and a failed listing
  here raises no dialog, because a change the user did not ask for must not
  answer with a popup. A listing that fails leaves the panel as it was and waits
  for P12.4 to place it. *Done when:* a directory changed underneath a
  `MockFileSystem` shows its new entries after the message, with the cursor and
  the surviving marks where they were, and a vanished directory produces no
  effect.
- **P12.2 The locations under watch** — `WatchLocations` is an effect carrying
  the paths the tab in front stands on, and the update raises it wherever that
  pair can change: at startup, on navigation into and out of a directory, on a
  root, on a path opened, and on a tab opened, closed, switched or cycled. A
  panel standing inside an archive contributes the archive file, not the level
  inside it, since the file is what the operating system can report on. Both
  panels in one directory contribute it once. *Done when:* every transition that
  moves a panel is proved to answer with the effect, and the paths in it are the
  ones on screen.
- **P12.3 The adapter and the burst** — `Adapters/DirectoryWatch.fs` holds the
  only `FileSystemWatcher` in the app: one per path, `IncludeSubdirectories`
  off, `NotifyFilters.FileName ||| DirectoryName ||| LastWrite ||| Size |||
  Attributes`, because a panel shows a size and a date as well as a name. A new
  `WatchLocations` disposes the watchers of the previous pair before it creates
  the next. Its events arrive on a thread-pool thread and reach the loop through
  `app.Invoke`, as the batch runner's progress already does; one copy raises
  hundreds of them, so the first arming a timeout and the rest only pushing it
  along collapses a burst into a single re-read. *Done when:* a file created,
  deleted or renamed by another process appears or goes within the debounce
  interval, and a directory copied into a watched one costs one listing rather
  than one per file.
- **P12.4 What the watcher cannot say** — three things it does not report, all
  arriving as `Error` or as nothing at all: the watched directory deleted
  underneath it, the buffer overflowing under heavy traffic, and the drive
  removed, which no console application can hear, since the device notification
  the system offers wants a window handle. `Error` is therefore not an error to
  show but a signal to read again, and a poll on a timeout is the net beneath
  it, asking only whether the paths on screen still exist. A panel whose
  location is gone walks up to the nearest ancestor that is still there rather
  than reporting the loss, because it stands somewhere now and the user did
  nothing to move it. *Done when:* deleting the directory a panel stands in from
  a shell leaves that panel in its parent, and pulling out the drive a panel
  stands on leaves it on a root that exists, with no dialog either time.
- **P12.5 Quiet while something is in front** — Terminal.Gui runs a dialog in a
  nested loop, so a timeout keeps firing while a prompt is open, and a re-read
  underneath one would move the cursor the prompt is about to act on. Delivery
  is therefore held while a modal is up and while a batch runs, and what was
  held becomes one re-read when the screen is the panels' own again. The
  operations that already finish with a listing of their own keep it; a held
  change is not a second one. *Done when:* a change made while `F5` is asking
  for its target does not move the entry under the cursor, and the panels show
  it once the dialog closes.
- **P12.6 The platforms and the documentation** — `Architecture/AUTO-REFRESH.md`
  says what is watched, what is not, why the errors are read as a request to
  look again, and why the tabs in the background are left alone;
  [Cross-platform](../Architecture/CROSS-PLATFORM.md) gains what the two systems
  do differently — `ReadDirectoryChangesW` against `inotify`, the per-user watch
  limit on Linux, and the network shares and file systems that report nothing,
  which is the other reason the poll of P12.4 exists. *Done when:* the phase is
  described where the rest of the design is, and the manual test is repeated on
  both systems.
