# Architecture overview

Dependencies flow downward only: `Adapters` → `Ui` → `State` → `FileSystem`.
`AppState` transitions are pure; the generic `State/Runtime.fs` host holds the
only mutable cell and drives the update/render/effect loop. The outside world
(file system, dialogs, quit) is injected through `Shell.Dependencies` and run
by `Shell.interpret`; `Adapters/` holds the real implementations of those ports
and `Adapters/Dependencies.fs` is the one place that wires them.

**Terminal.Gui 2.4 over Spectre.Console and Consolonia.** Only Terminal.Gui is
a full TUI framework (windows, focus, event loop) with a large, active
community and an MIT licence. Its v2 API is instance-based
(`Application.Create()`); the static `Application` model is deprecated.

**Test first.** Every task started with failing tests. Logic modules are pure
and take `IFileSystem`, so tests run against `MockFileSystem` and never touch
the disk. See [Testing](TESTING.md) for how the suite itself is organised.

**No code comments.** Intent lives in names, tests and these documents.

**`Ui` is one layer in eight folders.** Forty-odd modules under a single folder
stopped being navigable, so they are grouped by what they are about — theme,
formatting, keys, messages, panels, viewer, dialogs, and the application's own
composition root — and each folder is a namespace: `Marbas.Ui.Panels.FilePanel`.
The folder order in the code map below is also the compile order, and every
dependency inside `Ui` points at a folder listed above it, which is what keeps
the layer acyclic without any tooling. `Ui` is still one layer of `Adapters` →
`Ui` → `State` → `FileSystem`; the folders divide it by subject, not by rank.

**`Adapters` is where the operating system is allowed in.** `Shell.Dependencies`
declares the ports; every real implementation of one lives in `Adapters/`, and
nothing below that folder opens `System.Diagnostics`, imports `user32.dll` or
asks which operating system it is running on. The answer to that last question
is read once, by `HostEnvironment.current`, and carried as a value: `Platform`,
the two home directories, `$EDITOR`. `Dependencies.create` reads it and picks a
Windows or a Unix function per port — `RecycleBin.send` or `Trash.send`,
`ModifierWatch.poll` or `ModifierWatch.unsupported`, `MountedDrives` or
`RootAndHome` — so no branch on the platform survives below the composition
root. `Program.fs` is left with the handful of lines that start the application.

## Topics

- [Domain types](DOMAIN-TYPES.md) — paths as a type, typed messages and effects
- [State and effects](STATE-AND-EFFECTS.md) — the update loop, dependency injection, composition
- [File operations](FILE-OPERATIONS.md) — copy/move/delete/rename, batches, viewer and editor
- [Archives](ARCHIVES.md) — kinds and formats, the deduced name, writing, reading, extracting, the libraries
- [UI and input](UI-AND-INPUT.md) — colour, sorting, hidden entries, search, the key table
- [Settings persistence](SETTINGS-PERSISTENCE.md) — the config file and what survives a restart
- [Testing](TESTING.md) — suite layout, conventions, Terminal.Gui pitfalls
- [Packaging](PACKAGING.md) — publish profiles and the release pipeline
- [Cross-platform](CROSS-PLATFORM.md) — what has to hold on Windows and Linux, and why

## Code map

