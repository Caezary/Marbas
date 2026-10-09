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
