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
