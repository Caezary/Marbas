# Phase 21 — The log and the debts

The tasks are in
[Phase 21 — The log and the debts](../Plan/PHASE-21-THE-LOG-AND-THE-DEBTS.md).

**P21.1 made a crash leave a log.** `Program.fs` now hands the real file system
and environment to `Session.run` in `Adapters`, which does what `Program.fs`
did — the quit key, the application, the theme, the dependencies, the window —
inside `Crashes.guarded`, with `Crashes.watch` subscribed for the length of the
session. An exception that escapes is written as a `LogEntry` to `marbas.log`
beside the settings, and once the application is disposed `Session.run` prints
the one-line pointer from `CrashMessage.written` to standard error and exits
with 1. How the pieces fit is in [Diagnostics](../Architecture/DIAGNOSTICS.md).

Three things went differently from the plan.

- **There is no error handler on `app.Run`.** The plan named
  `app.Run(window, errorHandler)`. A probe showed that Terminal.Gui 2.4.17
  rethrows the exception after the handler answers `false`, so the `try` in
  `Crashes.guarded` sees every loop crash anyway; a handler would have written
  each one twice, and would not have covered a crash while the window is built.
- **The log is not on `Dependencies` yet.** Nothing below `Adapters` writes to
  it in this task, and the crash hooks have to work before `Dependencies` exists
  — building it is one of the things that can throw. The hooks take
  `LogEntry.write` over the log path directly. The port joins `Dependencies` in
  P21.2, together with its first callers in `Runtime`, `Shell.interpret` and
  `BatchRunner`.
- **A task nobody awaited does not end the application.** The plan said the
  application still ends after a crash. That holds for the UI thread, and for
  another thread, where .NET ends the process whatever the handler does. A
  task's unobserved exception surfaces only when the garbage collector
  finalizes the task, at a moment unrelated to anything on screen, and the loop
  never saw it, so it is written and the session goes on.

`LogEntry` has one case, `Crashed of CrashSite * exn`, and sits in `State` after
`Update`, where the cases P21.2 adds will find the message and effect types.
`LogFile` in `FileSystem` places, appends to and cuts the file; its errors are
`LogFileError`, mapped at its one `mapException`. A log that refuses an entry
after a crash on the UI thread turns the pointer into `CrashMessage.unwritten`
followed by the exception, so the stack trace is not lost with the file.

The suite gained twenty tests: the cut on `MockFileSystem` at a line break and
inside a line, the stamped lines of each site, the two messages, and
`Crashes.guarded` closed, crashed and refused. The unobserved task is a real
one, collected inside the test after `Crashes.watch` subscribed; it passed five
runs in a row. The probe — a loop that throws from a timeout, run in a console
of its own — left the stamped stack trace in the file, the pointer on standard
error and exit code 1.

**Between P21.1 and P21.2 the formatting became a check.** The style guide had
asked for Fantomas defaults, but nothing ran Fantomas, and 325 of 470 files
were not what Fantomas 8.0.7 would write. Its defaults had moved on since the
rule was written: from version 7 a multi-line record or list opens and closes
its brackets on lines of their own, which would have touched about 15,700
lines. The code was written in the older `cramped` style, so the repository now
carries an `.editorconfig` with `fsharp_multiline_bracket_style = cramped` and
`end_of_line = lf`, and one reformat brought 277 files in line with it — mostly
the width rules: a binding wider than 80 characters moves to a line of its own,
a short pipeline is joined, a long one is broken. Fantomas is pinned in
`.config/dotnet-tools.json`, a `format` job in CI runs `--check`, and the style
rule that asked for one operation per line now asks for one statement, leaving
where a pipeline breaks to the formatter. The suite passed unchanged after the
reformat: 3766 tests, 15 skipped.

