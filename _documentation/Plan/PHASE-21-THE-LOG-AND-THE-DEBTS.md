# Phase 21 — The log and the debts

Goal: when Marbas fails, it leaves behind enough to say why, and the code is
left easier to change than it was found. The phase adds one feature — a log,
written on a crash and, in debug mode, on every transition — and otherwise
pays off what earlier phases left owing: a progress window that is flooded on a
fast batch, a directory that can be copied into itself, a console editor on Unix that has to share the terminal, a
window program on Windows that writes over the panels, working
directories left in the temporary directory by every session that did not end
with `F10`, a Windows clipboard opened without a window and so not held while
it is written, a flag
argument, four ways of landing a panel that say the same thing, the effect
interpreter sitting in the wrong layer, ports whose types
say how they are called but not what they are for, mutation that nothing
demands, test files too long to find a test in, and the manual checks no
person has yet run.

The phase was opened by a crash. `Esc` during a large `F5` closed the progress
window while reports for it were still queued on the UI thread, the first of
them asked the disposed dialog for its line, and the exception ended the
application with nothing to show for it. That bug was fixed before the phase
(described in [State and effects](../Architecture/STATE-AND-EFFECTS.md)); what
it exposed was that a crash told nobody anything, and the first stage answers
that.

Three rules decide the order. Diagnostics come first, because every task after
them is safer with a log to read. The fixes a user can see come next. The
structural work runs from the lowest layer up and from the smallest change to
the largest, and the test files are divided last, once the modules they test
have stopped moving. The manual pass closes the phase, because it should be
run against the binaries the phase ends with.

Five decisions run through the first stage.

**The log is a port.** `Dependencies` gains a log that takes a `LogEntry`, a
union of what is worth recording: a message handled, an effect interpreted, a
batch started, finished or cancelled with its counts, an error reported to the
user, and a crash. Only `Adapters` writes the file. `Update` stays pure:
`Runtime` is handed an observer it calls with each message it drains, so the
host that owns the loop is the one place that sees every transition.

**A crash is always written, debug or not.** The loop's own error handler
(`app.Run(window, errorHandler)`), `AppDomain.UnhandledException` and
`TaskScheduler.UnobservedTaskException` all write the exception with its stack
trace. The application still ends — a loop that has thrown is in a state no
test has seen — but once the terminal is restored the console says, in one
line, where the log is.

**Debug mode is asked for, not configured.** `--debug` on the command line
turns on the transition log for that session; nothing is saved, so a forgotten
switch cannot fill a disk. `Program.fs` hands its arguments to the adapter that
reads the environment, and still only starts the application.

**Names, not contents.** A message or an effect is logged by its case name and
the panel it concerns; paths are written only for errors and batch items. No
password, no file content and no clipboard text is ever written, and a test
holds every case that carries an `ArchivePassword` to that.

**The log lives beside the settings and stays small.** One file, `marbas.log`,
next to the settings file, each line stamped with a `DateTimeOffset`. When a
session starts the file is cut to its last megabyte, so it holds the recent
past and never grows without bound.

## Stage 1 — Seeing what went wrong

- **P21.1 A crash leaves a log** — the log port, its file adapter and the
  three crash hooks; the one-line pointer printed after the terminal is
  restored. *Done when:* `LogEntry` is formatted and the file is cut to its
  last megabyte on `MockFileSystem`, a probe whose loop throws leaves the stack
  trace in the file and the pointer on the console, and an exception thrown on
  a background task is written too.
- **P21.2 Debug mode** — `--debug` turns on the observer in `Runtime`, the
  effect log in `Shell.interpret` and the batch entries in `BatchRunner`, with
  the redaction the decision above describes. *Done when:* a message, an
  effect and a cancelled batch each leave their line in a test with a fake log,
  no line is written without the switch, and no case carrying a password writes
  it.
- **P21.3 The progress window shows the latest path** — a batch's report no
  longer queues an `Invoke` per file: it overwrites one cell, and a timer on the
  UI thread shows what the cell holds every 100 ms, the cadence `SearchRunner`
  gathers at. The timer is removed when the window closes, so a report can no
  longer outlive its dialog by construction rather than by the second check that
  guards it today. *Done when:* a report is proved to queue nothing, a tick to
  show the latest path and a tick with nothing new to show nothing, and a batch
  of many small files is timed before and after in
  [Measurements](../Progress/MEASUREMENTS.md).

## Stage 2 — A copy kept out of itself

