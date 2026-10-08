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
