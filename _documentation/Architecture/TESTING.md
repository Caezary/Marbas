# Testing notes

Tests live in `Marbas.Tests` (xUnit). Logic modules (`FileSystem`, `State`) are
covered directly; UI behaviour is covered by driving real Terminal.Gui views
with an in-memory file system (`MockFileSystem`), so no test touches the disk.

## Running the suite

```
dotnet test Marbas.slnx
```

That is the whole suite — a few seconds, no disk, no terminal, no
network, nothing to set up beyond the SDK. Nothing is marked flaky, so a red run
is a real failure. A handful skip on Linux: the ones whose subject is drive
letters.

**One suite, two platforms.** Every path in an arrange block is built with
`Root.at`, which answers `C:\start\docs` on Windows and `/start/docs` on Linux,
so `MockFileSystem` is handed paths its own platform understands. `Root.relative`
does the same without a root, for the tests that care about a name carrying a
directory — on Linux a backslash is an ordinary character in a file name, so
`docs\notes.txt` there is one file and not a path. `[<WindowsFact>]` sets `Skip`
off Windows and carries the reason: bare, it says drive letters, which is what
`MockFileSystem` emulates Windows-style while answering a duplicated `/:\`
elsewhere; given a reason of its own, it marks the archive-name tests, whose
subject is the OEM code page a culture answers with — a number Windows keeps and
other platforms do not. Nothing else in the suite knows which platform it is on.

Coverage comes from the collector the test project already references:

```
dotnet test Marbas.slnx --settings coverlet.runsettings
```

`coverlet.runsettings` asks for Cobertura and excludes `Program.fs` and
`Adapters/`, the composition root — they are the files that wire the real file
system, the real dialogs and the real process, so there is nothing in them a
test can reach. Everything else is measured, and coverage stays close to
complete.

**Coverage is a reading, not a target.** P8.1 measured the suite rather than
adding to it blindly: the collector named the lines no test reached, and each
one was either given a test that says something (a read-only file refuses to be
deleted, a file in use cannot be viewed, an unreadable config file falls back to
the defaults, every sort key survives being written and read back, a transfer
whose other panel vanished answers with an error) or written down below as
unreachable with a reason. `Program.fs` and `Adapters/` are excluded in
`coverlet.runsettings` instead of being counted and apologised for: they are
the composition root, and a test that could reach them would be starting the
real application.

`.github/workflows/ci.yml` runs exactly the three commands above (restore,
build, test) on `windows-latest` and on `ubuntu-latest` for every push to `main`
and every pull request that touches something other than documentation, and
keeps each leg's `.trx` and coverage file as its own artifact. `fail-fast` is
off, so one platform going red still shows what the other did. A second push to
the same pull request cancels the run still going for the previous one; pushes
to `main` are left to finish, so the badge and the history keep a result for
every commit.

What the suite cannot check on either platform — colours, a resized terminal,
the mouse, a real permission bit, an editor sharing the terminal — is the
checklist in [CROSS-PLATFORM.md](CROSS-PLATFORM.md).

Four lines have no test and cannot get one from here: the `with ex ->` in
`DirectoryListing.read` and in `Operations.isNonEmptyDirectory`, and the two
`asOperationError` cases that only a failing listing produces. All three sit
behind a directory enumeration that throws, and `MockFileSystem` has no way to
make one throw — a file can be locked or made read-only, a directory cannot.
What each of them maps is unit-tested directly through `mapException`, so the
gap is the plumbing, not the rule. `AppState.navigateUp`'s `None` branch is
unreachable by construction: only `..` leads there and a root directory is never
listed with one.

## Layout

`FileSystem/`, `State/` and `Ui/` mirror the source folders they cover, one test
module per module under test, and namespaces follow: `Marbas.Tests.State.
UpdateTests` covers `Marbas.State.Update`. `Ui/` mirrors the source down to the
subfolder, so `Ui/Panels/PanelTitleTests.fs` covers `Ui/Panels/PanelTitle.fs` as
`Marbas.Tests.Ui.Panels.PanelTitleTests`. Everything in them is a unit test —
pure, fast, no Terminal.Gui.

`Integration/` holds every test that constructs a Terminal.Gui view, from a lone
dialog up to a whole `MainWindow` driven by keystrokes. That is the line: a test
that touches Terminal.Gui needs `BeginInit`/`EndInit`, a layout pass and the
process-wide statics that force parallelisation off, so it belongs on the other
side of the fence from the pure ones. It carries the same subfolders as
`Marbas/Ui` — all but `Messages/`, whose modules are pure text and never build a
view — so a feature's unit tests and its Terminal.Gui tests sit at matching
paths on either side.

The split is what makes a file like `State/DeleteTests.fs` (does the panel come
back right once the entries are gone?) readable next to
`Integration/App/DeleteKeyTests.fs` (does `F8` confirm and then reach the trash?) —
they used to be one file. Either half
runs on its own:

```
dotnet test --filter "FullyQualifiedName~Marbas.Tests.Integration"
```

Six modules sit outside the folders because everything uses them: `Root` (the
platform's root, and paths built on it), `WindowsFact` and `UnixFact` (the `Fact`
that skips where drive letters do not exist, and the one that skips where they
do), `Expect` (result unwrapping), `Fakes` (the
do-nothing `Shell.Dependencies`) and `AssemblyInfo`. `Integration/Gui.fs` adds the view helpers — `focusedWindow`,
`leftList`, `rightList`, `press`, `panel`, `panelTitle`, `shownPath`, which
is the path part of a panel title, so a navigation test says which directory a
panel shows without repeating the sort indicator that follows it, and
`drawnText`, which is the text a view formats for the screen rather than the
text it was handed. `pressKey`
sends a key to the panel that has focus — since the bottom bar stopped binding
its keys application-wide, a function key only arrives if it is sent where the
user's keystroke would go. F# does not open an enclosing
namespace for you, so a file under `State/` that uses `Expect.ok` needs an
explicit `open Marbas.Tests`.

## Arrange, act, assert

Every test is three blocks separated by a blank line — set the world up, do the
one thing under test, then assert. There are no `// Arrange` comments; the blank
lines carry the structure.

