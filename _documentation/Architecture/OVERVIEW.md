# Architecture overview

Dependencies flow downward only: `Adapters` → `Ui` → `State` → `Messages` →
`FileSystem` → `Domain`. `AppState` transitions are deterministic functions of
the state and
the `IFileSystem` they are given, and `IFileSystem` is the only port they may
reach: every other way out to the world — a dialog, the trash, a process, the
clock of a watcher — is an `Effect` the interpreter carries out. Work that can
take longer than a keystroke is an effect as well, so it runs off the UI thread
behind the progress window; [State and effects](STATE-AND-EFFECTS.md) draws that
line where the code draws it. The generic `State/Runtime.fs` host holds the only
mutable cell and drives the update/render/effect loop. The outside world (file
system, dialogs, quit) is injected through `Ports.Dependencies` — five
sub-records, `Prompts`, `Processes`, `Clipboard`, `Watches` and `Runners` — and
run by `Shell.interpret`; `Adapters/` holds the real implementations of those
ports and `Adapters/Dependencies.fs` is the one place that wires them.

**`State` holds the ports and the interpreter, below the line its transitions
cannot see past.** Neither `Ports` nor `Shell` draws anything — one is the
shape of the outside world, the other turns an `Effect` into a call on it — so
both live in `State`, with `Startup`, which decides how a session begins.
Nothing under `State/` opens
a `Terminal.Gui` namespace or a `Marbas.Ui` one. The rule that a transition
reaches no port but `IFileSystem` is now held by the compile order inside the
layer, which runs in two steps:

1. `PanelState`, `AppState`, `CommandExpansion`, `Update`, `LogEntry`,
   `Runtime` — the state, its transitions and the host. Nothing here can name a
   port: `Ports` is not compiled yet.
2. `Ports`, `Shell`, `Startup` — the outside world, the interpreter that
   carries an effect out into it, and the first steps of a session.

`Ui` sits on both and only draws and wires: a view may read a message's
wording or take a port's request record, and `MainWindow` hands `Shell.interpret`
to the runtime, but no view decides what an effect does.

**The wording is a layer of its own, between `FileSystem` and `State`.**
`Messages/` holds the words the application says — what an error means in a
sentence, what a question asks — as functions from a value to text, with no
view in them. Its readers are `Update`, which names what an effect reports,
`Shell`, which says the words, two adapters and two views, so it has to sit
below all of them; it cannot sit above `State`, because `Shell` writes the
worded text to the log. And it reads nothing of
`State`: every value it words is one `FileSystem` or `Domain` speaks. Two of
them were declared higher only because that is where they were first needed —
what a delete asks to have confirmed and why a command line was not expanded —
and moved down to `Domain/DeleteRequest.fs` and
`FileSystem/ExpansionRefusal.fs` when the layer was made. `Problem` is the
layer's one type: everything that is reported to the user, each case carrying
the error it reports, with its title and its description answered by one
module. [State and effects](STATE-AND-EFFECTS.md) says why it is a union.

**Terminal.Gui 2.4 over Spectre.Console and Consolonia.** Only Terminal.Gui is
a full TUI framework (windows, focus, event loop) with a large, active
community and an MIT licence. Its v2 API is instance-based
(`Application.Create()`); the static `Application` model is deprecated.

**Test first.** Every task started with failing tests. Logic modules take
`IFileSystem`, so tests run against `MockFileSystem`; only the handful under
`Integration/Disk/`, whose subject is what a real system does, touch the disk.
See [Testing](TESTING.md) for how the suite itself is organised.

**No code comments.** Intent lives in names, tests and these documents.

**`Ui` is one layer in eight folders.** Forty-odd modules under a single folder
stopped being navigable, so they are grouped by what they are about — theme,
formatting, keys, panels, viewer, dialogs, the search results, and the
application's own composition root — and each folder is a namespace:
`Marbas.Ui.Panels.FilePanel`.
The folder order in the code map below is also the compile order, and every
dependency inside `Ui` points at a folder listed above it, which is what keeps
the layer acyclic without any tooling. `Ui` is still one layer of `Adapters` →
`Ui` → `State` → `Messages` → `FileSystem` → `Domain`; the folders divide it by
subject, not by rank.

