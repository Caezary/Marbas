# Testing notes

Tests live in `Marbas.Tests` (xUnit). Logic modules (`FileSystem`, `State`) are
covered directly; UI behaviour is covered by driving real Terminal.Gui views
with an in-memory file system (`MockFileSystem`), so no test touches the disk.

## Injecting the outside world

`MainWindow.create` takes a `Shell.Dependencies` record — file system, start
path, quit callback, error reporter, drive chooser, pattern prompt, name prompt
and confirmation. Everything that would open a modal dialog or end the process
is a function, so tests pass fakes and assert on what was requested.
`Program.fs` is the only place that supplies the real `MessageBox` dialogs.
`TestHelpers.dependencies` gives every port a do-nothing default — a
confirmation that answers no, prompts that cancel — so a test only overrides
the one it cares about.

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