The act is a named binding, even when the call would fit inside the assertion:
`let roots = Roots.list fs MountedDrives` followed by `Assert.Equal<FileSystemPath
list>([...], roots)`, never `Assert.Equal<FileSystemPath list>([...],
Roots.list fs MountedDrives)`. Where a test
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
reach the panel `ListView` a test drives. `pressReturning` is `press` that hands
the key back, for the tests whose subject is whether it was handled.

## Injecting the outside world

`MainWindow.create` takes a `Shell.Dependencies` record — file system, start
path, settings path, quit callback, error reporter, root chooser, pattern
prompt, name prompt, confirmation, overwrite question, batch runner, file
viewer, editor and modifier watch.
Everything that would open a modal dialog, start a process or end the process
is a function, so tests pass fakes and assert on what was requested.
`Adapters/Prompts.fs` is the only place that supplies the real `MessageBox` dialogs.
`Fakes.dependencies` gives every port a do-nothing default — a
confirmation that answers no, an overwrite question that skips, prompts that
cancel — so a test only overrides the one it cares about. A prompt that carries
a suggestion (`AskTarget`) is easiest to fake as `fun _ suggestion -> Some
suggestion`, which is exactly "the user pressed Ok".

**Background work is a port, so tests stay synchronous.** `RunBatch` is the
dependency that copies and moves go through; `Fakes` supplies one that
runs the work inline with `Operations.unattended`, so a test presses `F5` and
finds the files already copied when the call returns. Nothing in the suite
waits on a thread. What that does *not* cover is `BatchRunner` — the real
runner, its progress dialog and the `Application.Invoke` marshalling are only
ever exercised by running the app.

**Held modifiers are a port too.** `WatchModifiers` is handed a callback at
startup; a test keeps the callback and calls it with the modifiers it wants
held, which is how `Integration/Keys/ModifierRowTests.fs` presses no key at all and
still asserts what the bottom bar shows. The polling itself lives in
`ModifierWatch` and is only exercised by running the app.

**An external program is a port too.** `RunEditor` is what `F4` goes through,
so a test hands it a function that records the path — and, where the point is
the refresh that follows, one that writes into the `MockFileSystem` before
answering `Ok`. Nothing in the suite starts a process.