**`Adapters` is where the operating system is allowed in.** `State/Ports.fs`
declares the ports; every real implementation of one lives in `Adapters/`, and
nothing below that folder opens `System.Diagnostics`, imports `user32.dll` or
asks which operating system it is running on. The answer to that last question
is read once, by `HostEnvironment.current`, and carried as a value: `Platform`,
the two home directories, `$EDITOR`. `Dependencies.create` reads it and picks a
Windows or a Unix function per port — `RecycleBin.send` or `Trash.send`,
`ModifierWatch.poll` or `ModifierWatch.unsupported`, the drives alone or the
drives and the home directory — so no branch on the platform survives below the composition
root. `Program.fs` is left with one line: it hands the real file system and
environment to `Session.run`, which starts the application under the crash
hooks described in [Diagnostics](DIAGNOSTICS.md).

## Topics

- [Domain types](DOMAIN-TYPES.md) — paths as a type, typed messages and effects
- [State and effects](STATE-AND-EFFECTS.md) — the update loop, dependency injection, composition
- [File operations](FILE-OPERATIONS.md) — copy/move/delete/rename, wiping, measuring, batches, viewer and editor
- [Links](LINKS.md) — the three kinds, which system makes which, why every walk stops at a link
- [Finding files](FINDING-FILES.md) — the query, matching text, the scopes, the results window, the results panel
- [Archives](ARCHIVES.md) — kinds and formats, the deduced name, writing, reading, extracting, the libraries
- [UI and input](UI-AND-INPUT.md) — colour, sorting, hidden entries, search, popups and the drive popup, the key table
- [Settings persistence](SETTINGS-PERSISTENCE.md) — the config file and what survives a restart
- [Diagnostics](DIAGNOSTICS.md) — the log, what writes to it, and what a crash leaves behind
- [Tabs](TABS.md) — a tab as a pair of panels, the zipper, switching, the counter
- [History](HISTORY.md) — what is remembered, where it is kept, the popup and the drop-down fields
- [Auto-refresh](AUTO-REFRESH.md) — watching the directories on screen, the burst, what the watcher cannot say
- [Testing](TESTING.md) — suite layout, conventions, Terminal.Gui pitfalls
- [Packaging](PACKAGING.md) — publish profiles and the release pipeline
- [Cross-platform](CROSS-PLATFORM.md) — what has to hold on Windows and Linux, and why

## Code map

