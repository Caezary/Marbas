# User guide

Marbas is a full-screen, keyboard-driven, two-panel file manager for the
terminal, in the spirit of the orthodox managers of the DOS era.

## Running Marbas

Download the binary for your platform from the
[latest release](https://github.com/Caezary/Marbas/releases) and run it —
each one is self-contained and carries its own .NET runtime, so nothing else
needs to be installed. On Linux and macOS, mark the file executable first
(`chmod +x Marbas`).

If Marbas stops on an error, it writes what happened to `marbas.log`, in the
same directory as its settings — `%APPDATA%\Marbas` on Windows,
`~/.config/Marbas` elsewhere — and says where the file is once the terminal is
back. That file is what to attach to a bug report. It keeps only its last
megabyte, so it never grows without bound.

To find out what led up to a problem, start Marbas with `--debug`
(`Marbas --debug`). For that session the same file also records every key's
command and every action taken, by name only, every batch with how many items
it went through, and every error shown, in the words it was shown with. No file
contents, no passwords and no clipboard text are written. The switch lasts for one session; the next start
without it records crashes alone again — and, should it ever happen, a line for
each leftover of an earlier session that could not be cleared away.

While it runs, Marbas keeps a working directory of its own under the system's
temporary directory (`%TEMP%\Marbas` on Windows, `/tmp/Marbas` elsewhere) and
removes it when you quit. If a session ends some other way — the console
window is closed, the machine shuts down — the next start of Marbas clears away
what it left, so nothing there needs tidying by hand.

## Panels and the cursor

- `Tab` switches which panel is active — the active one has a double border.
- `↑`/`↓`, `PgUp`/`PgDn`, `Home`/`End` move the cursor; `←`/`→` jump a page at
  a time without changing panels.
- `Enter` opens the directory (or file's parent, via `..`) under the cursor.
- The panels follow the disk on their own: a file or directory another program
  adds, renames or removes shows up in whichever panels are on screen, without a
  key. Only the tab in front is watched — the others are re-read when you switch
  to them. If the directory a panel stands in is deleted, that panel steps up to
  the nearest one that is still there; if the drive it stands on is pulled out,
  it lands on a root that exists.
- `Ctrl+R` reloads the active panel, which is what to reach for on a network
  share: those do not always report their own changes.
- `Alt+F1` / `Alt+F2` open *Drives* for the left / right panel: one row per
  drive with its root, label, kind, total size and free space — the ready drives
  on Windows, the mounts that hold files on Unix, followed there by your home
  directory. The popup opens over the panel it is for, beside that panel's
  outer edge. The list is read each time it opens, so a stick plugged in a moment
  ago is already there. In the popup:
  - `Enter` goes to the drive. If the *other* panel is on that drive, the panel
    goes to the other panel's directory, so both end up in one place; any other
    drive opens at its root.
  - `Alt+R` opens the drive in the system file manager (Explorer, or whatever
    `xdg-open` starts) and leaves both panels where they are.
  - `Esc` changes nothing.
- `Ctrl+PgUp` (or `Alt+U`) goes up one level, wherever the cursor is — the same
  as `Enter` on `..`: to the parent directory, to the level above inside an
  archive, out of an archive at its top level, and from search results back to
  where the search was started. On a root it does nothing.
- `Ctrl+4` (or `Ctrl+\`) sends the active panel to the root of the drive it is
  on. Terminals turn `Ctrl+\` into `Ctrl+4` before Marbas sees it, so both are
  bound and `Ctrl+4` is the one that works everywhere.
- Typing a printable character opens a search in the active panel: the
  cursor jumps to the first entry whose name contains what you typed. `+`, `-`
  and `*` are the exceptions: they mark (see below), and only type into a
  search that is already open.
  `Backspace` edits it, `Esc` or any other key closes it (and that other key
  still does its own job).

## Marking files

- `Insert`, `Space` mark the entry under the cursor and move down;
  `Shift+↑`/`Shift+↓` do the same one entry at a time in either direction.
- `Shift+PgUp`/`Shift+PgDn` (or `Shift+←`/`Shift+→`) mark or unmark a whole
  page at once.
- `+` / `-` mark or unmark everything matching a wildcard pattern (`*.fs`).
- `Alt+E` / `Alt+Shift+E` mark or unmark every entry with the same extension
  as the one under the cursor; `Alt+N` / `Alt+Shift+N` do the same for the same
  name without the extension. The extension is what follows the last dot, so
  `archive.tar.gz` goes with `data.gz` by extension and with `archive.tar.bz2`
  by name; names without a dot go together. Case is ignored and directories
  count too. The classic grey-key spellings — `Ctrl` or `Alt` with the `+` and
  `-` of the numeric keypad — are bound as well, but no terminal measured so far
  delivers them.
- `Alt+A` / `Alt+Shift+A` mark or unmark every file, leaving the marks on
  directories as they were.
- `*` (grey or main) or `Alt+I` inverts the marks: what was marked is
  unmarked and the other way round, directories included. `..` is never
  marked by any of these.
- `Alt+M` takes back the last of these marking commands — a pattern that
  cleared too much, an inversion pressed by mistake — and pressing it again
  goes forward to where it was. It reaches one step back only, and anything
  but moving the cursor, the quick search or `Tab` forgets it.

Marked entries are shown in yellow, and stay marked while the cursor moves
about the listing; the footer counts them and adds up their sizes.

## File operations

Every operation below acts on the marked entries, or on the entry under the
cursor when nothing is marked.

- `F5` copies to the directory shown in the other panel.
- `F6` moves/renames — accept the suggested path to move, or type a bare
  name to rename in place.
- `F7` creates a directory.
- `F8` (or `Del`) sends the targets to the trash — the recycle bin on Windows,
  `~/.local/share/Trash` on Linux — after confirming.
- `Shift+F8` (or `Shift+Del`) deletes for good, skipping the trash. The
  question says which of the two is about to happen, and warns separately when
  a target directory is not empty.
- `Alt+Del` wipes: every file is overwritten with random bytes, renamed and
  then deleted. The question says so, and also says what wiping cannot promise —
  on a solid-state disk, or on a copy-on-write or journaling file system, the old
  data may survive anyway. A link is removed without touching what it points at.
- `Alt+F6` makes a link. The popup holds the other panel's directory (and the
  entry's name, for a single entry) and a choice of *Symbolic link*, *Hard link*
  and, on Windows, *Junction*. A kind that cannot point at what you typed is
  greyed out with the reason underneath: a hard link only to a file on the same
  drive, a junction only to a directory. With several entries marked, one link
  each is made inside the directory you typed. Windows makes symbolic links only
  for an administrator or with Developer Mode on, and says so if you have
  neither.
- `F3` opens the file under the cursor in a read-only viewer (binary files
  show as a hex dump, sixteen bytes a row with a vertical line after the
  eighth and a ten-digit offset in front); `F3`/`Esc` closes it. `F8` in the
  viewer picks the code page the file is read in — `Auto` is what the viewer
  guessed, and UTF-8, UTF-16, Latin-1, Windows-1250, CP852, ISO-8859-2,
  Windows-1252 and CP437 can be chosen; the choice lasts until the viewer
  closes. `F7` in the viewer searches the text from the top of the screen
  down, or with *Backwards* ticked from the bottom of the screen up; *Case
  sensitive* and *Whole words* narrow what matches. With *Regular expression*
  ticked the field is a .NET regular expression matched one line at a time
  (whole words are then left to `\b`); look-arounds and back-references are
  not available, and a pattern that cannot be used is reported before
  anything is searched, leaving the popup open to fix it. The field remembers what
  you searched for, here and in *Find files*, and `F7` again opens on the last
  text and options. `Shift+F7` finds the next match the same way without
  asking. The match is highlighted and scrolled into view, a long search shows
  its progress and `Esc` stops it, and reaching the end — or the start — of
  the file without a match is reported. In the hex dump `F7` searches for
  bytes instead: type them as hexadecimal digits, `4D 5A` or `4D5A` (a single
  digit is a byte of its own), and the run found is highlighted in both
  columns; only *Backwards* applies, and the field keeps its own list of the
  bytes searched for. On a directory — or with directories
  marked — `F3` measures them instead: the size replaces `<DIR>` in the size
  column and shows on the status line. Sizes in the listing are rounded to the
  unit they reach (`1.5 KB`, `59.6 GB`); the status line and the panel footer
  give the exact number of bytes. A `≥` in front means something inside
  could not be read, so the real size is at least that. Sorting by size
  (`Ctrl+F6`) takes the measured sizes into account. The sizes are forgotten
  when the panel is read again.
- `F4` opens it in your `$EDITOR`, or the platform default (`notepad` on
  Windows, `vi` on Unix). On Windows Marbas does not wait for the editor — a
  console editor opens in a window of its own — and you can go on working in
  the panels; what you save shows up in them by itself. On Unix the editor
  takes over the terminal — `vi` or `nano` full screen, with every key — and
  the panels come back when you close it.

Copying, moving, wiping and measuring run in the background behind a progress
window; `Esc` stops them. A wipe stopped part way leaves the file it was working
on in place, already overwritten, and says which one it was. A measurement
stopped part way changes nothing.

Links are shown in bright cyan, and the status line shows where the link under
the cursor points (`twin -> C:\data\docs`). Copying, moving, deleting, packing,
measuring and wiping treat a link to a directory as the link itself and never go
inside it: a copy is a new link to the same place, and deleting removes only the
link.

## The clipboard

What the panel points at can be handed to other programs through the system
clipboard. Like the file operations, each command takes the marked entries, or
the entry under the cursor when nothing is marked; with `..` under the cursor
and nothing marked there is nothing to copy, except for the full path, which
is then the place the panel shows.

- `Ctrl+Ins` — or `Alt+C`, for a terminal that keeps `Ctrl+Ins` for its own
  copy — puts the names of those entries on the clipboard as text, one per
  line, in the order of the listing. There are no quotes, no separator after a
  directory and no line break after the last name, so a single name pastes
  cleanly into a field. In a panel of search results each hit gives its own
  name, not the path shown in the panel.
- `Alt+Shift+Ins` — or `Alt+P` — puts their full paths on the clipboard the same
  way. An entry inside an archive is spelled as the panel title spells it,
  through the archive: `C:\work\backup.zip\docs\notes.txt`. In a panel of
  search results each hit gives its own full path, wherever it was found. With
  `..` under the cursor and nothing marked it copies the path of the directory
  the panel shows — or of the level inside an archive, spelled as the title
  spells it; over search results it copies nothing.
- `Ctrl+Alt+Ins` — or `Alt+L` — puts on the clipboard where each of them
  leads: for a symbolic link or a junction the place it points at (the one the
  status line shows after `->`, made a full path when the link holds a relative
  one), for a `.lnk` shortcut the file or directory the shortcut names. Anything
  else — a plain file, a shortcut Marbas cannot read, an entry inside an
  archive — gives its own full path, so every target still gets a line. Only
  the entry itself is followed: a link inside a linked directory is resolved
  from where the panel shows it, not from where the directory really lives.
- `Ctrl+Shift+Ins` — or `Alt+J` — goes to the path on the clipboard: into it
  when it is a directory, beside it with the cursor on it when it is a file.
  The text is forgiven what it reasonably can be: spaces and quotes around it
  are dropped, a `file:///` address is read as the path it names, `~` stands
  for your home directory, environment variables are expanded (`%TEMP%` on
  Windows, `$HOME` or `${HOME}` on Linux), and a relative path is taken from the
  directory of the active panel. A path that runs into an archive Marbas can
  read — `C:\work\backup.zip\docs\notes.txt`, the way the panel title and
  `Alt+P` spell it — opens the archive at that level, with the cursor on the
  entry when the path names one. Text of several lines, an empty clipboard, a
  path that does not exist and a path into an archive that does not hold it are
  passed over without a word. The place you land on is remembered in the folder
  history.
- `Alt+Shift+C` — or `Ctrl+Shift+C`, in a terminal that delivers the `Shift` —
  puts the entries themselves on the clipboard as files to be copied, so
  Explorer, a mail program or any other program that takes files can paste
  them. `Alt+Shift+X` — or `Ctrl+Shift+X` — puts them there to be moved: pasted
  in Explorer, they leave the place they were cut from. Entries inside an
  archive are first unpacked into Marbas's working directory, behind the usual
  progress window, and those copies are what goes on the clipboard; they last
  until Marbas quits, so paste them before that. An archive cannot be cut from:
  `Alt+Shift+X` on an entry inside one says so and leaves the clipboard as it
  was.
- `Alt+Shift+V` — or `Ctrl+Shift+V` — pastes the files on the clipboard into
  the place the active panel shows, whether Marbas put them there or Explorer
  or another program did: copied when they were copied, moved when they were
  cut. It works like `F5` and `F6`: the same progress window, the same question
  before a file is replaced, the same report of what failed, and inside an
  archive that can be changed the files are added to it. After a move the
  clipboard is emptied, so the same files are not asked for twice. A clipboard
  holding no files — text, a picture, nothing — pastes nothing and says
  nothing. Files Explorer cut or copied from inside a zip may be offered only
  in a form Marbas does not read yet, and then nothing is pasted.

On Linux and macOS Marbas cannot reach the clipboard yet; the first attempt
in a session says so, and later ones stay quiet.

## Finding files

`Alt+F7` (or `Alt+Shift+F`, which is the one to use on a GNOME desktop, where
`Alt+F7` moves the window instead) asks what to look for:

- **File names** — one or more masks separated by `;`, such as `*.jpg;*.png`.
  `*` and `*.*` both mean every name, and a mask without `*` or `?` is found
  anywhere in the name, so `report` finds `2026-report.pdf`. Case never matters
  in a name.
- **Containing text** — leave it empty to search by name alone. With text, you
  can match case, match whole words only, look for files that do *not* contain
  it, and search binary files as well (off by default, since executables are
  full of accidental matches). The encoding of each file is detected, so a
  UTF-16 file is searched as text.
- **Where** — the panel's directory and everything below it, that directory
  alone, the marked directories, the whole drive the panel is on (without
  wandering into other drives mounted inside it), every drive, or the
  directories on your `PATH`.
- **Options** — directories as well as files, hidden entries, following links to
  directories (a link that loops back on itself is not followed twice), and
  looking inside archives, whose entries are matched by name and, with text, by
  content.

The results fill a full-screen window while the search runs, grouped under the
directory each one lives in:

- `Enter` closes the window and puts the panel on the result, or inside the
  directory whose header you are on. A hidden result turns hidden entries on; a
  result inside an archive opens the archive at that level.
- `F2` sends the whole list into the panel (see below).
- `F3` views the file under the cursor; `Esc` in the viewer returns to the list.
- `Esc` stops a search that is still running and keeps what it found on screen;
  `Esc` again, or `F10`, closes the window.

The last results are kept until the next search: *Last results (123)* in the
`Alt+F7` popup brings them back, drops whatever has been deleted since, and says
how many went. They are forgotten when Marbas quits.

**Results in the panel.** After `F2` the panel lists the results by their path
below where the search started — `photos\2025\beach.jpg` — or by their full path
when the search started in several places. The title says what was searched for
and where (`Find: *.jpg in C:\photos`). Sorting, hidden entries, marking and the
quick search work as usual. `..` returns to where the panel stood before, `Enter`
on a directory goes into it, `Enter` on a file runs it, `Ctrl+R` drops results
that are gone, and `Alt+F7` searches again from where the panel stood.

From there the marked results can be copied (`F5`) or moved (`F6`) into the other
panel, deleted (`F8`, `Shift+F8`), wiped (`Alt+Del`) or edited (`F4`) — even
when they lie in different directories and inside archives, all in one go. A
mark inside an archive makes `F8` delete for good, since an archive has no
trash, and an archive of a kind Marbas cannot change (a plain `tar`, a `rar`) is
reported by name while the rest still goes ahead. What needs a real directory —
making a directory or a file, packing, unpacking, making a link, renaming — says
so instead, and so does copying or moving *into* a panel of results.

## Sorting and hidden entries

- `Ctrl+F3`…`Ctrl+F6` sort the active panel by name, extension, date or size;
  pressing the same key again reverses the direction.
- `Alt+H` shows or hides dotfiles and OS-hidden/system entries in the active
  panel.

Both settings, and the panel's path, are shown in its title bar and are
remembered per panel.

## Tabs

A tab is a whole pair of panels, not one panel: switching one changes the left
and the right side at once, along with which of them is active.

- `F11` (or `Alt+T`) lists the open tabs — one row per tab with its number and
  where both its panels stand. `Enter` goes to the one under the cursor, `Esc`
  changes nothing.
- `Alt+F11` (or `Alt+Z`) opens a new tab on the directories already on screen.
  Its cursors come along; the marks do not.
- `Alt+Shift+F11` (or `Alt+Shift+Z`) closes the tab you are on. The last one
  cannot be closed.
- `Alt+←` / `Alt+→` walk to the previous / next tab without the popup, wrapping
  round at the ends.

The top right corner says which tab is in front and how many there are —
`[2/3]`. It shares the line with the right panel's title, so a path that no
longer fits is shortened to `C:\...\parent\current`; the sort marks stay put.

Coming back to a tab re-reads both of its panels, so a directory that changed
while you were elsewhere is shown as it is now. Opening, switching and closing a
tab each write the session, and `Shift+F9` writes it at any time without
quitting.

Use `Alt+T` if `F11` does nothing: Windows Terminal and the Windows console keep
that key for their own full-screen toggle and never pass it on. Use `Alt+Z` and
`Alt+Shift+Z` if `Alt+F11` puts the window in full screen instead, as
gnome-terminal does under WSLg.

## The function-key bar

The bottom bar has one slot per function key, `F1` … `F12`, sharing the width
of the terminal evenly between them. Each slot names its key on a background
of its own and puts the command beside it, so `F5 Copy` reads as a key and a
command rather than as one word.

The key names never move. On Windows, holding `Ctrl`, `Alt` or `Shift` swaps
the *commands* for the sort keys, the drive popups, the link or the permanent delete
while the modifier is down — `F3` stays `F3` and its command becomes `Name`,
`F8` becomes `Erase`. A slot no command asks for stays empty. Held modifiers are a Windows-only feature (see
[Cross-platform](Architecture/CROSS-PLATFORM.md#differences-that-are-meant-to-be-there)).

Clicking a slot runs its command.

Twelve slots want room: below about 90 columns the longer commands
(`RenMov`, `Delete`) are clipped to fit. Only `Alt+H`, `Ctrl+H`, `Ctrl+R`,
`Ctrl+Q`, `Ctrl+4`, `Alt+Del`, `+`, `-`, `Insert` and the shifted arrows are off the bar altogether
— the bar shows function keys and nothing else.

`Ctrl+Q` or `F10` quits and saves the session.

## Settings

Quitting writes the current session — every tab, where each of its panels
stands, its sort order, whether it shows hidden entries and the entry its cursor
rested on — to a config file, restored on the next start. Opening, switching or
closing a tab writes it too, and `Shift+F9` writes it on demand:

| Platform | Location |
|----------|----------|
| Windows | `%APPDATA%\Marbas\settings.conf` |
| Linux / macOS | `~/.config/Marbas/settings.conf` |

The file is `key=value` lines meant to be readable and editable by hand; a
missing or partly unreadable file just falls back to defaults for whatever it
could not read, rather than refusing to start. Each tab is a numbered block
(`tab.1.left.path`, `tab.1.active`, …) and `tabs.current` says which one to open
on; a file written by a version from before tabs opens as a single tab and is
rewritten in the new spelling the next time the session is saved.

## Favourite directories

Ten directories can be kept under the digits `0` to `9`, the same ten for every
tab and both panels.

- `Alt+K` keeps the place the active panel stands in — a directory, or a level
  inside an archive. A popup, *Add to favourites*, names the path and asks for
  the digit — a path too long for the screen is shortened in the middle: press
  `0` … `9` and the
  place is kept under it, replacing whatever the digit held, without a further
  word. Any other key, `Esc` included, closes the popup and keeps nothing. A
  panel of search results has no place to keep.
- `Alt+0` … `Alt+9` send the active panel to the place under that digit. A digit
  that holds nothing does nothing; a place that is no longer there is reported
  and the panel stays where it was. Going to a favourite is remembered in the
  folder history like any other move.
- `Alt+F8` — or `Alt+B`, since a GNOME desktop keeps `Alt+F8` for itself —
  opens *Favourites*: the ten digits in order, each with its place, a long path
  shortened in the middle, and an empty one with its digit alone. `Enter` goes to
  the place under the cursor and a digit to the place under that digit; a digit
  that holds nothing moves the cursor onto it instead. `Ins` adds the active
  panel's path under the digit the cursor is on, replacing what it held, and
  `Del` removes what the digit holds — both written at once, so they hold after
  a restart. `Esc` closes the popup. This is also the way to the digits where a
  terminal keeps `Alt+0` … `Alt+9` for itself.

The favourites live in a file of their own beside the settings —
`%APPDATA%\Marbas\favorites.conf` on Windows, `~/.config/Marbas/favorites.conf`
on Linux and macOS — written the moment one is kept, so a crash loses nothing
and two copies of Marbas running at once keep each other's. gnome-terminal
takes `Alt+1` … `Alt+0` for its own tabs once it has more than one.

## The user menu

`F2` opens *User menu*: the programs and commands you have defined, each on a
letter or a digit of its own, with lines between groups. Press an entry's
letter and it runs at once and the menu closes; `↑` and `↓` move between the
entries, stepping over the lines, and `Enter` runs the one under the cursor.
`Esc` closes the menu and runs nothing. Nothing Marbas starts is waited for:
the panels take keys again straight away, and what the program changes on the
disk shows up in them by itself.

An entry is a label, its letter, a command line, a way of running and whether
it runs as administrator. The command line names a program and its arguments,
words in double quotes counting as one, and may hold these:

| Write | For |
|-------|-----|
| `%n` | the name under the cursor |
| `%p` | its full path |
| `%s` | every marked entry's full path — the one under the cursor when nothing is marked |
| `%d` | the panel's directory |
| `%Ln`, `%Lp`, `%Ls`, `%Ld` | the same of the left panel |
| `%Rn`, `%Rp`, `%Rs`, `%Rd` | the same of the right panel |
| `%f` | a file listing the marked entries one per line, for a list too long for a command line |
| `%%` | a percent sign |

Without `L` or `R` a placeholder speaks of the active panel. Every path is put
in as one argument whatever spaces or quotes it holds, and `%s` puts in each
entry as an argument of its own, so the program is started once with all of
them. A `%` followed by anything else is kept as you typed it. In a panel of
search results a hit is named by its own name and path. A command is refused,
with a dialog saying why, when a placeholder names something inside an
archive, when `..` is under the cursor and nothing is marked, or when `%d`
names a panel of search results.

An entry runs one of three ways: the program is started directly, or the line
goes to the shell — `cmd /c` on Windows, whose window closes when the command
ends, or `cmd /k`, whose window stays open so you can read what it printed. On
Windows a console program gets a window of its own either way. Running as
administrator asks Windows for the rights the way `Alt+Shift+R` does; turning
the prompt down runs nothing and says nothing. The program starts in the
directory of the active panel — the one holding the archive when the panel is
inside one.

A command that ran is a row on the *History* tab of `Alt+F12`, showing its
label and the command line as it was expanded; `Enter` there runs it again
exactly as it ran, and `Alt+G` sends the active panel to the directory it ran
in.

The menu is edited where it is shown. A bare letter runs an entry, so the
editing keys are the ones a letter cannot be:

- `Ins` opens *Add to the menu* — the label, the letter, the command line, how
  it runs and *As administrator* — and the new entry goes right under the one
  the cursor is on. The same popup can add *a line above* or *a line below* the
  entry under the cursor instead; the fields are then not needed.
- `F4` opens the same popup on the entry under the cursor, to change it.
- `Del` asks before it removes anything. When a line stands right above or
  below the entry, the question offers that line as well as the entry; the
  answer offered is *Cancel*.
- `Ctrl+↑` and `Ctrl+↓` move the entry under the cursor one row; moving it past
  a line puts it on the other side of that line.

A letter another entry already runs on is refused in the popup, which stays
open and says which entry holds it; so is an entry without a label or a command
line. Every change is written to the file the moment it is made, so it holds
after a restart, and two copies of Marbas editing the menu at once keep each
other's entries.

The menu is kept beside the settings in `user-menu.conf`, and can be edited by
hand as well:

```
entry.1=command
entry.1.hotkey=E
entry.1.label=Edit in vim
entry.1.command=vim %p
entry.1.run=direct
entry.1.elevation=user
entry.2=separator
entry.3=command
entry.3.hotkey=Z
entry.3.label=Pack the marked files
entry.3.command=7z a %d\packed.7z %s
entry.3.run=shell-keep
```

`run` is `direct`, `shell-close` or `shell-keep`; `elevation` is `user` or
`administrator`. Either may be left out, for a direct start as the user.

## History

Every directory a panel goes to — by `Enter`, `..`, a drive, a root, a path
opened or a search hit — is remembered, newest first, a hundred at most, in a
file of its own beside the settings:

| Platform | Location |
|----------|----------|
| Windows | `%APPDATA%\Marbas\history.conf` |
| Linux / macOS | `~/.config/Marbas/history.conf` |

The file is written on every change, so a crash loses nothing, and two copies
of Marbas running at once share it rather than overwrite each other. If it
cannot be written, a dialog says so once and the session carries on.

`Alt+F12` opens it — `Alt+Shift+H` where a terminal keeps `Alt+F12` for
itself. The popup has three tabs, *History*, *View history* and *Folder
history*, each listing when and what, newest first; it opens on the tab it was
left on, *Folder history* the first time. `←` and `→` (or `Ctrl+PgDn` and
`Ctrl+PgUp`) walk the tabs; `↑` and `↓` only move the cursor and stop at the
first and the last row. On *Folder history*, `Enter` — or `Alt+G` (*Go to*),
which does the same there — sends the active panel to the directory under the
cursor, a level inside an archive included; `Esc` closes the popup.

*History* lists the files started — with `Enter`, with a program chosen in
`Alt+R`, or as administrator with `Alt+Shift+R` — and the commands run from the
`F2` menu, once they have started. The
program chosen follows the file's path in brackets, and a file started as
administrator is marked `admin`. `Enter` starts the file
under the cursor again exactly that way; `Alt+G` (*Go to*) puts the active
panel in the file's directory with the cursor on the file. A directory or a
drive shown in the system file manager is not listed, nor is a file that could
not be started.

*View history* lists the files opened with `F3`, marked `V`, and with `F4` or
`Shift+F4`, marked `E` — a file viewed and later edited is one row, now `E`. A
file inside an archive is listed with the archive's path in front of it. `Enter`
does again what the row says; `F3` views the file and `F4` edits it whatever
the row says, except that a file inside an archive can only be viewed. `Alt+G`
puts the active panel beside the file, inside the archive if that is where it
is. A directory measured with `F3` is not listed.

On any tab, `Del` (or *Delete*) forgets the entry under the cursor, and
`Shift+Del` forgets everything on the tab after asking — the answer offered is
*No*, so press `Y` to go ahead. The history file is changed at once, so an
entry forgotten here does not come back from another copy of Marbas running at
the same time, and a cleared tab is still empty the next time Marbas starts.

Typing in the popup filters the tab in front, the way typing in a panel
searches it: only the rows holding what was typed stay, wherever it appears in
them and whatever its case, and the text is shown under the table as
*Filter:*. `Backspace` takes back the last character, `Esc` clears the filter
(and a second `Esc` closes the popup), and moving to another tab clears it
too. `Shift+Del` clears the whole tab, not only the rows the filter shows.

The popups that ask one question remember what you typed: where `F6` moves to,
the names `F7` gave directories and `Shift+F4` gave files, and the patterns of
`+` and `-` (one list for both). Press `Alt+↓` or `F4` in the field to open its
list, newest first, with the newest row already chosen; `Enter` puts the chosen
row into the field, ready to edit or to confirm with a second `Enter`, and `Esc`
closes the list without closing the popup. `F7` still opens on the name the last
directory was given, and remembers a name only once the directory is made.

The larger popups remember too: the file name masks and the text of `Alt+F7`,
the archive of `Shift+F1`, where `Alt+F6` makes its link and the program chosen
in `Alt+R`. `Alt+↓` or `F4` opens the list of the field you are in; `↑` and `↓`
keep moving between the fields. The password of an archive is never kept.

## Things that differ by platform

- **Drives.** Windows lists the ready drives; Unix lists the mounts that hold
  files — the pseudo file systems (`proc`, `tmpfs`, snap `squashfs` loops and the
  like) are left out — followed by your home directory.
- **Links.** A junction can be made on Windows only. A symbolic link on Windows
  needs administrator rights or Developer Mode; Linux lets anyone make one.
- **`Ctrl+H`** does not toggle hidden entries on most terminals — it arrives
  as the byte `0x08`, indistinguishable from `Backspace`. Use `Alt+H`.
- **`F11`** never reaches Marbas under Windows Terminal or the Windows console,
  which keep it for full screen; cmder passes it through. Use `Alt+T` to list
  the tabs. A WSL session inherits whatever terminal it was started from, so the
  same Linux build answers `F11` under one and not under the other.
- **`Alt+F11`** puts gnome-terminal's window in full screen under WSLg and never
  reaches Marbas, and `Alt+Shift+F11` does not arrive there either. Use
  `Alt+Z` to open a tab and `Alt+Shift+Z` to close it.
- **`Alt+F7`** is taken by GNOME's window manager for moving a window, so on a
  GNOME desktop use `Alt+Shift+F` to find files. `PATH` is split at `;` on
  Windows and `:` on Unix.
- **The modifier-aware function-key bar is Windows-only** — no Unix terminal
  driver Marbas relies on reports a held `Ctrl`/`Alt` on its own.
- **The clipboard is Windows-only for now.** On Linux it needs `xclip` or
  `wl-clipboard` for text, which Marbas does not call yet; copying there is
  refused, and the refusal is shown once per session.
- **`F4` waits for the editor on Unix.** The editor is given the terminal
  until it closes, so the panels are out of reach meanwhile. On Windows the
  editor gets a window of its own and is not waited for.

See [Cross-platform](Architecture/CROSS-PLATFORM.md) for the full picture and
the reasons behind these differences.
