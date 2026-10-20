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

**One writer at a time.** `Session.run` takes a lock around `LogEntry.write`, so
an entry from the UI thread, one from the sweep of P21.7 on its background task
and one from a crash hook on a thread of its own are appended one after the
other, and none loses its line to a file the other holds open.

**A log that cannot be written is passed over, except after a crash.** The log
is the last place a failure can be told, so nothing reports a log that refuses
a cut, an entry of debug mode or an entry written from another thread. After a crash on the UI thread
there is still a console to say it on: `Crashes.guarded` answers
`CrashUnwritten` with the `LogFileError`, and `Session.run` prints the reason
the file refused instead of the pointer, followed by the exception itself, so
the stack trace reaches the user either way.

## What is recorded

`LogEntry` is the union of what the log records, and `LogEntry.lines` is the one
place that decides what of it reaches the file:

| Entry | Written by | Line |
|-------|------------|------|
| `MessageHandled(message, panel)` | the observer `MainWindow` hands `Runtime` | `message HiddenToggled on the left panel` |
| `EffectInterpreted effect` | `Shell.interpret`, before it acts | `effect ViewFile` |
| `ErrorReported(title, message)` | `Shell`'s one `report`, before the dialog | `error shown: Cannot copy: Access denied to '…'.` |
| `BatchStarted title` | `BatchRunner`, before the work starts | `batch started: Copying` |
| `BatchFinished(title, items)` | `BatchRunner`, on the UI thread when the work returns | `batch finished: Copying, 12 items` |
| `BatchCancelled(title, items)` | the same, when `Esc` closed the window first | `batch cancelled: Copying, 3 items` |
| `WorkspaceUnclaimed(directory, reason)` | `Shell`, when the session's working directory could not be made or its owner written | `working directory not claimed: …\Marbas\0f3c…: Access denied to '…'.` |
| `LeftoverKept(directory, reason)` | `Shell`, for each directory the sweep at start could not remove | `leftover kept: …\Marbas\69bd…: Could not complete '…': …` |
| `Crashed(site, version, exn)` | the three hooks below | `crash on the UI thread in Marbas 0.1.0: System.…` and the stack trace |

`LogEntry.write` appends the lines through `LogFile.append`, both on
`IFileSystem`. The union sits in `State`, after `Update`, because two of its
cases carry `Message` and `Effect`.

**Names, not contents.** A message and an effect carry the whole value, and
`lines` writes only its case name, read through `FSharpValue.GetUnionFields`.
Nothing a case carries — a path, an archive password, clipboard text, a typed
name — can reach the file through them, and the rule holds for every case added
later without anyone remembering it. Tests hold the two cases that carry an
`ArchivePassword` (`ArchiveEntered`, `ArchiveTargets`) and `PutOnClipboard` to
it. Paths reach the log only inside an error's words, which are the sentence
the user was shown, in a crash's message, and in the two entries of the sweep,
which name a directory under the temporary one and the reason it refused.
Every error reported is one effect since P21.12, so its line is `effect Report`
whatever went wrong; which error it was is the `error shown:` line `Shell.report`
writes right after it.

**A failure is written whatever the switch says.** `LogEntry.isFailure` answers
yes for a crash and for the two entries of the sweep at start
([File operations](FILE-OPERATIONS.md) has the sweep itself), and no for
everything debug mode adds. A leftover that will not go is not shown to the
user, who did nothing to cause it, so the log is the one place it is told; a
line that only `--debug` wrote would have told nobody. An error shown in a
dialog is no failure in this sense: the user has read it.

**The panel a message concerns.** `LogEntry.handling` names the panel the
message carries — `PanelFocused`, `RootRequested`, `PathOpened`, `DriveChosen`
and the two that carry a `PanelCursor` — and otherwise the active one, read
from the state the message arrives at. An effect has no panel: it is written
right after the message that raised it.

**A batch's items are counted, not listed.** `BatchRunner.counting` wraps the
control's `Report` so every path a batch reports adds one to a counter shared
with the worker thread (`Interlocked.Increment`), and `BatchRunner.concluded`
reads whether the cancellation was asked for when the work returns. The count
is a plain `int` on purpose: it is an ordinal, written and never computed with.
Writing each reported path would have made a large copy write a line per file
for no question anyone has asked yet.

## Debug mode

**Asked for on the command line, for one session.** `Marbas --debug` turns on
every entry above but the failures, which are written whatever the switch says.
`Program.fs` hands its arguments to `HostEnvironment.current`, the adapter that
reads the environment, and `Logging.ofArguments` answers `EveryTransition` when
`--debug` is among them and `FailuresOnly` otherwise. Nothing is saved, so a
switch forgotten in a shortcut is the only way to keep it on. Any other argument
is ignored, as all of them were before, except `--version` (below).

**The switch is decided once, at the port.** `Ports.Dependencies` carries
`Log: LogEntry -> unit`, and `DebugLog.writer` fills it in `Session.run`: the
same `LogEntry.write` the crash hooks use when the switch is on, and that write
behind `LogEntry.isFailure` when it is off. `Runtime`, `Shell` and `BatchRunner` call it unconditionally and never
learn which it is, which is what lets a test hand them a recording fake. The
fake in `Fakes.dependencies` is `ignore`.

**`Update` stays pure.** `Runtime` takes an observer, `'msg -> 'state -> unit`,
and calls it with each message it drains and the state that message arrives at,
before the update. It is the one place that sees every transition, including
the ones a transition's own effects dispatch.

## The version

I1.2. A crash report is only useful with the binary it came from, so every
crash names it: the `Crashed` entry carries the `ProductVersion` and its line
reads `crash on the UI thread in Marbas 0.1.0: …`, and both lines
`CrashMessage` prints after a crash start `Marbas 0.1.0 stopped …`.
`HostEnvironment.productVersion` reads it once from the
`AssemblyInformationalVersionAttribute` of the Marbas assembly — not the entry
assembly, which under the tests is the test host — and `Session.run` hands it
to `Crashes.watch` and `Crashes.guarded`. The attribute holds the version MinVer
computed with no commit appended
(`IncludeSourceRevisionInInformationalVersion` is false), since a release's
commit is its tag; see [Packaging](PACKAGING.md#the-version).

`Marbas --version` prints `Marbas 0.1.0` and exits with code 0 before anything
else happens: no log is cut, no crash hook subscribed, no terminal taken.
`Launch.ofArguments` answers `PrintVersion` when the switch is among the
arguments, whatever else is, and `OpenPanels` otherwise.

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
record holds its exception. The task comes from `Task.FromException`, not
`Task.Run`: a pool thread still holds a task it ran for a moment after waiters
see it complete, so a collection in that window leaves it alive and the event
unraised, which failed the test on a CI runner. The hook around `app.Run` was proved with a probe
whose loop throws from a timeout, run in a console of its own: the log held the
stamped stack trace down to `ApplicationImpl.Run`, standard error held the
pointer, and the exit code was 1.

Debug mode is held by tests with a recording fake for `Log`: a key pressed in a
real `MainWindow` leaves its `MessageHandled`, `Shell.interpret` leaves the
effect and the error it reports, `BatchRunner.concluded` answers a cancelled
batch, and `DebugLog.writer` writes no transition with the switch off and a
failure all the same. A probe ran the
real window with the real adapters and `--debug` against a scratch settings
directory, pressed four keys, and the file read:

```
… message PathOpened on the left panel
… message PathOpened on the right panel
… effect WatchLocations
… message PanelFocused on the left panel
… message HiddenToggled on the left panel
… message CursorMoved on the left panel
… message PanelFocused on the right panel
```
