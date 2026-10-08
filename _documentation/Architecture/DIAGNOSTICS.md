# Diagnostics

**A crash leaves a log.** Until P21.1 an exception that escaped the loop ended
the application with nothing to show for it: the terminal was handed back and
the reason was gone. Now the exception is written, with its stack trace, to
`marbas.log`, and once the terminal is restored the console says in one line
where that file is:

```
Marbas stopped on an unexpected error. The details are in 'C:\Users\user\AppData\Roaming\Marbas\marbas.log'.
```

The process then exits with code 1, where a session closed by the user exits
with 0.

**The log lives beside the settings.** `LogFile.beside` puts it in the
directory of `settings.conf`, the way `History.beside` places `history.conf`:
`%APPDATA%\Marbas\marbas.log` on Windows, `~/.config/Marbas/marbas.log` on Unix.
It is one file, written by appending, and every line starts with the moment it
was written as a `DateTimeOffset` in the round-trip format (`o`), so a line cut
out of the file still says when it happened. An entry that spans several lines
— an exception's message and its stack trace — stamps each of them with the
same moment.

**It is cut when a session starts.** `Session.run` calls `LogFile.cut` before
anything else, and a file longer than a megabyte keeps only its last megabyte.
The cut falls at a line break: the longest tail of at most `KeptBytes` that
starts a line is kept, so no line is left with its beginning missing, and a
multi-byte character cannot be split, since `\n` is never part of one in UTF-8.
Cutting at the start rather than on every write keeps a write to an append; a
single session can grow the file past a megabyte, and the next start trims it.

**A log that cannot be written is passed over, except after a crash.** The log
is the last place a failure can be told, so nothing reports a log that refuses
a cut or an entry written from another thread. After a crash on the UI thread
there is still a console to say it on: `Crashes.guarded` answers
`CrashUnwritten` with the `LogFileError`, and `Session.run` prints the reason
the file refused instead of the pointer, followed by the exception itself, so
the stack trace reaches the user either way.

## What is recorded

`LogEntry` is the union of what the log records. P21.1 gave it the crash,
`Crashed of CrashSite * exn`, where the site says which of the three hooks
caught it. `LogEntry.lines` turns an entry into its stamped lines and
`LogEntry.write` appends them through `LogFile.append`, both on `IFileSystem`.
It sits in `State`, after `Update`, because the entries P21.2 adds — a message
handled, an effect interpreted — carry the types `Update` defines.

## The three hooks

**The UI thread: everything `Session.run` starts runs inside `Crashes.guarded`.**
Starting the application, building the dependencies and the window, and
`app.Run` itself are one function handed to `guarded`, which catches whatever
escapes it and records `Crashed(UiThread, …)`. Terminal.Gui 2.4.17 was measured
before the hook was chosen: an exception thrown from a timeout inside the loop
leaves `app.Run` whether or not an error handler is given. With `app.Run(window, errorHandler)` the handler is called and, when it
answers `false`, the same exception is rethrown. A handler would therefore only
have logged what the `try` around it logs anyway, and would have left a crash
while the window is being built uncaught, so there is none. The `use` that
holds the application sits inside the guarded function, so the application is
disposed — the terminal handed back — while the exception unwinds, before
`guarded` writes the entry and well before the pointer is printed.

**Another thread: `AppDomain.UnhandledException`.** An exception nobody catches
on a thread of its own — a file system watcher's callback, a timer — ends the
process whatever a handler does. `Crashes.onUnhandled` writes it as
`Crashed(OtherThread, …)` on its way out. The terminal is not restored there:
the UI thread is still running and owns the driver, and disposing the
application from another thread could hang the very exit it is meant to tidy.
`ExceptionObject` is cast to an exception without a check, because .NET wraps
anything thrown that is not one in a `RuntimeWrappedException`.

**A task nobody awaited: `TaskScheduler.UnobservedTaskException`.** A faulted
task whose exception was never read raises this event when the garbage
collector finalizes it, which can be long after it failed. `Crashes.onUnobserved`
writes the `AggregateException` as `Crashed(UnobservedTask, …)`, inner
exceptions and their stack traces included. The application does not end: such
a task raises nothing on the loop, the session is in a state that was tested,
and the event fires at a moment the user cannot connect to anything. What the
user saw — a search that never finished — is explained by the log.

`Crashes.watch` subscribes both events and answers an `IDisposable` that
unsubscribes them, which `Session.run` holds for the length of the session and
which lets a test subscribe for the length of one test.

## How it is tested

`LogFile` and `LogEntry` run on `MockFileSystem`; the cut is checked on a file of
1024 lines of exactly a kibibyte each, so the boundary falls at a line break in
one test and inside a line in the other. `Crashes.guarded` is handed a session
that throws and a record that refuses. The unobserved task is real: a test
subscribes `Crashes.watch`, lets a task fail in a function that keeps no
reference to it, and forces a collection and the finalizers, after which the
record holds its exception. The hook around `app.Run` was proved with a probe
whose loop throws from a timeout, run in a console of its own: the log held the
stamped stack trace down to `ApplicationImpl.Run`, standard error held the
pointer, and the exit code was 1.
