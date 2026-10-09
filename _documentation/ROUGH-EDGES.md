# Rough edges

What is known to be wrong, missing or merely awkward, and why it stands. Each
entry was checked against the code on 2026-09-27, when phase 15 closed; none is
scheduled by [the plan](PLAN.md). What is a deliberate design decision belongs
in [Architecture/](Architecture/OVERVIEW.md) rather than here.

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
  yellow on the cursor's cyan, which is a third case in the `RowRender` handler,
  beside the cursor row it hands back and the marked rows it paints.
- The entry colours and the rule under the listing are drawn by a driver no test
  has, so what a test can say is which attribute `RowRender` hands back and
  where the `Line` sits — not what the screen looks like. Both belong on the
  manual row of [Cross-platform](Architecture/CROSS-PLATFORM.md), and bright blue
  on the panel's blue background is the one to look at first. The screen can be
  rendered into text without a terminal, which is how the entry above was
  settled.

## Archives

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
  directory's own hits before it descends into any subdirectory — a change to
  the order of the walk that P15.14's rewrite of `FileSearch.run` left as it
  was.
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
  that explains itself rather than one that works. A user menu entry marked to
  run as administrator is refused the same way.
- On Unix the two shell ways of a user menu entry are one: both are `sh -c`
  with the output drained, because there is no window of its own to close or
  keep open. What a shell command prints there is not seen anywhere.
- A direct run of a user menu entry as administrator relies on Windows starting
  the program in the directory it was given; the shell ways put a `cd /d` in
  front of the line so that they do not. Whether an elevated start keeps its
  directory has not been measured.
- A command run again from the history writes its list file where it was
  written the first time, so a run kept from an earlier session recreates that
  session's working directory, which nothing removes afterwards.
- Nothing checks that a configured editor exists before `F4` starts it. A path
  that names nothing is reported the way any other failed start is, after the
  key was pressed, rather than when it was typed into the popup.
- On Unix `F4` still waits for the editor. Windows gives a console editor a
  window of its own, so there `F4` starts it and lets go; Unix has no such
  window, and a console editor such as `vi` can only share the terminal the
  panels are drawn in. So on Unix the editor runs as a child process sharing
  this terminal, the panels hold still until it ends, and only a redraw
  (`ClearScreenNextIteration`) is forced when it returns. A windowed editor —
  VS Code, `gedit` — round-trips cleanly. A full-screen console editor shares
  the terminal with a Terminal.Gui driver that is still reading input, and
  there is no suspend/resume in the v2 API for a child process
  (`IDriver.Suspend` is `SIGTSTP`, a different thing). If that turns out to
  matter, the fix is to shut the driver down around the process and
  re-initialise afterwards, on the waiting branch alone; it touches
  `Adapters/EditorProcess.fs` only.
- On Windows a window program Marbas starts can write over the panels. Every
  start goes through the shell verb, which gives a console program a console
  of its own and a window program none, and an Electron program — `Code.exe`
  named in a user menu entry, as the editor of `F4` or as the program `Enter`
  opens a file with — then attaches to the console of its parent, the terminal
  the panels are drawn in, and writes its log there. Terminal.Gui does not
  know, so the text stays until those cells are drawn again. The shell verb
  cannot redirect the streams, and starting without it would take the window
  of its own away from a console program; the fix is to read the subsystem
  from the program's header and start a window program with its streams
  drained, which leaves a start through a file's association as it is.
  `ELECTRON_NO_ATTACH_CONSOLE=1` in the user's environment is Electron's own
  switch against attaching and has not been tried here.
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
- A copy, a move or an archive into itself is refused by comparing paths as
  written, so a target that reaches one of the sources only through a link — the
  other panel standing in a link to a directory under the source — is not
  caught, and the copy runs away as it did before P21.4. Resolving both sides to
  their final paths first would close it.

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
  cover is every key the viewer takes and every row it would draw. The same
  holds for `WindowSearchRunner.run`: its reports are tested, the window and
  the task were driven once through the probe, and no test runs them.
- The progress window opens for every `F7`, so a match on the screen or a few
  lines down shows it for an instant. Waiting a moment before showing it was
  weighed and left out for one shape shared with the other runners.
- The mark of a match counts a column per character, as the rows do, so on a
  line holding a character two columns wide, or one outside the basic plane,
  it lands as far to the left as the line is drawn short. The mark of a run of
  bytes in the character column of the dump does the same on a row whose
  characters are two columns wide.
- Where detection has to guess — a byte order mark is missing and the bytes are
  not valid UTF-8 — the viewer still says Latin-1, which is wrong for a file in
  CP852 or Windows-1250. `F8` puts that right by hand, but only for the viewing:
  nothing is remembered, and the next `F3` guesses again.
