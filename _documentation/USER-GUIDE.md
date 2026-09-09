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
- `Ctrl+R` reloads the active panel.
- `Alt+F1` / `Alt+F2` open a root chooser for the left / right panel — the
  ready drives on Windows, the file-system root and your home directory on
  Unix.
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
- `F3` opens the file under the cursor in a read-only viewer (binary files
  show as a hex dump); `F3`/`Esc` closes it.
- `F4` opens it in your `$EDITOR`, or the platform default (`notepad` on
  Windows, `vi` on Unix).

Copying and moving run in the background behind a progress window; `Esc`
stops the batch at the next file.

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
the *commands* for the sort keys, the root choosers or the permanent delete
while the modifier is down — `F3` stays `F3` and its command becomes `Name`,
`F8` becomes `Erase`. A slot no command asks for stays empty. Held modifiers are a Windows-only feature (see
[Cross-platform](Architecture/CROSS-PLATFORM.md#differences-that-are-meant-to-be-there)).

Clicking a slot runs its command.

Twelve slots want room: below about 90 columns the longer commands
(`RenMov`, `Delete`) are clipped to fit. Only `Alt+H`, `Ctrl+H`, `Ctrl+R`,
`Ctrl+Q`, `+`, `-`, `Insert` and the shifted arrows are off the bar altogether
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

- **Drive/root chooser.** Windows lists the ready drives; Unix lists the
  file-system root and your home directory.
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
