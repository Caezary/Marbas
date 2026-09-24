# Rough edges

What is known to be wrong, missing or merely awkward, and why it stands. Each
entry was checked against the code on 2026-09-20; the ones a later task closes
name that task in [the plan](PLAN.md). What is a deliberate design decision
belongs in [Architecture/](Architecture/OVERVIEW.md) rather than here.

## Keys and the terminal

- `Alt+F1`, `Alt+F2` and `Alt+F6` cannot be reached on a GNOME desktop. Its
  window manager claims all three by default — `panel-main-menu`,
  `panel-run-dialog` and `cycle-group` — and a grab means the terminal is never
  sent the key, whichever terminal it is. The drive choosers of P13.13 and the
  link key of P13.11 therefore do nothing there, and unlike every key measured
  since P10.7 they carry no second binding. The fix is the shape the rest of the
  application already uses: a letter with `Alt` bound beside each of them in
  `KeyMap`, which is a row in the table and a line in the user guide per key.
  Found by reading GNOME's defaults during P14.1, not by pressing them on a
  desktop — no one has run Marbas on one yet.
- `Shift+Enter` does not reach the panel under Windows Terminal: the modifier is
  dropped and the bare `Enter` *runs* the entry instead of asking what should
  open it. `Alt+R` was added beside it and is the binding to use; the dead one
  is kept because it is the traditional name and costs nothing where a terminal
  does deliver it.
- `Ctrl+Alt+Enter` reaches the panel as nothing at all under Windows Terminal:
  `Alt+Enter` is its own full-screen toggle, and adding `Ctrl` does not get the
  combination past it. `Alt+Shift+R` is the binding to use; the dead one is kept
  for the same reason `Shift+Enter` is.
- `Alt+W` and `Alt+Shift+R` have not been pressed on Linux yet. Both of
  Terminal.Gui's key converters were asked by reflection and spell them the way
  `KeyMap` does — the shift flag survives, so `Alt+Shift+R` cannot quietly
  become `Alt+R` — but whether a given terminal hands the driver those
  modifiers at all is still a manual row.
- `Ctrl+H` toggles hidden entries only where the terminal reports it as a key
  rather than as the byte `0x08`, which no terminal outside the kitty keyboard
  protocol does. Under Windows Terminal it arrives as `Backspace` and does
  nothing, which is why `Alt+H` is the binding on the bar. Nothing is bound to
  `Backspace` itself, so the collision is silent rather than surprising — but it
  is also the reason `Backspace` cannot later be given the "go up one directory"
  meaning some managers have without a `Ctrl+H` pressed elsewhere reaching it.
- Whether the terminal delivers `Shift+↑` / `Shift+↓` / `Shift+PgUp` /
  `Shift+PgDn` as keys of their own is the driver's business, and no test
  reaches it — the suite injects the key. It is a manual row in
  [Cross-platform](Architecture/CROSS-PLATFORM.md), and `Shift+←` / `Shift+→` are
  bound to the same two commands partly as a way round a terminal that drops the
  shifted page keys.
- The bar gives each of `F1` … `F12` an equal slice of the terminal, so below
  about ninety columns the longer commands are clipped (`Quit` reads `Qui` at
  eighty, `NewFile` reads `New`). Shorter labels would fix the narrow case and
  waste the wide one; a width-aware label (long name where it fits, short where
  it does not) is the fix if it ever matters. It is also why `F12` is labelled
  `Setup` while the popup it opens is titled `Settings` — `Settings` beside a
  three-character key name is clipped to `Set` on an eighty-column terminal.
- `F1`, `F2` and `F9` have slots on the unmodified row and no commands, which is
  what the reserved space is for — `F1` help and `F2` a user menu are the
  classic fillers. All three carry a command with a modifier (`Shift+F1` packs,
  `Shift+F2` unpacks, `Shift+F9` saves the session).
- The modifier row only appears on Windows. `Watches.Modifiers` registers nothing
  elsewhere, because no Unix driver in Terminal.Gui 2.4.17 reports a standalone
  modifier outside the kitty keyboard protocol. If that protocol is ever wired
  through, the fix is one more implementation of the same port.
- The poll reads the physical keyboard, not this window's: holding `Ctrl` in
  another application changes Marbas's bar too, if it happens to be visible.
  `GetConsoleWindow` would gate it, but under Windows Terminal that handle is
  not the window the user sees, so the gate would switch the feature off where
  it matters most.
- The bar follows the keyboard ten times a second, so a `Ctrl` tapped and
  released inside 100 ms may never show its row. Polling faster costs more than
  the miss does.

## The panels

- Both panels start on the same directory, so `F5` straight after launch copies
  a file onto itself and the OS refuses it with an error dialog. `F6` can be
  pointed elsewhere by hand, `F5` cannot — a target prompt for `F5`, or a check
  that the two directories differ, would be the fix.
- The search matches the first entry from the top of the listing and there is no
  "next match" key, so a name that shares its text with an earlier one cannot be
  reached by typing alone. Searching on from the cursor, or binding a repeat
  key, is the fix whenever it starts to bother.