- A character two columns wide counts as one in the text of a hex row, so
  scrolling across a dump of UTF-16 Chinese stops a column or two short of the
  end of the row. The character column itself is laid out by width and stays
  under its bytes.
- A single line longer than `LineIndex.MaxLineLength` (a megabyte) is cut there.
  Nothing in a viewer can show more of one line than a screen holds, but
  scrolling right past that point stops rather than going on.

## History

- The tab strip of the `Alt+F12` popup draws its joints wrongly once the tab in
  front changes. The first picture is right — `├───────┴────────────┘` under
  the headers — but after a switch the line under them starts with a corner and
  loses the joints of the tabs behind, and their top corners do the same. It is
  `Tabs` drawing itself: switching with `←`/`→`, which the control handles on
  its own, leaves the same marks as `Ctrl+PgUp`/`Ctrl+PgDn`, which set
  `Tabs.Value`, and neither a relayout nor a redraw of the whole dialog cures
  it. Measured on Terminal.Gui 2.4.17 during P16.4; the lines are wrong, the
  rows and the colours are not. A strip drawn by Marbas itself over one table
  whose rows are swapped would end it, at the price of the keys `Tabs` gives
  for free.

## The clipboard

- The clipboard reaches Windows alone. `SystemClipboard.forPlatform Unix`
  answers every request with `ClipboardUnavailable`, shown once per session and
  then passed over in silence: text on Linux needs `xclip` or `wl-clipboard` (or
  a terminal's OSC 52 escape), and files a desktop-specific format, and none of
  them is called yet. The fix is one more implementation of the same port; the
  plan of phase 18 leaves it for a later phase.
- `Alt+L` reads a `.lnk` shortcut only as far as a path goes. A shortcut to
  something that has no path — a Control Panel item, a printer, an application
  from the Store — and one whose target is kept only in the environment
  variable block (`%ProgramFiles%\…`) gives the shortcut's own path. So does a
  local path written in a code page other than ASCII when the shortcut carries
  no relative path to fall back on; the code page is not recorded in the file.
  An internet shortcut (`.url`) is not read at all.
- A path read from the clipboard into an archive matches the names inside it
  exactly, case included. On Windows `C:\work\Backup.zip\Docs` reaches the
  archive, because the disk ignores case, and then finds no `Docs` in it if the
  archive stores `docs` — and, as the plan asks of a path that leads nowhere,
  says nothing. A path copied with `Alt+P` always matches.
- A session's working directory, `<temporary root>\Marbas\<session>`, is
  removed only by `F10` or `Ctrl+Q` in the main window. Closing the console
  window, a logoff or shutdown, a killed or crashed process and `Ctrl+Q` inside
  a dialog leave it behind, with whatever it held — since P18.6 that includes
  every entry unpacked for the clipboard — and no later session looks for it.
  [Phase 21](Plan/PHASE-21-THE-LOG-AND-THE-DEBTS.md) sweeps such directories
  at start.
- Entries copied from inside an archive with `Alt+Shift+C` reach the clipboard
  as unpacked copies in the session's working directory, which is deleted when
  Marbas quits. A paste after that finds nothing. Keeping them would leave the
  temporary directory to grow with every session; the user guide says to paste
  before quitting instead.
- Two hits of the same name found in archives — `a.zip\x.txt` and
  `b.zip\docs\x.txt` — are unpacked into the same scratch directory for the
  clipboard, and the second overwrites the first, so the clipboard names the
  one file twice. `F5` of the same results into one directory meets the same
  collision.
- Files cut with `Alt+Shift+X` and pasted in Explorer stay on the clipboard
  after the move. Explorer clears a cut only for a program that answers its
  `Paste Succeeded` handshake through an OLE data object, and the adapter hands
  over plain clipboard blocks; a second paste then finds the files gone and
  Explorer says so.
- `Alt+Shift+V` into the directory the files came from does what `F5` does
  between two panels on one directory: it asks whether to replace each file
  with itself, and the system refuses the copy. Explorer makes `notes - Copy.txt`
  instead; Marbas has no such naming yet.
- Files Explorer cut or copied from inside a zip probably reach the clipboard
  only as virtual files (`FileGroupDescriptorW` and `FileContents`, not
  `CF_HDROP`), which `GetFiles` does not read, so `Alt+Shift+V` pastes nothing
  and says nothing. This was not measured; how it could be is at the end of
  the clipboard section of [File operations](Architecture/FILE-OPERATIONS.md).