Nothing stops a directory from being copied or moved into itself. Reading
`Operations.copying` while P18.7 was written showed why that is worse than a
refusal from the system: a directory's children are listed only when the walk
reaches it, so a directory copied into its own subdirectory meets its fresh
copy among them and copies it again, one level deeper each time, until a path
grows too long. A move that cannot be a rename goes through the same copy
before it deletes. `F5` and `F6` reach it whenever the other panel stands
below the source, a typed `F6` target reaches it anywhere, and since P18.7 a
paste reaches it in three keys — `Alt+Shift+C` on a directory, `Enter`,
`Alt+Shift+V`. Packing already has the guard, `Archive.wouldPackItself`; the
transfers do not.

- **P21.4 A directory is not copied into itself** — a test on
  `MockFileSystem` first shows the walk running away, bounded so the suite
  cannot hang. Then every transfer from the disk is refused before its batch
  when the place it lands in is at or under one of its sources —
  `FileSystemPath.atOrUnder` on the platform's rules, the comparison
  `wouldPackItself` makes: `F5` and `F6` into the other panel, an `F6` target
  typed into the prompt, a paste of copied or cut files, and an archive target
  whose file lies under a source directory, which would be read while it is
  written. The check is one function of `Operations` beside `approve`, which
  `wouldPackItself` then calls too, and the refusal names the target the way
  `TargetWithinSource` does today; its sentence stops speaking of archives
  alone. A source that only reaches the target through a link is not followed:
  the comparison is of paths, as packing's is. *Done when:* the runaway is
  shown and then refused for a copy, a move, a typed target and a paste, each
  by name and before any batch starts; a target beside the source, and one in
  a directory whose name merely begins with the source's, still copy; and the
  entry in [Rough edges](../ROUGH-EDGES.md) is gone.

## Stage 3 — The terminal handed over

The panels are drawn in a terminal that the programs Marbas starts can reach.
On Unix a console editor started by `F4` shares it with a driver that is still
reading keys. On Windows a program started through the shell verb is given a
console of its own only when it is a console program; a window program is given
none, and an Electron program such as `Code.exe` then attaches to the console of
its parent — Marbas's, the terminal the panels are drawn in — and writes its log
there. Terminal.Gui knows nothing of it, so the text stays until those cells are
drawn again. It was seen with a user menu entry `code.exe %p`, and `Enter` or
`F4` reach the same program the same way. The shell verb cannot redirect the
streams, and starting every program without it would take the window of its
own away from a console program, so the two kinds have to be told apart.

- **P21.5 A console editor gets the terminal** — on Unix, `F4` shuts the
  Terminal.Gui driver down before starting the editor and brings it back after
  it exits, so a full-screen editor receives every key and the panels are
  redrawn whole; Windows keeps today's path behind a switch. A spike first
  proves that the driver can be taken down and brought back inside one
  `IApplication`. *Done when:* the switch is proved per platform, a full-screen
  editor round-trips by hand on Linux, and the entry in
  [Rough edges](../ROUGH-EDGES.md) is gone.
- **P21.6 A window program keeps off the panels** — on Windows, a program
  Marbas starts by name is first resolved to its file — the path as written,
  else the `PATH` with the extensions of `PATHEXT`, else the system's App
  Paths — and the subsystem in its PE header decides how it is started. A
  window program is started without the shell verb, its streams redirected and
  drained as `LaunchProcess.detached` does on Unix, so it holds valid standard
  handles and has no reason to reach for its parent's console. A console
  program, a script (`.cmd`, `.bat`), a file that cannot be resolved or read,
  and every run as administrator — `runas` exists only as a shell verb — go
  through the shell as today. Reading the subsystem is a function of
  `FileSystem` over `IFileSystem` that answers a union rather than a number;
  resolving the name, `PATH` and App Paths included, stays in `Adapters`. The
  rule covers every start that names its program: a user menu entry run
  `Directly`, `Enter` on an executable or with a program chosen for the
  extension, and `F4`. A start through a file's association, where the system
  picks the program, cannot be redirected and keeps the shell verb. A spike
  first proves by hand that `Code.exe` started with redirected streams writes
  nothing to the terminal and that a console program started through the shell
  verb still opens a window of its own. *Done when:* the subsystem is read on
  `MockFileSystem` from a window program's header, a console program's, a file
  too short to hold one and a file that is not an executable; each start is
  described with the verb or the redirection its kind calls for; the user menu
  entry `code.exe %p` opens VS Code by hand with nothing written over the
  panels; and the entry in [Rough edges](../ROUGH-EDGES.md) is narrowed to a
  start through an association.

## Stage 4 — What a session leaves behind

