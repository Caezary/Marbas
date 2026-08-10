# Testing notes

Tests live in `Marbas.Tests` (xUnit). Logic modules (`FileSystem`, `State`) are
covered directly; UI behaviour is covered by driving real Terminal.Gui views
with an in-memory file system (`MockFileSystem`), so no test touches the disk.

## Arrange, act, assert

Every test is three blocks separated by a blank line — set the world up, do the
one thing under test, then assert. There are no `// Arrange` comments; the blank
lines carry the structure.

The act is a named binding, even when the call would fit inside the assertion:
`let drives = Drives.list fs` followed by `Assert.Equal<string list>([...],
drives)`, never `Assert.Equal<string list>([...], Drives.list fs)`. Where a test
needs to compare before and after, the "before" value is captured in the arrange
block and asserted at the end, so no assertion sits between two acts.

`Expect.ok` and `Expect.error` unwrap a `Result` so it can be acted on in one
line; without them the `match` that unwraps the result also holds the
assertions, which fuses act and assert. A `Result` that *is* the thing under
test stays whole and is compared with `Assert.Equal(Error(...), result)`.

`TestHelpers` carries the arrange boilerplate: `focusedWindow deps` builds,
initialises and focuses a `MainWindow`, and `leftList` / `rightList` reach the
panel `ListView` a test drives.

## Injecting the outside world

`MainWindow.create` takes a `Shell.Dependencies` record — file system, start
path, quit callback, error reporter, drive chooser, pattern prompt, name prompt
and confirmation. Everything that would open a modal dialog or end the process
is a function, so tests pass fakes and assert on what was requested.
`Program.fs` is the only place that supplies the real `MessageBox` dialogs.
`TestHelpers.dependencies` gives every port a do-nothing default — a
confirmation that answers no, an overwrite question that skips, prompts that
cancel — so a test only overrides the one it cares about. A prompt that carries
a suggestion (`AskTarget`) is easiest to fake as `fun _ suggestion -> Some
suggestion`, which is exactly "the user pressed Ok".

**Background work is a port, so tests stay synchronous.** `RunBatch` is the
dependency that copies and moves go through; `TestHelpers` supplies one that
runs the work inline with `Operations.unattended`, so a test presses `F5` and
finds the files already copied when the call returns. Nothing in the suite
waits on a thread. What that does *not* cover is `Program.fs` — the real
runner, its progress dialog and the `Application.Invoke` marshalling are only
ever exercised by running the app.

A UI test that needs the two panels on *different* directories gets there the
way a user would: `press list PanelInput.rightDriveKey` with a `ChooseOption`
that answers with the wanted path. No test reaches into the state to set it.

## Terminal.Gui pitfalls

**`Key` is mutable and `Handled` sticks.** Sending a shared `Key` instance
(such as `PanelInput.refreshKey`) through `NewKeyDownEvent` sets `Handled` on
that object permanently; every later send of the same instance is ignored and
handlers stop firing. Use `TestHelpers.press`, which sends a fresh copy.
The symptom of getting this wrong is a test that passes alone but fails as
part of the suite.

**Keys do not bubble.** `NewKeyDownEvent` on a nested view is not forwarded to
its parent, so key handlers are attached to the view that actually holds focus
— the panel's `ListView`.

**Application-level bindings need the running app.** Defaults such as
Tab → `NextTabStop` and `Command.Quit` are not exercised by
`NewKeyDownEvent` in unit tests. Assert the binding
(`Application.GetDefaultKey`) and the resulting behaviour (`AdvanceFocus`)
separately.

**A `Dialog` is much smaller inside than out.** Border, button row and padding
eat six rows, so `Height = 7` leaves a one-row content area and anything placed
below it is clipped — while still taking focus and keys, which looks like an
invisible but typable field. Dialogs are built in `Ui` modules, not inline in
`Program.fs`, so a test can lay one out and assert its views fit inside
`dialog.Viewport`.

**`ListView` type-ahead swallows printable keys.** `ListView.OnKeyDown` runs
before the `KeyDown` event and hands printable characters to its
`KeystrokeNavigator`, which reports a match even when nothing matches — so
`+` and `-` never reached Marbas until the panel set
`KeystrokeNavigator <- null`. The symptom is `NewKeyDownEvent` returning `true`
with `key.Handled` still `false`.

**StatusBar shortcuts swallow their keys.** A `Shortcut` marks its key handled
even with a `null` action, which is why every shortcut that should do
something gets a real callback.

**Parallelisation is disabled** (`AssemblyInfo.fs`) because Terminal.Gui keeps
process-wide static state, including default key bindings.

## Layout assertions

Formatting depends on the panel width, so rows are only column-formatted after
a layout pass. Tests that assert on column alignment call
`window.Layout(Size(width, height))` first; tests that only check entry names
inspect the unformatted rows produced before the first layout.