**The settings file is not a port.** `SettingsPath` is a plain path on the
dependencies and `Settings.load`/`Settings.save` use the injected file system, so
a test points the path into its own `MockFileSystem` and the real config file is
never touched. That is what lets `Integration/App/SettingsTests.fs` state the whole
promise of P7.4 in one test: build a window, sort it, show its hidden entries,
send another panel to a second drive, press `Ctrl+Q`, then build a second window
over the *same* mock file system and read the titles back.

**A failure is arranged on the file, not faked around it.** `MockFileData` is
what makes the error paths testable: `Attributes <- FileAttributes.ReadOnly`
makes a delete or a write throw `UnauthorizedAccessException`, which is the
`AccessDenied` case, and `AllowedFileShare <- FileShare.None` makes every open
throw `IOException`, which is the unexpected-failure case that carries the OS
message. That is how "delete refuses a read-only file", "the viewer reports a
file it cannot open" and "an unreadable settings file falls back to the
defaults" are ordinary tests rather than mocks of the file system interface.

**What `MockFileData` cannot arrange is subclassed, not mocked.** Two failures
have no representation on a file: a directory the operating system will not
list, and a disk that refuses every write. `Fakes.UnreadableDirectory` and
`Fakes.FullDisk` are `MockFileSystem` subclasses that override one member each
— `MockDirectory.EnumerateFileSystemEntries` to raise
`UnauthorizedAccessException` for one named path, and `MockFile.Create` to hand
back a stream that raises `IOException` on every write — and are otherwise the
mock file system the rest of the suite uses, so the arrange block still says
`AddFile` and `AddDirectory`. They are what proves that a copy reports a
directory it may not read instead of throwing past its batch, and that an
archive that cannot be finished is deleted rather than left unreadable. Anything
`MockFileData` *can* express belongs on the file: these two exist because
nothing else reaches those code paths.

A UI test that needs the two panels on *different* directories gets there the
way a user would: `press list KeyMap.rightRootKey` with a `ChooseOption`
that answers with the wanted path. No test reaches into the state to set it.

## Terminal.Gui pitfalls

**`Key` is mutable and `Handled` sticks.** Sending a shared `Key` instance
(such as `KeyMap.refreshKey`) through `NewKeyDownEvent` sets `Handled` on
that object permanently; every later send of the same instance is ignored and
handlers stop firing. Use `Gui.press`, which sends a fresh copy.
The symptom of getting this wrong is a test that passes alone but fails as
part of the suite.

**Keys do not bubble.** `NewKeyDownEvent` on a nested view is not forwarded to
its parent, so key handlers are attached to the view that actually holds focus
— the panel's `ListView`.

**`View.AddCommand` is `protected`.** Custom shortcuts use `KeyDown` handlers
instead.

**Application-level bindings need the running app.** Defaults such as
Tab → `NextTabStop` and `Command.Quit` are not exercised by
`NewKeyDownEvent` in unit tests. Assert the binding
(`Application.GetDefaultKey`) and the resulting behaviour (`AdvanceFocus`)
separately.

**A `Dialog` is much smaller inside than out.** Border, button row and padding
eat six rows, so `Height = 7` leaves a one-row content area and anything placed
below it is clipped — while still taking focus and keys, which looks like an
invisible but typable field. Dialogs are built in `Ui` modules, not inline in
the adapter that runs them, so a test can lay one out and assert its views fit
inside `dialog.Viewport`.

**`ListView` type-ahead swallows printable keys.** `ListView.OnKeyDown` runs
before the `KeyDown` event and hands printable characters to its
`KeystrokeNavigator`, which reports a match even when nothing matches — so
`+` and `-` never reached Marbas until the panel set
`KeystrokeNavigator <- null`. The symptom is `NewKeyDownEvent` returning `true`
with `key.Handled` still `false`.

**StatusBar shortcuts swallow their keys.** A `Shortcut` marks its key handled
even with a `null` action, which is why every shortcut that should do
something gets a real callback. That is the reason the bottom bar stopped being
a `StatusBar` of `Shortcut`s: a plain `View` of slot views takes no key at all,
and a test asserts it by sending `F5` to the bar and finding it unhandled.

