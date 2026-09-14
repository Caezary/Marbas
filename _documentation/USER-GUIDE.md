# User guide

Marbas is a full-screen, keyboard-driven, two-panel file manager for the
terminal, in the spirit of the orthodox managers of the DOS era.

## Running Marbas

Download the binary for your platform from the
[latest release](https://github.com/Caezary/Marbas/releases) and run it —
each one is self-contained and carries its own .NET runtime, so nothing else
needs to be installed. On Linux and macOS, mark the file executable first
(`chmod +x Marbas`).

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
  directory. The list is read each time it opens, so a stick plugged in a moment
  ago is already there. In the popup:
  - `Enter` goes to the drive. If the *other* panel is on that drive, the panel
    goes to the other panel's directory, so both end up in one place; any other
    drive opens at its root.
  - `Alt+R` opens the drive in the system file manager (Explorer, or whatever
    `xdg-open` starts) and leaves both panels where they are.
  - `Esc` changes nothing.
- `Ctrl+4` (or `Ctrl+\`) sends the active panel to the root of the drive it is
  on. Terminals turn `Ctrl+\` into `Ctrl+4` before Marbas sees it, so both are
  bound and `Ctrl+4` is the one that works everywhere.
- Typing a printable character opens a search in the active panel: the
  cursor jumps to the first entry whose name contains what you typed.
  `Backspace` edits it, `Esc` or any other key closes it (and that other key
  still does its own job).

## Marking files

- `Insert`, `Space` mark the entry under the cursor and move down;
  `Shift+↑`/`Shift+↓` do the same one entry at a time in either direction.
- `Shift+PgUp`/`Shift+PgDn` (or `Shift+←`/`Shift+→`) mark or unmark a whole
  page at once.
- `+` / `-` mark or unmark everything matching a wildcard pattern (`*.fs`).

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
  show as a hex dump); `F3`/`Esc` closes it. On a directory — or with directories
  marked — `F3` measures them instead: the size replaces `<DIR>` in the size
  column and shows on the status line. A `≥` in front means something inside
  could not be read, so the real size is at least that. Sorting by size
  (`Ctrl+F6`) takes the measured sizes into account. The sizes are forgotten
  when the panel is read again.
- `F4` opens it in your `$EDITOR`, or the platform default (`notepad` on
  Windows, `vi` on Unix).

Copying, moving, wiping and measuring run in the background behind a progress
window; `Esc` stops them. A wipe stopped part way leaves the file it was working
on in place, already overwritten, and says which one it was. A measurement
stopped part way changes nothing.

Links are shown in bright cyan, and the status line shows where the link under
the cursor points (`twin -> C:\data\docs`). Copying, moving, deleting, packing,
measuring and wiping treat a link to a directory as the link itself and never go
inside it: a copy is a new link to the same place, and deleting removes only the
link.

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
- `Alt+F11` opens a new tab on the directories already on screen. Its cursors
  come along; the marks do not.
- `Alt+Shift+F11` closes the tab you are on. The last one cannot be closed.
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
that key for their own full-screen toggle and never pass it on.

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
- **The modifier-aware function-key bar is Windows-only** — no Unix terminal
  driver Marbas relies on reports a held `Ctrl`/`Alt` on its own.
- **`F4` with a full-screen console editor** (`vi`, `nano`) does not work
  well on Unix yet, because the terminal is shared with a driver that has not
  let go of it. Use a windowed editor, or `EDITOR="code -w"`, until this is
  fixed.

See [Cross-platform](Architecture/CROSS-PLATFORM.md) for the full picture and
the reasons behind these differences.