```
Marbas/
  Collections/            data structures below every layer, tied to nothing
    Zipper.fs             a non-empty list with one item in front: the tabs
  Domain/                 the words the whole application speaks, no System.IO in them
    Platform.fs           which operating system this is, as a value
    PanelSide.fs          left or right, needed as low as the settings file
    PanelCursor.fs        a side and the row it points at, as one value
    TabStep.fs            forward or backward, for the key that walks the tabs
    EntryName.fs          single-case type for the name of one entry
    SortOrder.fs          which key a panel sorts by, in which direction
    HiddenEntries.fs      what makes an entry hidden, and whether a panel lists it
    Executability.fs      what makes an entry a program, by name or by mode bit alone
    EntryVerification.fs  whether a listed entry was checked, or stands for a hit nothing could check
    SearchText.fs         the text a panel is being searched for, and what it matches
    NamePattern.fs        wildcard matching for `+` / `-`
    SelectionMode.fs      marking or unmarking, instead of a boolean
    CursorDirection.fs    up or down, for a mark that moves the cursor with it
    DeleteScope.fs        whether a delete reaches into a directory's contents
    DeleteMode.fs         to the trash, or past it
    DeleteRequest.fs      the entries a delete names, how deep it reaches and how it removes
    DirectorySize.fs      a measured size, exact or a lower bound
    LinkKind.fs           symbolic, hard or junction, and why a kind is refused
    DriveKind.fs          fixed, removable, network or optical, not `System.IO.DriveType`
    ProgramSettings.fs    extension and role -> the program that edits or opens it
    FindQuery.fs          what a search asks: masks, text, scope, options, and what a file answers
    SearchPattern.fs      a regular expression that parsed for the linear engine, or why not
    WantedBytes.fs        the bytes the hex field asks for, read from pairs of hex digits, or why not
    ResultsRefusal.fs     which command a panel of search results cannot carry out
    EnteredText.fs        what was typed into a field, kept exactly, never blank, never two lines
    MenuHotkey.fs         the one letter or digit that runs a user menu entry, kept in capitals
    MenuLabel.fs          the name a user menu entry is shown under
    CommandLine.fs        what a user menu entry runs, not yet expanded, and how it is started
    Modifier.fs           Ctrl, Alt or Shift, as the ports and the key table both name them
  FileSystem/
    FileSystemPath.fs     single-case path type wrapping a raw string
    EntryAttributes.fs    what FileAttributes and UnixFileMode say, in the Domain's words
    LaunchRequest.fs      what to start, with what, where, and with which rights
    ProgramImage.fs       the subsystem a program's PE header names: window, console, other, none
    CommandRun.fs         a user menu command as expanded, and how and where it ran
    ExpansionRefusal.fs   why a user menu command line was not expanded
    Handler.fs            a program the system offers for a name, and its display name
    ProgramCommand.fs     a command line -> the program it starts
    EditorCommand.fs      which editor `F4` starts, per platform and `$EDITOR`
    DesktopEntry.fs       one .desktop file -> the handler it describes
    MimeDatabase.fs       globs -> mime types, mimeapps.list -> desktop entries
    DesktopHandlers.fs    the XDG directories -> the handlers a name has
    Settings.fs           the session every tab carries, and its config file
    DirectoryListing.fs   entries, ordering, ".." handling, errors as values
    Drives.fs             where a panel can be sent: the usable drives, and home on Unix
    PanelLocation.fs      where a panel stands: a directory, an archive level, search results;
                          a hit and the ground a search covers
    Place.fs              a directory or file on disk, or a place inside an archive
    HistoryError.fs       why the history could not be read or written
    History.fs            the remembered lists, the changes made to them, and history.conf
    UserMenuError.fs      why the user menu could not be read, written or changed
    UserMenu.fs           the user menu entries, the changes made to them, and user-menu.conf
    LogFileError.fs       why the log could not be cut or written
    LogFile.fs            marbas.log beside the settings: appended to, cut to its last megabyte
    Operations.fs         copy / move / rename / delete / mkdir, errors as values
    Links.fs              what a link request means, what refuses it, and making one
    DirectoryMeasure.fs   the size of a directory or an archive level, stopping at links
    ContentMatch.fs       whether a file holds a text, read a buffer at a time
    FindGround.fs         a scope -> the places to walk and the roots not to cross
    FileSearch.fs         the walk: hits reported as found, links followed once
    FindAnswer.fs         what the results window answers, and the last results kept
    ArchiveKind.fs        the archive kinds, their extensions and what can be done to each
    ArchivePath.fs        a place inside an archive: never rooted, never climbing
    ArchiveFormat.fs      which kinds can be created, and what each one carries
    ArchiveName.fs        the name an archive is offered under, and its extension
    ArchiveReader.fs      opening an archive: its entries folded over, one entry as a stream
    ArchiveListing.fs     reading one level of an archive as the entries a directory gives
    SearchListing.fs      search hits as the entries a directory gives, named by their path
    SessionOwner.fs       the process a working directory belongs to: its id and the
                          moment it started, and the two lines they are written as
    Workspace.fs          the session's working directory: claimed, scratch directories
                          in it, released; what earlier sessions left behind swept
    Archive.fs            writing one and taking one apart: the walk, cancelling, the password
    Trash.fs              the freedesktop trash: where it is, and how an entry goes in
    ContentError.fs       what can go wrong reading a file, as values
    TextEncoding.fs       which encoding a file carries, from its mark or its bytes,
                          and the code pages it can be read in
    RewindingStream.fs    a forward-only stream seen as one that seeks, by opening it again
    FileWindow.fs         a file held open, read one byte range at a time
    LineIndex.fs          line starts scanned on demand, one in 64 kept, and the line at an offset;
                          the lines from an offset as a walk, window by window, either way
    WindowSearch.fs       the viewer's search: a text or a pattern, its case and a direction,
                          from a place in a line, stoppable
    ByteSearch.fs         the dump's search: a run of bytes from an offset, either way, stoppable
  Messages/               the words the application says, from values the two
                          layers below it speak
    ListingErrorMessage.fs    ListingError -> user-facing text
    ContentErrorMessage.fs    ContentError -> user-facing text
    ArchiveErrorMessage.fs    ArchiveError -> user-facing text
    LinkRefusalMessage.fs     LinkRefusal -> the one sentence saying why
    OperationErrorMessage.fs  OperationError (and failed items) -> user-facing text
    LaunchErrorMessage.fs     LaunchError -> user-facing text
    ExpansionRefusalMessage.fs  why a user menu command was refused -> user-facing text
    UserMenuErrorMessage.fs     UserMenuError -> user-facing text
    CrashMessage.fs           a crash -> the line printed once the terminal is back
    SettingsErrorMessage.fs   SettingsError -> user-facing text
    HistoryErrorMessage.fs    HistoryError -> user-facing text
    FavoritesErrorMessage.fs  FavoritesError -> user-facing text
    ClipboardErrorMessage.fs  ClipboardError -> user-facing text
    Problem.fs                everything reported to the user -> its title and its description
    DeleteConfirmation.fs     delete targets -> confirmation question
    OverwriteQuestion.fs      collision -> question text, answer buttons and mapping
    PasswordQuestion.fs       protected archive -> the words of the password prompt
  State/
    PanelState.fs         one panel: cursor, marks, search and sort, never the disk
    AppState.fs           tabs and app state: loaders, finish* transitions and the
                          queries Update asks, built from and read back as Settings
    CommandExpansion.fs   a user menu command line + the panels -> the program and its arguments,
                          or why it refuses
    Update.fs             messages and effects: message + state -> state + effects
    LogEntry.fs           what the log records, the panel a message concerns, and the
                          stamped lines each entry becomes, case names only
    Runtime.fs            generic host: the single mutable cell, the message
                          queue, one drain loop over observe/update/render/interpret
    Ports.fs              the ports: prompts, processes, the clipboard, watches,
                          runners and the log
    Shell.fs              the effect interpreter
    Startup.fs            how a session begins: the state the settings restore, the
                          first paths opened, the first watch and the sweep
  Ui/                     eight folders, compiled in this order; every
                          dependency points at a folder listed above it
    Theme/                the look, applied before any view is built
      ClassicScheme.fs      the five schemes the screen is painted with
      DialogStyle.fs        frame, buttons, glyphs and schemes every popup wears
    Format/               the text more than one feature formats with
      LiteralText.fs        turns a view's hot key specifier off, so `_` is shown
      EntrySize.fs          size column text, byte format injected per view
      CompactSize.fs        bytes -> one decimal in the largest unit reached, 931.5 GB
      EntryFormat.fs        one entry -> one fixed-width row
      TimestampText.fs      one moment <-> the text a dialog reads and writes back
      PathText.fs           a path too long for the room -> C:\...\parent\current
      QueryText.fs          a query and where it ran -> the words a title and a summary use
    Keys/                 what a keystroke means, and the bar that names them
      Modifiers.fs          the modifiers as a set, the set a key carries,
                            and the key a set makes of a plain one
      KeyMap.fs             every key binding: key, bar label, message; the twelve
                            bar slots of a set of modifiers; the shifted page keys,
                            which only carry a direction
      KeyBarLayout.fs       bar width -> where each of the twelve slots sits
      FunctionKeyBar.fs     bottom bar: a slot per function key, F1 to F12

    Panels/               the two panels and everything drawn inside one
      EntryColor.fs         one entry -> its colour group, and that group's attribute
      PageJump.fs           where a page key lands, from the page on screen
      SelectionSummary.fs   marked entries -> panel summary text
      PanelFooter.fs        panel state -> the one line under the rule
      PanelTitle.fs         path and sort order -> the panel frame's title,
                            shortened to the width it may use
      FilePanel.fs          panel views, render state -> view
      PanelWiring.fs        one panel's list bound to keys, cursor and search
    Viewer/               the full-screen viewer behind `F3`
      RowBytes.fs           one dump row's bytes, with the three on either side
      HexText.fs            a row's bytes -> its character column, a cell to a byte
      HexDump.fs            bytes at an offset -> one offset / hex / text row, and where each byte stands in it
      HexMark.fs            a run of bytes -> the columns it covers on one row, in both columns
      ViewerMode.fs         text or hex, wrapped or not
      PrintableText.fs      a decoded line -> what a terminal can show of it
      LineWrap.fs           a line -> the rows it takes, or the window shown of it
      ViewerScroll.fs       clamped scrolling, how many lines fill a page, percent
      ViewerHeader.fs       name, mode, encoding, size, place -> the viewer's top bar
      ViewerKeys.fs         the viewer's own key table and its twelve bar labels
      CodePage.fs           Auto or a charset, what it is called and what it reads as
      CodePageDialog.fs     the `F8` popup: Auto and every code page, one to choose
      FoundSpan.fs          a row on screen, and the columns a match covers on it
      ViewerSearch.fs       what `F7` offers, the history it reads, and what it reports
      FileViewer.fs         the viewer itself: header, rows, bar
    Dialogs/              the modal popups, each one built where a test can lay it out
      PromptDialog.fs       single-field text prompt (label + default), plain or secret
      PatternDialog.fs      the `+` / `-` pattern prompt, over PromptDialog
      ProgressDialog.fs     the window shown while a batch runs
      AssociationRow.fs     one extension and its program -> one row of the list
      HandlerRow.fs         one program the system knows -> one row of the list
      PropertiesDialog.fs   what `F4` shows for a directory, and writes back
      SettingsDialog.fs     the default editor and the associations, one list per role
      OpenWithDialog.fs     which program opens this file, and whether to remember it
      PackDialog.fs         what `Shift+F1` asks before an archive is written
      LinkDialog.fs         what `Alt+F6` asks, with the refused kinds greyed out
      TabRow.fs             one tab -> its number and where both its panels stand
      TabDialog.fs          the list `F11` opens, and the tab it answers with
      HistoryRow.fs         a remembered moment and place -> the text of their columns
      HistoryDialog.fs      the popup `Alt+F12` opens: three tabs, and the tab and row it answers with
      DriveLine.fs          drive rows -> lines whose root, label, kind and sizes line up
      DriveDialog.fs        the list `Alt+F1` / `Alt+F2` open, and the row it answers with
      UserMenuRow.fs        a user menu entry -> its hotkey and label, a separator -> a line
      MenuEntryForm.fs      the fields of the entry popup -> an entry, or why it is refused
      MenuRemoval.fs        what `Del` in the user menu may remove, and the change for each
      MenuEntryDialog.fs    the popup `Ins` and `F4` open in the user menu
      UserMenuDialog.fs     the popup `F2` opens, and the entry it answers with
      FindDialog.fs         what `Alt+F7` asks, greyed where the query means nothing
      ViewerSearchDialog.fs what `F7` in the viewer asks: the text and four options, a broken pattern refused
    Find/                 the full-screen results window
      FindKeys.fs           its keys and its twelve bar labels
      FindSummary.fs        the walk so far, or its outcome -> the summary line
      ResultRow.fs          hits -> header and hit rows, grouped by where they live
      ResultsWindow.fs      the window: list, summary, bar, and the answer it gives
    App/                  the screen as a whole and the composition root
      CursorInfo.fs         cursor entry -> status line text
      StatusLine.fs         the status line view
      TabCounter.fs         which tab is in front, of how many, in the corner
      AppView.fs            window, both panels, the status line and the counter
      MainWindow.fs         composition: views, runtime, event wiring
  Adapters/               the real implementations of Ports.Dependencies;
                          the only folder allowed to touch the OS
    HostEnvironment.fs    platform, home directories, `$EDITOR` and `--debug`, read once
    WindowSearchRunner.fs the viewer's search on a background task under the progress dialog
    Prompts.fs            the message boxes and popups the application runs
    RecycleBin.fs         the Windows recycle bin, through the VB runtime
    TerminalHandOver.fs   the Terminal.Gui driver stopped while a program holds the
                          terminal and built again after it, inside one application
    LaunchProcess.fs      a program handed to the system, or on Windows a window program
                          started from its file with its streams drained; never waited on
    EditorProcess.fs      the editor started detached on Windows; on Unix handed the
                          terminal and waited on
    CommandProcess.fs     a user menu command started directly or through the shell, never waited on
    NativeLinks.fs        hard links and junctions through kernel32 and libc
    RegistryHandlers.fs   the Windows registry -> the handlers a name has
    ProgramResolution.fs  a program's name -> its file: as written, `PATH` with `PATHEXT`, App Paths
    BatchRunner.fs        a batch on a background task under the progress dialog;
                          its start and its end, with the items it reported, logged;
                          and work on a background task with no window at all
    SearchRunner.fs       the walk on a background task, hits gathered ten times a second
    ModifierWatch.fs      held Ctrl / Alt / Shift, polled on Windows, absent elsewhere
    DirectoryWatch.fs     the directories on screen watched, bursts collapsed,
                          what the watcher cannot say polled for
    SessionProcess.fs     the process of this session as an owner, and whether an
                          owner's process still runs
    Crashes.fs            a session that throws, an unhandled thread and an
                          unawaited task -> a crash written to the log
    DebugLog.fs           the log port: every entry with `--debug`, the failures without
    Dependencies.fs       every port bound to one implementation, platform picked here
    Session.fs            quit key, application, theme, dependencies, window, run
                          under the crash hooks; the pointer to the log after a crash
  Program.fs              hands its arguments, the real file system and the
                          environment to Session.run
  Properties/PublishProfiles/
    win-x64.pubxml        self-contained single-file build for Windows
    linux-x64.pubxml      the same for Linux

Marbas.Tests/
  Root.fs                 the root every test path is built on, per platform
  WindowsFact.fs          a Fact that skips where drive letters do not exist
  Expect.fs               Result unwrapping, so act and assert stay apart
  Fakes.fs                the do-nothing Ports.Dependencies every test starts from
  FileSystem/             unit tests, one module per FileSystem module
  Messages/               unit tests of the wording, one module per module
  State/                  unit tests, one module per State module; the
                          interpreter in the Shell* files
  Ui/                     unit tests of the pure Ui modules, in the same eight
                          folders as the source
  Integration/            every test that constructs a Terminal.Gui view, in
                          those same folders
    Gui.fs                 focusedWindow, leftList, rightList, press, panel,
                          panelTitle, shownPath, drawnText

coverlet.runsettings      what coverage measures and what it leaves out
.editorconfig             the Fantomas settings: its defaults, cramped brackets, lf
.config/
  dotnet-tools.json       Fantomas, pinned to one version
.github/workflows/
  ci.yml                  restore, build and test on Windows and Linux;
                          the format check on Linux
  release.yml             publish both profiles on a tag, attach them to a release
```

## Explicit dependencies

**FSharp.Core**: Central package management switches the
SDK's implicit reference off (`DisableImplicitFSharpCoreReference` becomes
true), so a project that pulls in no other F# package — a probe, say — compiles
against the SDK's `FSharp.Core` and ships none, and falls over on its first line
with a `FileNotFoundException`. The `PackageVersion` pins the version the tree
already resolved through `FsToolkit.ErrorHandling`, and a project that needs it
asks for it by name like any other package.