```
Marbas/
  Platform.fs             which operating system this is, as a value
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
    ArchiveKind.fs        the archive kinds, their extensions and what can be done to each
    ArchivePath.fs        a place inside an archive: never rooted, never climbing
    ArchiveFormat.fs      which kinds can be created, and what each one carries
    ArchiveName.fs        the name an archive is offered under, and its extension
    ArchiveReader.fs      opening an archive: its entries, their content, failures as values
    ArchiveListing.fs     reading one level of an archive as the entries a directory gives
    Archive.fs            writing one and taking one apart: the walk, cancelling, the password
    Trash.fs              the freedesktop trash: where it is, and how an entry goes in
    ContentError.fs       what can go wrong reading a file, as values
    TextEncoding.fs       which encoding a file carries, from its mark or its bytes
    FileWindow.fs         a file held open, read one byte range at a time
    LineIndex.fs          line starts scanned forward on demand, and the line at one
  State/
    AppState.fs           immutable panel/app state and its transitions,
                          built from and read back as Settings
    Update.fs             messages and effects: message + state -> state + effects
    Runtime.fs            generic host: the single mutable cell, dispatch, render
  Ui/                     eight folders, compiled in this order; every
                          dependency points at a folder listed above it
    Theme/                the look, applied before any view is built
      ClassicScheme.fs      the five schemes the screen is painted with
      DialogStyle.fs        frame, buttons, glyphs and schemes every popup wears
    Format/               the text more than one feature formats with
      LiteralText.fs        turns a view's hot key specifier off, so `_` is shown
      EntrySize.fs          size column text, byte format injected per view
      EntryFormat.fs        one entry -> one fixed-width row
      TimestampText.fs      one moment <-> the text a dialog reads and writes back
    Keys/                 what a keystroke means, and the bar that names them
      Modifiers.fs          Ctrl / Alt / Shift as a set, the set a key carries,
                            and the key a set makes of a plain one
      KeyMap.fs             every key binding: key, bar label, message; the twelve
                            bar slots of a set of modifiers; the shifted page keys,
                            which only carry a direction
      KeyBarLayout.fs       bar width -> where each of the twelve slots sits
      FunctionKeyBar.fs     bottom bar: a slot per function key, F1 to F12
    Messages/             the words the application says, from values it holds
      ListingErrorMessage.fs    ListingError -> user-facing text
      ContentErrorMessage.fs    ContentError -> user-facing text
      ArchiveErrorMessage.fs    ArchiveError -> user-facing text
      OperationErrorMessage.fs  OperationError (and failed items) -> user-facing text
      DeleteConfirmation.fs     delete targets -> confirmation question
      OverwriteQuestion.fs      collision -> question text, answer buttons and mapping
      PasswordQuestion.fs       protected archive -> the words of the password prompt
    Panels/               the two panels and everything drawn inside one
      EntryColor.fs         one entry -> its colour group, and that group's attribute
      PageJump.fs           where a page key lands, from the page on screen
      SelectionSummary.fs   marked entries -> panel summary text
      PanelFooter.fs        panel state -> the one line under the rule
      PanelTitle.fs         path and sort order -> the panel frame's title
      FilePanel.fs          panel views, render state -> view
      PanelWiring.fs        one panel's list bound to keys, cursor and search
    Viewer/               the full-screen viewer behind `F3`
      HexDump.fs            bytes at an offset -> one offset / hex / text row
      ViewerMode.fs         text or hex, wrapped or not
      PrintableText.fs      a decoded line -> what a terminal can show of it
      LineWrap.fs           a line -> the rows it takes, or the window shown of it
      ViewerScroll.fs       clamped scrolling, how many lines fill a page, percent
      ViewerHeader.fs       name, mode, encoding, size, place -> the viewer's top bar
      ViewerKeys.fs         the viewer's own key table and its twelve bar labels
      FileViewer.fs         the viewer itself: header, rows, bar
    Dialogs/              the modal popups, each one built where a test can lay it out
      PromptDialog.fs       single-field text prompt (label + default), plain or secret
      PatternDialog.fs      the `+` / `-` pattern prompt, over PromptDialog
      ProgressDialog.fs     the window shown while a batch runs
      AssociationRow.fs     one extension and its editor -> one row of the list
      PropertiesDialog.fs   what `F4` shows for a directory, and writes back
      SettingsDialog.fs     the default editor and the extension associations
      PackDialog.fs         what `Shift+F1` asks before an archive is written
    App/                  the composition root, and the way out to the world
      CursorInfo.fs         cursor entry -> status line text
      StatusLine.fs         the status line view
      AppView.fs            window, both panels and the status line in one record
      Shell.fs              Dependencies and the effect interpreter
      EditorCommand.fs      which editor `F4` starts, per platform and `$EDITOR`
      MainWindow.fs         composition: views, runtime, event wiring
  Adapters/               the real implementations of Shell.Dependencies;
                          the only folder allowed to touch the OS
    HostEnvironment.fs    platform, home directories and `$EDITOR`, read once
    Prompts.fs            the message boxes and popups the application runs
    RecycleBin.fs         the Windows recycle bin, through the VB runtime
    EditorProcess.fs      the editor started as a child process, and waited on
    BatchRunner.fs        a batch on a background task under the progress dialog
    ModifierWatch.fs      held Ctrl / Alt / Shift, polled on Windows, absent elsewhere
    Dependencies.fs       every port bound to one implementation, platform picked here
  Program.fs              quit key, application, theme, dependencies, window
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
  Ui/                     unit tests of the pure Ui modules and the Shell
                          interpreter, in the same eight folders as the source
  Integration/            every test that constructs a Terminal.Gui view, in
                          those same folders
    Gui.fs                 focusedWindow, leftList, rightList, press, panel,
                          panelTitle, shownPath, drawnText

coverlet.runsettings      what coverage measures and what it leaves out
.github/workflows/
  ci.yml                  restore, build and test on Windows and Linux
  release.yml             publish both profiles on a tag, attach them to a release
```
