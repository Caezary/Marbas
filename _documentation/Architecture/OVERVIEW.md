# Architecture overview

Dependencies flow downward only: `Ui` → `State` → `FileSystem`. `AppState`
transitions are pure; the generic `State/Runtime.fs` host holds the only
mutable cell and drives the update/render/effect loop. The outside world
(file system, dialogs, quit) is injected through `Shell.Dependencies` and run
by `Shell.interpret`; `Program.fs` is the only place wiring the real ones.

**Terminal.Gui 2.4 over Spectre.Console and Consolonia.** Only Terminal.Gui is
a full TUI framework (windows, focus, event loop) with a large, active
community and an MIT licence. Its v2 API is instance-based
(`Application.Create()`); the static `Application` model is deprecated.

**Test first.** Every task started with failing tests. Logic modules are pure
and take `IFileSystem`, so tests run against `MockFileSystem` and never touch
the disk. See [Testing](TESTING.md) for how the suite itself is organised.

**No code comments.** Intent lives in names, tests and these documents.

## Topics

- [Domain types](DOMAIN-TYPES.md) — paths as a type, typed messages and effects
- [State and effects](STATE-AND-EFFECTS.md) — the update loop, dependency injection, composition
- [File operations](FILE-OPERATIONS.md) — copy/move/delete/rename, batches, viewer and editor
- [UI and input](UI-AND-INPUT.md) — colour, sorting, hidden entries, search, the key table
- [Settings persistence](SETTINGS-PERSISTENCE.md) — the config file and what survives a restart
- [Testing](TESTING.md) — suite layout, conventions, Terminal.Gui pitfalls
- [Packaging](PACKAGING.md) — publish profiles and the release pipeline
- [Cross-platform](CROSS-PLATFORM.md) — what has to hold on Windows and Linux, and why

## Code map

```
Marbas/
  FileSystem/
    EntryName.fs          single-case type for the name of one entry
    FileSystemPath.fs     single-case path type wrapping a raw string
    SortOrder.fs          which key a panel sorts by, in which direction
    HiddenEntries.fs      what makes an entry hidden, and whether a panel lists it
    Executability.fs      what makes an entry a program, by name and by mode bit
    SearchText.fs         the text a panel is being searched for, and what it matches
    Settings.fs           the session both panels carry, and its config file
    DirectoryListing.fs   entries, ordering, ".." handling, errors as values
    Roots.fs              where a panel can be sent: drives, or root and home
    NamePattern.fs        wildcard matching for `+` / `-`
    Operations.fs         copy / move / rename / delete / mkdir, errors as values
    ContentError.fs       what can go wrong reading a file, as values
    TextEncoding.fs       which encoding a file carries, from its mark or its bytes
    FileWindow.fs         a file held open, read one byte range at a time
    LineIndex.fs          line starts scanned forward on demand, and the line at one
  State/
    AppState.fs           immutable panel/app state and its transitions,
                          built from and read back as Settings
    Update.fs             messages and effects: message + state -> state + effects
    Runtime.fs            generic host: the single mutable cell, dispatch, render
  Ui/
    ClassicScheme.fs      the five schemes the screen is painted with
    LiteralText.fs        turns a view's hot key specifier off, so `_` is shown
    Modifiers.fs          Ctrl / Alt / Shift as a set, the set a key carries,
                          and the key a set makes of a plain one
    KeyMap.fs             every key binding: key, bar label, message; the twelve
                          bar slots of a set of modifiers; the shifted page keys,
                          which only carry a direction
    KeyBarLayout.fs       bar width -> where each of the twelve slots sits
    EntrySize.fs          size column text, byte format injected per view
    EntryFormat.fs        one entry -> one fixed-width row
    EntryColor.fs         one entry -> its colour group, and that group's attribute
    PageJump.fs            where a page key lands, from the page on screen
    SelectionSummary.fs   marked entries -> panel summary text
    PanelFooter.fs         panel state -> the one line under the rule
    CursorInfo.fs           cursor entry -> status line text
    StatusLine.fs           the status line view
    HexDump.fs             bytes at an offset -> one offset / hex / text row
    ViewerMode.fs           text or hex, wrapped or not
    PrintableText.fs        a decoded line -> what a terminal can show of it
    LineWrap.fs             a line -> the rows it takes, or the window shown of it
    ViewerScroll.fs         clamped scrolling, how many lines fill a page, percent
    ViewerHeader.fs         name, mode, encoding, size, place -> the viewer's top bar
    ListingErrorMessage.fs  ListingError -> user-facing text
    ContentErrorMessage.fs  ContentError -> user-facing text
    OperationErrorMessage.fs  OperationError (and failed items) -> user-facing text
    DeleteConfirmation.fs delete targets -> confirmation question
    OverwriteQuestion.fs  collision -> question text, answer buttons and mapping
    PanelTitle.fs           path and sort order -> the panel frame's title
    FilePanel.fs             panel views, render state -> view
    FunctionKeyBar.fs       bottom bar: a slot per function key, F1 to F12
    ViewerKeys.fs           the viewer's own key table and its twelve bar labels
    Shell.fs                 Dependencies and the effect interpreter
    PromptDialog.fs         single-field text prompt (label + default)
    PatternDialog.fs         the `+` / `-` pattern prompt, over PromptDialog
    ProgressDialog.fs       the window shown while a batch runs
    FileViewer.fs             the full-screen viewer behind `F3`: header, rows, bar
    EditorCommand.fs        which editor `F4` starts, per platform and `$EDITOR`
    MainWindow.fs             composition: views, runtime, event wiring
  Program.fs              real file system and real dialogs
  Properties/PublishProfiles/
    win-x64.pubxml        self-contained single-file build for Windows
    linux-x64.pubxml      the same for Linux

Marbas.Tests/
  Root.fs                 the root every test path is built on, per platform
  WindowsFact.fs          a Fact that skips where drive letters do not exist
  Expect.fs               Result unwrapping, so act and assert stay apart
  Fakes.fs                the do-nothing Shell.Dependencies every test starts from
  FileSystem/             unit tests, one module per FileSystem module
  State/                  unit tests, one module per State module
  Ui/                     unit tests of the pure Ui modules and the Shell interpreter
  Integration/            every test that constructs a Terminal.Gui view
    Gui.fs                 focusedWindow, leftList, rightList, press, panel,
                          panelTitle, shownPath, drawnText

coverlet.runsettings      what coverage measures and what it leaves out
.github/workflows/
  ci.yml                  restore, build and test on Windows and Linux
  release.yml             publish both profiles on a tag, attach them to a release
```