Each session works in `<temporary root>\Marbas\<session>` — entries on their way
from one archive to another and, since P18.6, the entries unpacked for the
clipboard — and only the `Quit` effect removes it. A console window closed with
its button, a logoff or a shutdown, a killed or crashed process and `Ctrl+Q`
inside a dialog all skip that effect, the session id is a fresh GUID each run,
and nothing ever looks at the directories left behind, so they stay until the
system cleans its temporary directory, if it ever does. Releasing on the
console's close and shutdown signals as well would narrow the hole but not
close it — a kill or a crash still skips every handler — so the sweep at start
is the fix, and a signal handler is left out.

- **P21.7 Old sessions are swept at start** — every session writes an owner
  file into its working directory when it makes it: its process id and the
  moment the process started, which together name one process even after the
  system reuses the id. When a session starts, the directories under
  `<temporary root>\Marbas` other than its own are read, and one whose owner is
  not a running process started at that moment is removed whole; asking whether
  it runs is a port on `Processes`, filled from `System.Diagnostics.Process` in
  `Adapters`. A directory with no owner file — left by a version from before
  this task — is removed once nothing in it has been written for a day, so a
  session of an older Marbas still running beside a new one keeps its files.
  The sweep runs off the UI thread so a large leftover cannot delay the first
  screen, and a directory it cannot remove — a file in it held open — is
  skipped and written to the log, not shown: the user did nothing to cause it.
  Only directories directly under `Marbas` are touched, never anything beside
  it. *Done when:* on `MockFileSystem` a dead session is removed and so is one
  whose process id now belongs to a process started at another moment, a live
  session and the session's own are kept, an ownerless directory is removed
  after a day and kept before it, a directory that refuses deletion leaves a
  log line and no dialog, on Windows a session whose console was closed with
  the window's button is gone after the next start, and the entry in
  [Rough edges](../ROUGH-EDGES.md) is gone.

## Stage 5 — The clipboard held while it is written

`SystemClipboard` opens the clipboard with `OpenClipboard(0n)`, no window of
its own, and then empties it and hands over one block after another before it
closes. While the tests of the phase 18 appendix were written, a process that
had opened the clipboard that way did not keep a second process from opening it
too, while one that opened it with a window handle did, and the second was
answered access denied. If that holds, another program can open the clipboard
between Marbas's `EmptyClipboard` and its last `SetClipboardData` — emptying it,
or writing a block of its own beside half of Marbas's — and a read can see a
list of files whose `Preferred DropEffect` was written by someone else. The
retries of `opened` guard against a clipboard held by another program, not
against one that is not held at all. It was seen once, in a quick measurement,
so the task proves it before it fixes it.

- **P21.8 The clipboard opened with a window of Marbas's own** — first, an
  integration test in `Integration/Clipboard/` proves the gap as the system
  answers it: a process holding the clipboard opened with no window keeps no
  other process out. Then the Windows adapter opens the clipboard with a
  message-only window (`HWND_MESSAGE`) it creates once and keeps for the
  session, so the clipboard is held while every request of `whileOpen` runs,
  and Marbas becomes the owner `EmptyClipboard` records. A spike settles first
  which thread may create the window and open the clipboard with it, since the
  requests reach the adapter from the UI thread and from batches alike, and
  whether the blocks handed over stay on the clipboard after the window is
  destroyed at exit. *Done when:* while the adapter holds the clipboard open,
  another program's `OpenClipboard` is refused — through a seam that lets a test
  hold the adapter's open clipboard — every test of `WindowsClipboardTests`
  still passes, text and files written by Marbas survive Marbas quitting and
  paste into another program, and
  [Testing](../Architecture/TESTING.md) no longer has to explain why the holder
  of the tests opens with a window.

## Stage 6 — The structure

- **P21.9 Replacing is said by name** — `Operations.copyFile` and `moveFile`
  lose their `bool`: copying always replaces, so its argument goes, and moving
  takes a union that says whether an existing target is replaced (every
  transfer) or kept (the anonymous rename a wipe makes). *Done when:* no
  function in `Operations` takes a `bool` for what it does, and the suite
  passes unchanged.
- **P21.10 One way to land a panel** — four transitions end in
  `AppState.standInside` or `standBeside`, each through a type or a match of its
  own: a history choice through `Place.toLocation` and `Place.beside`, a
  favourite through `Place.toLocation`, a search answer through
  `FindLanding.AtDirectory`, and a path from the clipboard through `Landing`,
  which P18.5 added. `Landing` — `Into` a location, or `Beside` an entry in one
  — becomes the one answer: `Place` gains the two conversions to it and loses
  `beside`, `FindLanding` carries a `Landing` where it carried a directory
  (`AtHit`, which also shows a hidden hit, and `InPanel` keep their meaning),
  and `Update` lands through one function. The walk up of `Landing.ofPath` and
  `FileSystemPath.nearest` stay apart: one keeps the names it climbs past to
  read them inside an archive, the other stops at a directory and drops them.
  *Done when:* `standInside` and `standBeside` are called from one function in
  `Update`, the folder history records the same places as before, and the
  suite passes with only the renamed cases changed.
