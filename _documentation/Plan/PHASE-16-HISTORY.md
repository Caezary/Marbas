# Phase 16 — History

Goal: the application remembers where the user has been and what they did, and
hands it back without retyping. `Alt+F12` opens one popup with three tabs — the
files started with `Enter`, the files viewed and edited, and the directories the
panels stood in — and `Enter` on a row does the thing again. The text fields of
the popups that ask for a path, a name or a pattern remember what was typed into
them and offer it back as a drop-down list.

Seven decisions run through the whole phase.

**History has a file of its own.** `history.conf` sits next to `settings.conf`,
in the directory `Settings.location` names, and speaks the same `key=value`
lines with numbered entries — `folder.1=…`, `view.1=…`, `input.target.1=…`. The
settings file would be buried under a few hundred lines that change on every
`Enter`, and a database would bring a native library per runtime and a port of
its own outside `IFileSystem`, which is what keeps the settings parser, its
fallbacks and the file itself testable on `MockFileSystem`. The parser forgives
the way `Settings.parse` does: a line it cannot read costs that one entry.

**The file is the source of truth, and a change is an operation on it.** Two
running copies of Marbas must not erase each other's history, and a union of
both on every write would bring back what the user deleted in the other one. So
a transition never writes a history it holds; it raises `Remember entry`,
`Forget entry` or `ClearHistory list`, and `Shell` answers each by reading the
file, applying the change, writing the file and dispatching the result back as
`HistoryChanged`. Both copies converge at every change they make. The history is
therefore written on every change, and a crash loses nothing.

**The moment comes from the shell.** Every entry carries the `DateTimeOffset` it
was last used, shown in the popup. A transition may reach the file system and no
other port, so it cannot read a clock: `Shell` stamps the moment when it applies
the change, from a clock `Dependencies` injects the way the trash and the
directory watch already get theirs.

**Every list is global, most recent first, and capped.** One list per kind, not
per panel or per tab: history answers "where was I", and the common move is
opening in this panel the directory the other one was just in. Using an entry
again moves it to the top rather than repeating it, and a list keeps its hundred
most recent entries.

**Nothing is checked when the history is shown.** An entry on a network share
that is offline can hang a check for seconds, so the popup opens on what the file
says. Acting on an entry that is gone ends in the error the action already
reports, and the entry stays — a drive unplugged for an hour is no reason to
lose it; `Del` removes it.

**A place in the history is a directory or a place inside an archive.**
`PanelLocation.SearchResults` is a listing, not a place, and does not outlive the
session, so it is never recorded; `OnDisk` and `InArchive` are recorded as they
are, and choosing an archive entry goes back inside the archive.

**A text field remembers text.** What a field hands back is exactly what was
typed, so every input list stores an `EnteredText` under an `InputKind` rather
than an `EntryName` or a `NamePattern`: the meaning is read after the popup is
confirmed, where it is read today. A value with a line break — a path Linux
allows and a line-based file cannot hold — is not remembered.

- **P16.1 The keys and the controls, measured** — `_probes/KeyProbe` gains
  `Alt+F12` and its stand-in `Alt+Shift+H` in the panel; `Ctrl+PgUp`,
  `Ctrl+PgDn`, `Del`, `Shift+Del` and `Alt+G` inside a modal `Dialog`; and
  `Alt+↓` and `F4` in a `DropDownList` inside one. The same five terminals as
  P13.1 answer, and a key that does not arrive is replaced by a letter with
  `Alt`. Beside the probe, a spike renders `Tabs` holding a `TableView`, and an
  editable `DropDownList`, inside a dialog in the classic colours, and drives
  them with synthetic keys: whether the popover fits inside the dialog, whether
  `Enter` on an open list picks the row or presses the default button, whether
  `Esc` closes the list or the dialog, and whether `TableView` swallows the
  letters the filter of P16.8 needs. *Done when:* the table in
  [Cross-platform](../Architecture/CROSS-PLATFORM.md) has a row for every key,
  and `Architecture/HISTORY.md` records what the controls did and what the
  later tasks do about it.
- **P16.2 The history and its file** — the types: a `Place` that is `OnDisk` or
  `InArchive`; a launch entry holding the whole `LaunchRequest`, so doing it
  again starts the same program with the same elevation; a file entry holding
  its place and whether it was `Viewed` or `Edited`; and an input entry holding
  `EnteredText` under an `InputKind`. Each carries the moment it was last used.
  `History.remember`, `forget` and `clear` are pure, deduplicate by what the
  entry points at — a file viewed and then edited is one entry, now `Edited` —
  and keep a hundred per list. `History.load` and `History.change` go through
  `IFileSystem`; `Dependencies` names `history.conf` and a clock. *Done when:*
  the operations, the parser's fallbacks and a change applied on top of a file
  another copy wrote are proved on `MockFileSystem`, and the application still
  starts with no history file at all.