**P21.2 added debug mode.** `Marbas --debug` records, for that session, every
message `Runtime` drains with the panel it concerns, every effect
`Shell.interpret` carries out, every error `Shell` shows, and every batch
`BatchRunner` starts, finishes or sees cancelled, with the items it reported.
`Program.fs` passes its arguments to `HostEnvironment.current`, where
`Logging.ofArguments` turns `--debug` into `EveryTransition`, and
`DebugLog.writer` makes the new `Log` port of `Dependencies` either the crash
log's writer or `ignore`. The port came in here rather than in P21.1, with its
first callers.

- **Names come from reflection.** `LogEntry` carries the message and the effect
  themselves, and `LogEntry.lines` writes only the case name. Redaction is
  therefore not a list of cases to remember: no case's payload reaches the file,
  including the cases added after this task. The two cases that carry an
  `ArchivePassword` and the one that carries clipboard text are held to it by
  tests.
- **The panel a message concerns** is the one it names, where it names one —
  including the side inside a `PanelCursor`, which the probe showed `CursorMoved`
  had been logged without — and the active panel otherwise. An effect is
  written without one.
- **Errors are written with their words.** The plan's list of entries had an
  error reported to the user; it is logged in `Shell`'s single `report`, with
  the title and the sentence of the dialog, which is the only way a path reaches
  the debug log.
- **A batch is counted, not listed.** Every path a batch reports adds to a
  counter, and the end of the batch writes the count beside `finished` or
  `cancelled`. The plan allowed paths for batch items; a line per file of a
  large copy was more than any question so far needed.

`Runtime` gained a fifth constructor argument, the observer, and every test that
builds one passes a do-nothing `unobserved`. Twenty-six tests were added: the
lines of every entry, the panel of a message, the three redactions, the observer
seeing a follow-up message with the state it arrives at, a key in a real
`MainWindow`, `Shell`'s effect and error, the batch's count and its conclusion,
and the switch on and off. A probe ran the real window with `--debug` and the
real adapters against a scratch settings directory and read the file back.

**P21.3 stopped the progress window from being flooded.** A batch's report used
to queue an `Invoke` per path; now `BatchRunner.control` overwrites one cell,
and a timer the runner adds on the UI thread calls `BatchRunner.showLatest`
every 100 ms, which takes the cell's path, if any, and shows it. The timer is
removed as soon as the window's loop returns, before the window is disposed, so
the second look at the cancel flag that guarded a queued report against a
closed window had nothing left to guard and went with the queue.
`WindowSearchRunner`, the viewer's search, keeps its own control: it reports an
offset only when the text it shows changes, and was not part of the task.