- **P21.11 `Shell` and `Ports` join `State`** — neither draws anything: `Ports`
  is the shape of the outside world and `Shell` interprets effects into it. Both
  move under `State/`, and with them the wording of `Ui/Messages`, which is
  plain text with no view in it and whose only readers are `Shell`,
  `Adapters/Prompts` and one dialog — all of them above `State`. `Modifier` moves
  to `Domain`; `Modifiers.ofKey`, which reads a Terminal.Gui key, stays in
  `Ui`. What `MainWindow.create` decides rather than wires — restoring the
  settings, opening the first paths, the first watch — becomes a `State`
  function the window calls. *Done when:* `Ui/App` holds views and wiring only,
  nothing under `State` opens a `Terminal.Gui` namespace, the dependency rule in
  [Overview](../Architecture/OVERVIEW.md) is restated for the new layout, and
  the suite passes with its tests moved alongside.
- **P21.12 Ports say what they are for** — a port that takes functions takes a
  request record instead, the way the prompts already do: `Runners.Batch` takes
  a title, the work and what to do with the result as named fields, and so do
  `Measure`, `Search` and the two `Watches`. A function type that stays a field
  (`LinkPrompt.Refusal`, `HistoryPrompt.Change`, `FavoritesPrompt.Change`) is
  given a name that says what it answers. *Done when:* no field of
  `Dependencies` is a function whose argument is another function, and `Fakes`
  builds every port from named parts.
- **P21.13 Where mutation is allowed** — a rule in
  [Coding style](../coding-style-conventions.md) says where a mutable binding or
  a reference cell earns its place: a measured hot loop confined to one
  function (`LineIndex`, `TextEncoding`, `ArchiveReader`, `RewindingStream`), an
  interop call that demands a `byref` (`NativeLinks`, `ClassicScheme`), state
  shared with another thread inside an adapter, and `Runtime`'s single cell.
  Every other use in `FileSystem` and `State` — the counters of
  `FileSearch.run`, the mutable record in `Archive`'s walk — becomes a fold or
  a returned value. *Done when:* each remaining mutable binding outside `Ui`
  falls under a clause of the rule, the hot loops are timed before and after in
  [Measurements](../Progress/MEASUREMENTS.md), and the suite passes unchanged.
- **P21.14 Views keep their state in one value** — `FileViewer`,
  `ResultsWindow`, `ResultRow` and `SettingsDialog` hold a handful of mutable
  locals each, changed from several event handlers. Each gathers them into one
  immutable record behind one cell, with the changes written as functions over
  the record, so the transitions can be tested without a view. *Done when:* each
  view has a single cell, its transitions have unit tests, and the rendered
  screens are unchanged before and after.

## Stage 7 — The suite and the platforms

- **P21.15 Test files by feature** — no test file runs past a thousand lines.
  `ShellTests` (about 2,600 lines, 161 tests) splits along the features its
  helpers already group — transfers, history, typed text, packing, unpacking
  and the rest — the way `ShellPlacesTests` was split from it, and `UpdateTests`
  splits the same way. Shared helpers move into a fixture module, as
  `ArchiveFixture` does for the archive tests. *Done when:* the longest test
  file is under a thousand lines, the count of tests is the same before and
  after, and [Testing](../Architecture/TESTING.md) states the limit.
- **P21.16 The manual rows, by hand** — every row of
  [Cross-platform](../Architecture/CROSS-PLATFORM.md) marked *manual* is run
  against the published binaries of this phase, on Windows and on a Linux
  desktop, by a person at the terminal: colours, a resized terminal, the mouse,
  a real Unix permission bit, the trash, an archive made by another tool, `F4`
  with a full-screen console editor, and the GNOME key grabs that
  [Rough edges](../ROUGH-EDGES.md) read out of a configuration rather than
  pressed. *Done when:* each manual row carries the date and the outcome on
  each platform, and every row that failed is an entry in
  [Rough edges](../ROUGH-EDGES.md).

Every task updates [What works today](../Progress/WHAT-WORKS.md) where the
behaviour changes and [Overview](../Architecture/OVERVIEW.md) where a module
moves. P21.1 adds `Architecture/DIAGNOSTICS.md` for the log, and P21.2
documents `--debug` in [the user guide](../USER-GUIDE.md).

## Left for a later phase

- **A log level in the settings** — debug mode is a switch for one session;
  making it persistent, or finer than on and off, waits until a session in the
  field shows it is needed.
- **Sending a crash report** — the log stays on the user's disk; nothing is
  uploaded.
