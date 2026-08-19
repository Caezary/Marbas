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
- `F8` deletes, after confirming (with an extra warning for non-empty
  directories).
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

## The function-key bar

The bottom bar always shows the classic keys (`F3 View` … `F10 Quit`). On
Windows, holding `Ctrl` or `Alt` temporarily swaps it for the sort keys or
the root choosers; this is a Windows-only feature (see
[Cross-platform](Architecture/CROSS-PLATFORM.md#differences-that-are-meant-to-be-there)).

`Ctrl+Q` or `F10` quits and saves the session.

## Settings

Quitting writes the current session — where each panel stands, its sort
order and whether it shows hidden entries — to a config file, restored on the
next start:

| Platform | Location |
|----------|----------|
| Windows | `%APPDATA%\Marbas\settings.conf` |
| Linux / macOS | `~/.config/Marbas/settings.conf` |

The file is `key=value` lines meant to be readable and editable by hand; a
missing or partly unreadable file just falls back to defaults for whatever it
could not read, rather than refusing to start.

## Things that differ by platform

- **Drive/root chooser.** Windows lists the ready drives; Unix lists the
  file-system root and your home directory.
- **`Ctrl+H`** does not toggle hidden entries on most terminals — it arrives
  as the byte `0x08`, indistinguishable from `Backspace`. Use `Alt+H`.
- **The modifier-aware function-key bar is Windows-only** — no Unix terminal
  driver Marbas relies on reports a held `Ctrl`/`Alt` on its own.
- **`F4` with a full-screen console editor** (`vi`, `nano`) does not work
  well on Unix yet, because the terminal is shared with a driver that has not
  let go of it. Use a windowed editor, or `EDITOR="code -w"`, until this is
  fixed.

See [Cross-platform](Architecture/CROSS-PLATFORM.md) for the full picture and
the reasons behind these differences.