The measurement is in [Measurements](MEASUREMENTS.md#the-progress-window-flooded-and-drained-p213-2026-10-09).
A batch that only reports paths closed its window 0.6 s after its work at ten
thousand paths and 35 s after it at a million; it now closes within a tick at
every size, and the work itself no longer slows down. Copying small files on a
disk never flooded the window and costs what it did. Getting the numbers took
two attempts: a probe that started its batch from a timeout waited forever, the
pitfall the `render-tui-screen` skill already names, and starting it from the
loop's `Iteration` event instead is what works.

The three tests of the old control, which replayed a queued report around a
cancel, gave way to four: a report only overwrites the cell, a tick shows the
latest path, a second tick with nothing new shows nothing, and the control still
answers the cancel flag.

**P21.4 kept a directory out of itself.** A test on `MockFileSystem` showed the
runaway first, and showed it worse than the plan had read it: `MockFileSystem`
lists a subdirectory before the files beside it, so a directory copied into its
own subdirectory went deeper without reporting a single file, and the test, bounded
at first by the number of reports, ended the test host with a stack overflow.
It is bounded now by the number of times the walk asks whether it was
cancelled, and asserts a copy three levels below where the source tree ends.

`Operations.landsWithinSource` beside `approve` is the check, and
`Archive.wouldPackItself` calls it. In `Update`, `requestIfOutsideSource` puts
every copy and move from the disk through it before the effect is requested —
`F5` and `F6` into the other panel, a typed `F6` target, a paste of copied or
cut files, and a copy or move into an archive whose file lies under a source —
and answers `TargetWithinSource` with the target's name. It joins the refusal
of an archive whose kind cannot be changed through one `requestUnlessRefused`,
the place under the source decided first. `TargetWithinSource` now reads "'…'
lies inside what would be copied, moved or packed into it", for packing too.

- **The gates are named for what they let through.** The first names were
  `refuseFirst`, `refuseTransfer` and the older `refuseUnchangeable`, and each
  read as if it always refused, though on the common path it requests the
  effect. They are `requestUnlessRefused`, `requestIfOutsideSource` and
  `requestIfChangeable`, and the row of
  [Coding style](../coding-style-conventions.md) that had named a gate for what
  it refuses now names it for what it does when it lets through.

- **A move is refused at the typed target, not at `F6`.** `F6` from the disk
  into a directory always opens the prompt first, and its answer is what is
  checked, so a move into the other panel under the source and a target typed
  under it are one refusal reached two ways. A move into an archive skips the
  prompt and is refused at `F6`.
- **A link is not followed.** The comparison is of paths, as packing's was, so
  a target that reaches a source only through a link still runs away. The
  entry in [Rough edges](../ROUGH-EDGES.md) for a transfer into itself is gone
  and a narrower one for the link takes its place.

The suite gained twenty-two cases, one of which skips on each platform: the
runaway, the target at or under a source and the one beside or above it on both
case rules, the five refusals and the two archive ones by name, a copy beside
the source and into a name that merely begins with the source's, a move into
the latter, and the sentence.

**P21.5 gave a console editor the terminal.** On Unix `F4` now stops the
Terminal.Gui driver before it starts the editor and builds it again when the
editor exits, inside the one `IApplication` the session runs; Windows starts the
editor through the shell as before. `EditorWait.WaitsForExit` is
`TakesTheTerminal`, `EditorProcess.run` takes the hand-over where it took a
repaint, and `TerminalHandOver.lend` in `Adapters` is the hand-over. How it
works is in [File operations](../Architecture/FILE-OPERATIONS.md).

The spike came first, as the plan asked. Terminal.Gui 2.4.17 was decompiled to
find a way that is not public: `IDriver.Suspend` sends `SIGTSTP` to the whole
process, and `Dispose` followed by `Init` resets the application with the
driver. What does the job is the internal `MainLoopCoordinator` — its `Stop`
ends the input thread and gives raw mode and the main screen back — and the
private `CreateDriver` and internal `SubscribeDriverEvents` that `Init` itself
calls, reached by reflection. A throwaway program stopped the driver from a key
handler, ran a child that read a line, and started the driver again; driven
through `script` on WSL the child got the line and the window got the keys
after it, and driven through tmux the same program round-tripped `vim` full
screen with the window redrawn.

- **The size had to be given by hand.** The first run of the real binary with an
  editor that does not exist crashed: the rebuilt driver learns the terminal's
  size from an ANSI query answered on a later iteration, the error dialog was
  laid out before that against a 0×0 screen, and `MessageBox` threw — the log
  of P21.1 said where. The size is now read through `Console.WindowWidth` and
  `WindowHeight` and set before the redraw, which also brings back a window
  resized while the editor held it.
- **Missing members fall back, and a test watches for them.** If an upgrade of
  Terminal.Gui renames one of the three members, `lend` lets the editor share
  the terminal as before and forces a redraw; `TerminalHandOverTests` asserts
  that the version in use has them, so the suite says so first.
- **Done by probe, not yet by hand.** The real binary was driven in tmux on WSL
  (Ubuntu 24.04) with `EDITOR=vim`: a line added and saved, the window resized
  while `vim` held it, the panels back at the new size with the file's new
  length, a second `F4`, `Up` reaching the panel, `F10` ending with 0; and with
  an editor that is not there, its error in a dialog over whole panels and the
  panels taking keys after it. Running it at a Linux desktop is left to P21.16,
  whose row in [Cross-platform](../Architecture/CROSS-PLATFORM.md) stays
  *manual*.

The suite gained four tests: the hand-over on Unix, none on Windows (a Windows
fact, since only Windows has the shell verb), the members found on the
Terminal.Gui in use, and none found on a type without them. The test of the
Unix description now names `TakesTheTerminal`. The entry in
[Rough edges](../ROUGH-EDGES.md) is gone.

**P21.6 kept a window program off the panels.** On Windows a start that names
its program — a user menu entry run `Directly`, `Enter` on an executable or with
a program chosen for the extension, and `F4` — now resolves the name to its
file and reads the subsystem from its PE header; a window program is started
from that file without the shell verb, its streams redirected and drained, and
everything else goes through the shell as before. `ProgramImage.subsystem` in
`FileSystem` reads the header, `ProgramResolution` in `Adapters` finds the file
and asks App Paths, and `HostEnvironment` gained `ProgramExtensions` from
`PATHEXT`. How the pieces fit is in
[File operations](../Architecture/FILE-OPERATIONS.md).

The spike came first and at first found nothing. A throwaway program started
`Code.exe` through the shell verb, then from its file with its streams drained,
then `cmd` through the verb, then `cmd` inheriting the console as a control, and
after each read its own console buffer back with
`ReadConsoleOutputCharacterW`. Run from a fresh console window — Windows
Terminal — the control left its marker and nothing else did, VS Code fresh or
already running. The user had seen the log in cmder, and under ConEmu the same
program read back the log they had pasted: `[main …]` lines, the `DEP0169`
deprecation warnings and `Unknown channel: agentHostClientProxy`, from a fresh
VS Code started through the verb. Started from its file with its streams
drained, a fresh VS Code wrote nothing and opened the file; `cmd` through the
verb still got a console of its own.

- **The terminal decides whether it shows.** The defect is real under ConEmu
  and invisible under Windows Terminal and conhost, which is written down in
  [Cross-platform](../Architecture/CROSS-PLATFORM.md) beside the row it belongs
  to.
- **The subsystem is a union of five.** Beside a window and a console program,
  `OtherSubsystem` keeps a native or EFI image from passing for either,
  `NotAnImage` covers a file too short, a script and a header that points
  nowhere, and `UnreadableImage` a file that cannot be opened. Only a window
  program changes how anything starts.
- **A name without an extension never stands alone.** `code` is looked up as
  `code.com`, `code.exe`, `code.bat`, `code.cmd` in each directory in turn, the
  way Windows does it, so the shell script `code` beside `code.cmd` in VS Code's
  `bin` is never taken; `code` therefore resolves to the script and keeps the
  shell verb, while `code.exe` resolves through App Paths to the window program.
  Run against this machine, `notepad` and `explorer.exe` came out window
  programs and `cmd` a console one.
- **Run as administrator keeps the verb**, since `runas` exists only as one, and
  a user menu shell run stays `cmd` whatever its line names.
- **Done by probe, the hand check left to the user.** A probe under ConEmu ran a
  user menu entry `code.exe <file>` through the real `CommandProcess.start` with
  a fresh VS Code: the file opened and the console held nothing but the probe's
  own line. `code.exe %p` from the real `F2` menu in cmder has not been run by a
  person yet.

The suite gained thirty-six tests: eight on the header — a window program, a
console one, another subsystem, a file too short, a script, a header pointing
past the end and one without the signature, a file that is not there —
fourteen on the resolution, four on `PATHEXT`, and ten describing the starts of
`Enter`, `F2` and `F4` with a fake that knows one window program. The entry in
[Rough edges](../ROUGH-EDGES.md) is narrowed to a start through an association.

**P21.7 swept old sessions at start.** A session now makes its working
directory as it starts and writes the file `owner` into it, the process id and
the moment the process started, and then reads the directories beside its own
under `<temporary root>\Marbas`: one whose owner no longer runs is removed
whole, one with no owner once nothing in it has been written for a day.
`SessionOwner` and `Workspace.claim` and `sweep` in `FileSystem` are the rules,
`SessionProcess` in `Adapters` fills the two new fields of `Processes` — the
session's `Owner` and `IsRunning` — and `MainWindow.create` raises the new
effect `SweepLeftovers`, which `Shell` hands to the new `Runners.Background`, a
task with no window. How the pieces fit is in
[File operations](../Architecture/FILE-OPERATIONS.md).

Four things went differently from the plan.

- **The directory is made at the start, not at first use.** The plan had the
  owner file written when the session makes its directory, which until now
  was whenever something first needed it. Three places make it —
  `Workspace.scratch` from `Shell`, the same from `Archive.copyToArchive`,
  which holds a path and nothing else, and `CommandProcess.writeList` for a
  list file — and each would have needed the owner. One `Workspace.claim` at
  the start covers them, and every session now holds an empty directory with a
  file of two lines for as long as it runs.
- **"Started at that moment" is within two seconds.** The comparison was going
  to be an equality. A script under WSL showed three processes reading the
  start of a fourth up to 2.8 ms apart, where Windows hands every reader the
  same tick, so an equality would have let each session on Linux remove the
  directory of every other one still running.
  `SessionProcess.sameStart` allows two seconds; the numbers are in
  [Measurements](MEASUREMENTS.md#one-start-read-by-two-processes-p217-2026-10-10).
- **The line in the log is written without `--debug` too.** The log port wrote
  nothing without the switch, and a leftover that is not shown and not written
  would have been skipped in silence. `LogEntry.isFailure` now names what is
  written whatever the switch says — a crash, `LeftoverKept` and
  `WorkspaceUnclaimed` — `DebugLog.writer` lets those through, and
  `CrashesOnly` became `FailuresOnly`. The entries carry the reason as the
  sentence `OperationErrorMessage` makes of it, built in `Shell`, the way
  `ErrorReported` carries its words.
- **The log has one writer at a time.** The sweep writes from its own thread
  while the UI thread may be writing a transition, and an append that met the
  file open would have lost its line. `Session.run` takes a lock around the
  write, which also covers the crash hooks that were already writing from
  other threads.

Three smaller choices were made along the way.

- **A claim that fails is logged, not shown.** When the working directory
  cannot be made the session goes on, the sweep still runs, and whatever needs
  the directory later reports it the way it always did.
- **A directory read as nobody's waits a day.** An `owner` that cannot be read
  as one — half written, or a session caught between making the directory and
  writing the file — is treated like none at all, and the day decides.
- **Two entries in [Rough edges](../ROUGH-EDGES.md) came out of it.** A wall
  clock stepped by more than two seconds makes a running session on Linux look
  dead to the next one, reasoned from the measurement and not seen; and on
  Unix every user's sessions share one `Marbas` directory under the temporary
  one, which only its first user can write to — older than this task, but the
  claim is where it now shows first. The entry the task was written against is
  gone, and the one about a run from the history that recreates an old
  session's directory now says when that directory goes.

Done by probe on both systems, with the temporary directory pointed at a
scratch one. On Windows a session in a console window of its own was closed by
its button — `WM_CLOSE` posted to the window — and the next start removed its
directory, one planted with the id of a running process and another start, and
an unclaimed one dated two days back, and kept a fresh unclaimed one, a file
beside them and, on a third start, the directory of the second session still
running. Under WSL three sessions in tmux showed a running one kept and a
killed one removed. The Linux leg of the suite was run there as well, from a
copy of the tree: 3870 passed and 50 skipped.

The suite gained fifty-three tests: eleven on the owner's two lines, sixteen on
the claim and the sweep on `MockFileSystem` — a dead session, an id that
belongs to another start, a live session, the session's own, the day from
both sides and with one fresh file deep inside, an owner that names nobody, a
directory that refuses and one after it that still goes, a file beside the
directories, a directory beside `Marbas`, and no `Marbas` at all — seven on
`Shell`, among them the log line with no dialog and the work handed to the
runner rather than done, seven on the two entries and on what counts as a
failure, one on the writer with the switch off, ten on the real process and
the two seconds, and one that builds the window over a leftover.
`Fakes.HeldOpen` is the file that will not be deleted.

**P21.8 opened the clipboard with a window of Marbas's own.**
`SystemClipboard` makes one message-only window the first time the clipboard is
asked for, on a background thread that runs a message loop for it, and every
request opens the clipboard with that window, one request at a time. While a
request runs no other program can open the clipboard, and Marbas is the owner
of what it writes. How it fits is in
[State and effects](../Architecture/STATE-AND-EFFECTS.md), and what the spike
measured is in
[Measurements](MEASUREMENTS.md#who-a-held-clipboard-keeps-out-p218-2026-10-10).

Three things went differently from the plan.

- **The gap was not the one the plan described.** The plan had a clipboard
  opened with no window keeping no other process out. The first version of the
  test said so and failed: a second program that brought a window was refused.
  What the system does is know an open clipboard by its window, and let a
  second caller in when it brings the same one — and no window is the same as
  no window. The second process of the phase 18 measurement was the adapter,
  which brought none. So the adapter had been safe from every program that
  opens with a window and open to every one that does not, and the tests now
  say both.
- **Two threads of Marbas were never kept apart either**, which the plan did
  not ask about, and a window alone does not change that: two requests opening
  with the one window are both let in, and the first to close closes it for the
  other. A lock around `whileOpen` takes them in turn. The plan had requests
  reaching the adapter from the UI thread and from batches alike; read again,
  every one of them comes from the UI thread — `BatchRunner` runs a batch's
  `finish`, which empties the clipboard after a pasted move, through
  `app.Invoke`. The lock therefore guards what the port allows rather than
  what the application does today.
- **The window has a thread of its own.** The plan asked which thread may make
  the window. Any thread may open the clipboard with it, but the one that made
  it has to outlive every request and has to answer the window's messages, or
  another program's write waits five seconds for it; neither the UI thread nor
  a batch's does both.

`whileOpen` is public, the seam the plan asked for: a test runs another
program's `OpenClipboard` inside it. Nothing destroys the window at exit, since
what was handed over stays without it.

Done by test and by probe. The suite gained nine tests in
`WindowsClipboardTests`: three on what the system answers between two other
programs — none against none, none against a window, a window against none —
two on another program refused while Marbas holds the clipboard, with a window
and without, one on the clipboard free again afterwards, one on Marbas being
the owner of what it wrote, one on another program's write not waiting for
Marbas, and one on a second request of Marbas waiting for the first. Three of
them failed before the change: the program without a window was let in, the
owner was nobody, and the second request ran inside the first. One mutation was
tried, the message loop replaced by a sleep: the two pastes of the system shell
failed in a run of the folder, and the test of the wait failed at five seconds
run alone but passed in that run, for a reason that was not taken apart. The
twenty-three older tests pass unchanged. For the exit, a throwaway program
built on the real adapter put text and exited, then files and exited; another
program read both back with no owner left on the clipboard, and the system
shell pasted the files, a directory and what it held among them. Marbas itself
quitting with `F10` and a paste into an editor has not been done by a person.

**P21.9 said replacing by name.** `Operations.copyFile` lost its `bool`: every
copy replaces what stands at the target, so there was nothing to choose.
`moveFile` takes an `ExistingTarget` where it took one — `Replaced` for every
transfer and for the rename `startRename` makes of a file, `Kept` for the
anonymous rename a wipe makes before it deletes. The type is private to the
module, like the two functions, and the one place it becomes the `bool` that
`File.Move` asks for is inside `moveFile`. The `true` handed to `File.Copy`,
`Flush` and `GetHexString` stays: those are the system's signatures, not
functions of `Operations`.

No test was written first, since none could fail: the functions are private
and nothing they do changed. The suite passes unchanged, 3916 tests with 16
skipped on Windows. Swapping the two cases inside `moveFile` to see the suite
notice failed one test, `move replaces whatever is already there`; nothing
fails for `Kept` taken as `Replaced`, because the name a wipe moves to is
drawn at random until one is free and no test can put a file in its way —
which was as true of the `false` it replaced.