- `Home` and `End` are swallowed at the ends of the listing rather than handled,
  which is invisible while nothing else wants them.
- A marked entry under the cursor is drawn in the cursor's colours, not the
  mark's, so the mark itself is invisible while the cursor stands on it. The
  cursor is the one that wins — black on cyan, as on an unmarked row — and only
  the footer's count says the entry is marked. The classic look is the mark's
  yellow on the cursor's cyan, which is a third case in the `RowRender` handler
  that already skips both rows.
- The entry colours and the rule under the listing are drawn by a driver no test
  has, so what a test can say is which attribute `RowRender` hands back and
  where the `Line` sits — not what the screen looks like. Both belong on the
  manual row of [Cross-platform](Architecture/CROSS-PLATFORM.md), and bright blue
  on the panel's blue background is the one to look at first. The screen can be
  rendered into text without a terminal, which is how the entry above was
  settled.

## Archives

- Reading an archive lists it whole for every level: `ArchiveListing.read` walks
  all of the entries and keeps the one level asked for. For the archives a
  panel will show that costs nothing measurable, but an archive of a hundred
  thousand entries pays the walk on every step into it. P15.13 caches the index
  and answers the levels from it.
- Extracting opens the archive once per target, because the tar reader can only
  go forwards and each target answers for itself. Unpacking ten marked entries
  out of a `tar.gz` therefore decompresses it ten times. A single pass that
  keeps a result per target would fix it, at the cost of holding every target's
  outcome while the entries arrive in the archive's own order. P15.17 measured
  what it costs and found it the largest thing wrong with `Shift+F2`, which asks
  for one target per top-level entry: a flat archive of *n* entries is walked *n*
  times, so unpacking 4 000 of them takes 5.4× what the same content takes under
  one directory, and the curve is the square. It is the same for zip as for 7z —
  the container has nothing to do with it. The numbers are in
  [Measurements](Progress/MEASUREMENTS.md#unpacking-a-solid-7z-p1517-2026-09-24),
  and P15.25 makes one walk serve every target.
- A wrong password is reported entry by entry with the library's own wording,
  not as "the password is wrong": `PasswordNeeded` is only answered before an
  extraction starts, when the archive holds an encrypted entry and no password
  was given at all.
- An archive is written whole or not at all only when it is cancelled. A single
  unreadable file inside one source leaves the archive standing with everything
  that did go in, which is deliberate — the failures are listed and the marks
  survive — but it does mean a zip can be complete-looking and short of a file.
- `SharpZipLib` cleans an entry name as it writes it, turning a backslash into
  a separator. On Linux, where a backslash is a legal character in a file name,
  packing `a\b.txt` therefore stores `a/b.txt` and the archive gains a level the
  disk never had. `ArchiveListing` reads keys the other way — only `/` starts a
  new level — so such an archive lists a directory that was never there. The fix
  is on the writing side, where the entry name would have to bypass
  `ZipEntry`'s cleaning.
- A password only reaches zip. Tar and its compressed kinds have no notion of
  one, and 7z — which can encrypt its headers as well as its content — is
  created without one, because `ArchiveKind.carriesPassword` answers `true` for
  zip alone.

## Finding files

- A directory whose hits are split by a subdirectory heads a group for each run
  of them. P15.10 made the results window append its rows instead of rebuilding
  them every tick, and a group header is opened where the location changes, so
  the list is the walk read straight through. A depth-first walk reports a
  directory's own entries interleaved with descending into its subdirectories,
  so `*.md` over `_documentation` shows 8 headers for the 5 directories that
  hold the 48 hits. Nothing is missing or mislabelled, but a directory can be
  named more than once. Removing the split means `FileSearch` reporting a
  directory's own hits before it descends, which is a change to the walk and
  belongs with P15.14.
- One archive whose index cannot be read keeps the whole results panel from
  loading. Since P15.4 a hit inside such an archive is no longer shown as if it
  had been verified — the listing answers `ArchiveUnreadable` instead — but the
  refusal takes every other hit down with it, including the ones on disk that
  were verified fine. P15.24 shows what could be verified and marks the rest.
- A search *from the root of the volume* on Linux walks into `/proc`, `/sys`,
  `/run` and the other pseudo file systems. The walk is bounded by the other
  listed drives, and P13.12 keeps exactly those mounts off the list, so nothing
  bounds them; P14.5 described them as never entered, which holds only for
  *All drives*, where they are never a starting place. The fix is to hand
  `FindGround.resolve` the filtered mounts as roots not to cross.

## Running and editing

- Nothing on Unix asks for administrator rights: `LaunchProcess` refuses an
  `AsAdministrator` request by platform, because there is no way to raise them
  that does not put a password prompt on the screen the panels own. The refusal
  reaches the user as the reason it carries, so `Alt+Shift+R` there is a key
  that explains itself rather than one that works.
- Nothing checks that a configured editor exists before `F4` starts it. A path
  that names nothing is reported the way any other failed start is, after the
  key was pressed, rather than when it was typed into the popup.
- `F4` starts the editor as a child process sharing this terminal and only
  forces a redraw (`ClearScreenNextIteration`) when it returns. A windowed
  editor — `notepad`, VS Code — round-trips cleanly. A full-screen console
  editor on Unix shares the terminal with a Terminal.Gui driver that is still
  reading input, and there is no suspend/resume in the v2 API for a child
  process (`IDriver.Suspend` is `SIGTSTP`, a different thing). If that turns out
  to matter, the fix is to shut the driver down around the process and
  re-initialise afterwards, with a switch that keeps the detour off Windows; it
  touches `Adapters/EditorProcess.fs` only.
- The open-with popup lists the programs but never says which one the system
  would pick by default, and it cannot remove a program from the list — the
  registry and the mime database are read, never written. Removing a handler is
  the desktop's own job.
- The Windows handler list is only as good as the registry: an app registered
  through a `rundll32` stub or a packaged app with no `shell\open\command` is
  simply missing from it, and a program whose executable carries no file
  description shows as its file name (`devenv.exe`) rather than a title.
- Neither adapter follows the `.desktop` legacy dash convention
  (`foo-bar.desktop` under `foo/bar.desktop`), so a handler filed that way is
  passed over.
- Nothing warns that naming a program for an archive extension takes `Enter`
  away from the archive browser. `Alt+W` is the way back in, and the only
  place that says so is this file.

## Settings and the session

- The settings popup covers the programs and nothing else. Everything else the
  settings file holds — where each panel stands, how it sorts, whether it
  conceals — is still written on quit and editable only by hand. Adding those
  rows is a matter of more fields on the same popup whenever it is wanted.
- The properties popup edits one entry, the one under the cursor, and never the
  marked ones. It also offers no way to reach the properties of `..` or of a
  file — `F4` on a file goes to the editor, which is what the key is for.
- The session is written on quit, on every tab command and on `Shift+F9`, but
  not after ordinary navigation — so a crash still loses whatever changed since
  the last of those. Writing after every step would cost a file write per
  keystroke; a write on a timer is the middle road if it ever matters.
- `Ctrl+Q` pressed while a dialog holds focus still quits through the
  application binding, which does not go through `Update` and therefore does not
  save. Unbinding `Command.Quit` is not the way out: `Esc` answers to that same
  command on purpose, since that is what closes a popup.

## Operations

- Delete goes through an effect, like copy and move, but it still runs on the UI
  thread rather than behind `Runners.Batch`, so removing a huge tree blocks. Neither
  `Directory.Delete` nor the recycle bin reports progress, so a progress window
  would have nothing to show; cancelling is what it would buy. Wiping, the one
  delete that has something to report, is the exception: it runs behind
  `Runners.Batch` and `Esc` stops it. P15.15 puts the rest there too.
- Only the home trash is written on Unix, never the per-volume
  `$topdir/.Trash-$uid` the freedesktop spec asks for. Deleting from another
  disk therefore copies the entry across rather than renaming it — correct, but
  slow for a large tree.
- Nothing in Marbas browses or empties the trash; that is the desktop's job.
  A `Trash.restore` reading the `.trashinfo` files back would be the obvious
  next step if it is ever wanted.
- Progress is posted per file through `app.Invoke`. A tree of thousands of tiny
  files therefore queues thousands of callbacks; if that ever stutters, the fix
  is to coalesce (keep the latest path in a field and refresh on a timer)
  rather than to report less.
- An operation that finishes with a listing of its own — a copy, a delete — pays
  one extra listing if the disk also changed while its progress window was up:
  the held re-read arrives just after. It keeps the cursor and the marks, so it
  costs a directory read and nothing else.

## Watching the disk

- The watch notices what a `FileSystemWatcher` reports, and the poll underneath
  it only asks whether the watched paths still exist. On a file system that
  reports nothing — an SMB or NFS share is the usual one — an entry added inside
  the directory is therefore still invisible until `Ctrl+R`. Polling the listing
  itself instead would mean reading two directories every couple of seconds for
  every session left open.
- A removed drive shows up within the poll interval, two seconds, rather than at
  once. The device notification Windows offers wants a window handle, which a
  console application does not have.

## The viewer

- The viewer is opened by `Shell` through the `ShowFile` port, which hands over
  an opened `FileWindow.Source` rather than a list of lines, so the modal run
  itself (the `running` helper in `Prompts.showFile`) is only exercised by running
  the app — the same blind spot the progress dialog has. What the tests do
  cover is every key the viewer takes and every row it would draw.
- The viewer shows one encoding per file and offers no way to override it. Where
  detection has to guess — a byte order mark is missing and the bytes are not
  valid UTF-8 — it says Latin-1 and renders every byte, which is readable but
  wrong for a single-byte code page that is not Latin-1. A key that cycles the
  encoding would be a small addition to `ViewerKeys` and `ContentView`.
- A single line longer than `LineIndex.MaxLineLength` (a megabyte) is cut there.
  Nothing in a viewer can show more of one line than a screen holds, but
  scrolling right past that point stops rather than going on.