**A mouse click is raisable from a test.** `View.NewMouseEvent` is public and
takes a `Terminal.Gui.Input.Mouse`, so
`slot.NewMouseEvent(Mouse(Flags = MouseFlags.LeftButtonClicked))` is a click on
a bar slot — no driver, no layout pass, no `Position`. That is what covers the
one input the bottom bar owns.

**A key that escapes a view can only be caught as `Handled`.** Focus navigation
runs above `NewKeyDownEvent`, so a test cannot watch `↓` at the last entry move
to the other panel — what it can assert is that the panel marked the key
handled, which is the whole mechanism that keeps it in. `Gui.pressReturning`
sends a fresh key and hands it back for exactly that assertion; `Gui.press` is
it with the answer thrown away.

**`ListView.Viewport.Y` is the list's scroll offset** and `Viewport.Height` the
rows it shows, which is what the page jumps are measured from. Both are zero
until a layout pass, so a test about them lays the panel out first —
`view.Layout(Size(40, 12))` — and one test asserts `Viewport.Y > 0` after
scrolling, so the assumption fails loudly if a future version moves the offset
elsewhere.

**`RowRender` is raisable from a test.** `ListView.OnRowRender` is public, so a
test constructs a `ListViewRowEventArgs row`, raises it and reads
`RowAttribute` back — the one thing about colour that is observable without a
driver. `null` there means "leave the row alone", which is what a marked row and
the row under the cursor of a focused list must get.

**`View.Text` is what was set, not what is drawn.** A `Label` that eats an
underscore as its hot key specifier still hands the underscore back from `Text`,
so an assertion there cannot see the bug at all. `Gui.drawnText` reads
`view.TextFormatter.GetLines()` instead, which is the formatted text with the
specifier already removed — the closest a driverless test gets to the screen.
`TitleTextFormatter` is not public, so the panel frame is covered the other way
round: a title carrying an underscore must leave `view.HotKey` empty. Both need
a layout pass first, and the underscore has to be followed by a character —
a trailing `_` is not a specifier and survives untouched.

**`RequestStop` does nothing outside a run.** `Runnable.RequestStop` goes
through the application that is running the dialog, so a dialog built in a test
never sets `StopRequested` — not even after `SetIsRunning true`. A view that
has to close itself therefore takes the closing action as an argument
(`FileViewer.create close …`), which a test can record and `Prompts.showFile` fills
in with `_.RequestStop()`.

**A view that paints by hand is asserted through what it would paint.** There
is no driver in a test, so nothing can read the screen back. `FileViewer`'s
content view therefore computes its rows in one place — `Rows()` — and
`OnDrawingContent` only paints what that answers. A test calls `Rows()` and
compares strings, which is how scrolling, wrapping, the hex switch and a
UTF-16 file are all covered without a terminal. A view that painted straight
from its own state inside the override would be untestable here.

**Colours are asserted as schemes, never as pixels.** There is no driver in a
test, so nothing can read back what was painted. What a test can assert is the
`Scheme` a view resolves to — `view.GetScheme()` — and the attributes the scheme
holds, which is what `Integration/Theme/SchemeTests.fs` and `Ui/Theme/ClassicSchemeTests.fs`
do. A view without a scheme of its own inherits its super-view's, so the panel
list and the status line are covered by the schemes set on the panel frame and
on the window. It is also why the bottom bar draws a key name and its command as
two `Label`s rather than as one line in two attributes: two views are two
schemes to read back, one hand-painted line is nothing at all.

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

## Seeing the screen

What no assertion reaches — a frame, a shadow, a glyph a font may not carry, one
attribute against another — can still be read rather than guessed. A throwaway
console project in the scratchpad, referencing `Marbas.fsproj`, runs the real
view under `Application.Create().Init()` with a piped stdout and prints
`Driver.ToString()` as text and `Driver.Contents[row, col]` as colours; a
timeout injects keys and takes the shot from inside the loop. The popups of the
`DialogStyle` round were settled that way, before and after side by side. It
belongs beside the suite rather than in it: it needs a driver, it is slow, and
what it proves is a picture. The `render-tui-screen` skill in `.claude/skills`
carries the harness and the traps — chiefly that the screen measures 0x0 when
stdout is piped, which makes `MessageBox` throw until it is set.