- **P16.3 The directories are remembered** — a panel that lands somewhere other
  than where it stood raises `Remember`: `Enter`, `..`, `Alt+W`, a root, a drive,
  a hit stood on, a path opened. A re-read, a sort, a hidden toggle and a tab
  switch do not move the panel and raise nothing; a panel entering search
  results raises nothing either. `HistoryChanged` puts the answer into
  `AppState`. A history that cannot be written says so once per session, since
  the next `Enter` would otherwise raise the same dialog again. *Done when:*
  every transition that moves a panel is proved to raise the effect, the ones
  that do not are proved silent, and walking around the real application fills
  `history.conf`.
- **P16.4 The popup and the directory tab** — `Alt+F12`, labelled `History` in
  the `Alt` layer of the bar, and `Alt+Shift+H` open a dialog with the tabs
  History, View history and Folder history in that order; each is a table with
  the moment and the place, newest first. Opening reads the file afresh, so what
  another copy remembered in the meantime is there. It opens on the tab last
  used in the session, Folder history the first time; `Ctrl+PgUp` and
  `Ctrl+PgDn` walk the tabs. The buttons are `[Open]` — the default, so `Enter`
  presses it — `[Go to]`, `[Delete]` and `[Close]`; on this tab `[Open]` sends
  the active panel to the place and `[Go to]` is disabled. Choosing an entry
  closes the popup. *Done when:* the popup is rendered and driven through a real
  `Application`, and a directory chosen from it is where the active panel
  stands.
- **P16.5 The files started** — `LaunchFinished` carries the request it
  answers, and a start that succeeded raises `Remember`: a file opened with
  `Enter`, a program chosen in `Alt+R`, and a run as administrator. `Alt+R` on a
  directory or a drive shows it in the system file manager and is not
  remembered; a start that failed is not remembered either. The History tab
  shows the file and, where one was chosen, the program and the elevation;
  `[Open]` starts it again and `[Go to]` puts the active panel in the file's
  directory with the cursor on it. *Done when:* each of the three is proved
  remembered and the other two proved not to be, and a file started from the
  popup runs as it ran the first time.
- **P16.6 The files viewed and edited** — `F3` on a file remembers it as
  `Viewed` in the transition that raises `ViewFile` or `ViewArchiveEntry`; `F4`
  and `Shift+F4` remember it as `Edited` in the transition that raises
  `EditFile`, before the editor runs, since a console editor holds the screen
  until it is closed. `F3` on a directory measures it and is not remembered. The
  View history tab marks each row `V` or `E`; `[Open]` repeats what the row
  says, `F3` views and `F4` edits whatever the row says, and `[Go to]` behaves
  as in P16.5. An entry inside an archive is viewed inside it and refuses `F4`
  the way the panel does. *Done when:* both kinds are proved remembered at the
  moment they are raised, and the popup opens each of them.
- **P16.7 Deleting and clearing** — `Del` forgets the row under the cursor and
  `Shift+Del` clears the tab in front after asking; both are operations on the
  file like `Remember`, so the other copy does not bring the entries back.
  *Done when:* a forgotten entry is gone from the file and from the popup, and a
  cleared list stays empty after a restart.
- **P16.8 The filter** — typing narrows the tab in front to the rows containing
  what was typed, anywhere in the text and ignoring case, shown on a line under
  the table. `Backspace` shortens it, `Esc` clears a filter that is not empty and
  closes the popup when it is, and switching tabs clears it — the gesture of the
  quick search in the panel. *Done when:* the popup is driven through a typed
  filter, a `Backspace` and both presses of `Esc`.
- **P16.9 The fields that ask one question** — the `To:` of `F5` and `F6`, one
  list between them; the name of `F7`; the name of `Shift+F4`; and the pattern
  of `+` and `-`, one list between them. Each field becomes an editable
  `DropDownList` whose list is its history, opened with `Alt+↓` or `F4`. What
  was typed is remembered when the popup is confirmed, whether or not the
  operation then succeeds, except for `F7`, which remembers a name only once the
  directory exists, so a refused name does not take the suggestion's place. `F7`
  still opens holding the most recent name, now read from its list:
  `LastCreatedDirectory` leaves `Settings` and `AppState`, and `mkdir.name` is no
  longer written — a stale line is passed over like any other unknown key.
  *Done when:* each field is proved to remember on confirming and not on
  cancelling, and each offers its list through a real `Application`.
- **P16.10 The fields of the larger dialogs** — the masks and the text of
  `Alt+F7`, the archive path of `Shift+F1`, the target of `Alt+F6` and the
  program of `Alt+R` get a list each, the same way. The password of an archive
  is never remembered. *Done when:* each field offers its history, and moving
  between the fields of a dialog with the arrows still does what it did.

Every task updates [the user guide](../USER-GUIDE.md), the history section of
[What works today](../Progress/WHAT-WORKS.md) and `Architecture/HISTORY.md` for
what it adds, rather than leaving the description for a task at the end.
