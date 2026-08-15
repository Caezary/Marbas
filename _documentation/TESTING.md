# Testing notes

Tests live in `Marbas.Tests` (xUnit). Logic modules (`FileSystem`, `State`) are
covered directly; UI behaviour is covered by driving real Terminal.Gui views
with an in-memory file system (`MockFileSystem`), so no test touches the disk.

## Layout

`FileSystem/`, `State/` and `Ui/` mirror the source folders they cover, one test
module per module under test, and namespaces follow: `Marbas.Tests.State.
UpdateTests` covers `Marbas.State.Update`. Everything in them is a unit test —
pure, fast, no Terminal.Gui.

`Integration/` holds every test that constructs a Terminal.Gui view, from a lone
dialog up to a whole `MainWindow` driven by keystrokes. That is the line: a test
that touches Terminal.Gui needs `BeginInit`/`EndInit`, a layout pass and the
process-wide statics that force parallelisation off, so it belongs on the other
side of the fence from the pure ones.

The split is what makes a file like `State/DeleteTests.fs` (does `deleteTargets`
delete the right entries?) readable next to `Integration/DeleteKeyTests.fs`
(does `F8` confirm and then delete?) — they used to be one file. Either half
runs on its own:

```
dotnet test --filter "FullyQualifiedName~Marbas.Tests.Integration"
```

Three modules sit outside the folders because everything uses them:
`Expect` (result unwrapping), `Fakes` (the do-nothing `Shell.Dependencies`) and
`AssemblyInfo`. `Integration/Gui.fs` adds the view helpers — `focusedWindow`,
`leftList`, `rightList`, `press`, `panel`. F# does not open an enclosing
namespace for you, so a file under `State/` that uses `Expect.ok` needs an
explicit `open Marbas.Tests`.

## Arrange, act, assert

Every test is three blocks separated by a blank line — set the world up, do the
one thing under test, then assert. There are no `// Arrange` comments; the blank
lines carry the structure.

The act is a named binding, even when the call would fit inside the assertion:
`let drives = Drives.list fs` followed by `Assert.Equal<FileSystemPath
list>([...], drives)`, never `Assert.Equal<FileSystemPath list>([...],
Drives.list fs)`. Where a test
needs to compare before and after, the "before" value is captured in the arrange
block and asserted at the end, so no assertion sits between two acts.

`Expect.ok` and `Expect.error` unwrap a `Result` so it can be acted on in one
line; without them the `match` that unwraps the result also holds the
assertions, which fuses act and assert. A `Result` that *is* the thing under
test stays whole and is compared with `Assert.Equal(Error(...), result)`.
`Expect.some` does the same for the `Option` that a validating constructor
answers with, so `NamePattern.tryCreate "*.fs" |> Expect.some` builds a value
in the arrange block instead of branching there. Total constructors need no
such help: modules that have one expose it as `create`, and tests alias it at
the top the way they already alias `fp` — `let private en = EntryName.create`.

`Integration/Gui.fs` carries the arrange boilerplate: `focusedWindow deps`
builds, initialises and focuses a `MainWindow`, and `leftList` / `rightList`
reach the panel `ListView` a test drives.

## Injecting the outside world

`MainWindow.create` takes a `Shell.Dependencies` record — file system, start
path, quit callback, error reporter, drive chooser, pattern prompt, name
prompt, confirmation, overwrite question, batch runner, file viewer and editor.
Everything that would open a modal dialog, start a process or end the process
is a function, so tests pass fakes and assert on what was requested.
`Program.fs` is the only place that supplies the real `MessageBox` dialogs.
`Fakes.dependencies` gives every port a do-nothing default — a
confirmation that answers no, an overwrite question that skips, prompts that
cancel — so a test only overrides the one it cares about. A prompt that carries
a suggestion (`AskTarget`) is easiest to fake as `fun _ suggestion -> Some
suggestion`, which is exactly "the user pressed Ok".

**Background work is a port, so tests stay synchronous.** `RunBatch` is the
dependency that copies and moves go through; `Fakes` supplies one that
runs the work inline with `Operations.unattended`, so a test presses `F5` and
finds the files already copied when the call returns. Nothing in the suite
waits on a thread. What that does *not* cover is `Program.fs` — the real
runner, its progress dialog and the `Application.Invoke` marshalling are only
ever exercised by running the app.

**An external program is a port too.** `RunEditor` is what `F4` goes through,
so a test hands it a function that records the path — and, where the point is
the refresh that follows, one that writes into the `MockFileSystem` before
answering `Ok`. Nothing in the suite starts a process.

A UI test that needs the two panels on *different* directories gets there the
way a user would: `press list PanelInput.rightDriveKey` with a `ChooseOption`
that answers with the wanted path. No test reaches into the state to set it.

## Terminal.Gui pitfalls

**`Key` is mutable and `Handled` sticks.** Sending a shared `Key` instance
(such as `PanelInput.refreshKey`) through `NewKeyDownEvent` sets `Handled` on
that object permanently; every later send of the same instance is ignored and
handlers stop firing. Use `Gui.press`, which sends a fresh copy.
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

**`RequestStop` does nothing outside a run.** `Runnable.RequestStop` goes
through the application that is running the dialog, so a dialog built in a test
never sets `StopRequested` — not even after `SetIsRunning true`. A view that
has to close itself therefore takes the closing action as an argument
(`FileViewer.create close …`), which a test can record and `Program.fs` fills
in with `_.RequestStop()`.

**A `TextView` lets its `KeyDown` handlers see `Esc` and function keys**, and
normalises the text it is given: lines joined with `\n` come back joined with
`Environment.NewLine`, so an assertion on `TextView.Text` has to use the
platform separator.

**Colours are asserted as schemes, never as pixels.** There is no driver in a
test, so nothing can read back what was painted. What a test can assert is the
`Scheme` a view resolves to — `view.GetScheme()` — and the attributes the scheme
holds, which is what `Integration/SchemeTests.fs` and `Ui/ClassicSchemeTests.fs`
do. A view without a scheme of its own inherits its super-view's, so the panel
list and the status line are covered by the schemes set on the panel frame and
on the window.

**Which `VisualRole` a view draws with is observable.** Subscribing to
`view.GettingAttributeForRole`, laying the view out and calling `Draw()` records
every role it asks for, in order, which is how the palette was matched to
`ListView`, `FrameView` and `StatusBar`. Useful when picking colours; too
coupled to Terminal.Gui's internals to keep as a test.

**Parallelisation is disabled** (`AssemblyInfo.fs`) because Terminal.Gui keeps
process-wide static state, including default key bindings.

## Layout assertions

Formatting depends on the panel width, so rows are only column-formatted after
a layout pass. Tests that assert on column alignment call
`window.Layout(Size(width, height))` first; tests that only check entry names
inspect the unformatted rows produced before the first layout.
