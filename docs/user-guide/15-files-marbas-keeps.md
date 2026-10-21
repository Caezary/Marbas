# The files Marbas keeps

Marbas keeps its files in one directory, and a working directory of its own
while it runs.

| Platform | Directory |
|----------|-----------|
| Windows | `%APPDATA%\Marbas` |
| Linux | `~/.config/Marbas`, or `$XDG_CONFIG_HOME/Marbas` when that is set |

| File | Holds | Written |
|------|-------|---------|
| `settings.conf` | the session and what Setup holds | on quitting, on a change of tab, on `Shift+F9` |
| `favorites.conf` | the ten favourites | when a favourite changes |
| `history.conf` | the three histories and what the fields remember | on every change |
| `user-menu.conf` | the user menu | on every change of the menu |
| `marbas.log` | crashes, and with `--debug` what happened | as it happens |

- The `.conf` files are `key=value` lines, readable and editable by hand.
- A file that is missing holds nothing yet: Marbas starts without it and makes
  it when there is something to write.
- `favorites.conf`, `history.conf` and `user-menu.conf` are read before every
  change and written at once, so two copies of Marbas running at once keep
  each other's changes.

## The settings

| Line | Means |
|------|-------|
| `tabs.current=2` | the tab in front, counted from 1 |
| `tab.1.active=left` | the active panel of tab 1, `left` or `right` |
| `tab.1.left.path=C:\work` | where the left panel of tab 1 stands |
| `tab.1.left.sort=name` | `name`, `extension`, `date` or `size` |
| `tab.1.left.direction=ascending` | `ascending` or `descending` |
| `tab.1.left.hidden=concealed` | `concealed` or `shown` |
| `tab.1.left.cursor=notes.txt` | the entry the cursor is on |
| `editor.default=C:\Tools\edit.exe` | the default editor of Setup |
| `program.editing.txt=C:\Tools\edit.exe` | the program `txt` is *Edited with* |
| `program.opening.zip=C:\Tools\unpack.exe` | the program `zip` is *Opened with* |

- `right` takes the place of `left` for the right panel.
- A line missing, wrong or misspelled falls back to what a first start would
  use, and a directory that is no longer there to the directory Marbas was
  started in.
- A file written by a version from before tabs opens as one tab, and is
  written in this form the next time the session is saved.
- When the file cannot be written, Marbas quits without a word.

## The user menu

| Line | Means |
|------|-------|
| `entry.1=command` | the first row is an entry |
| `entry.1.hotkey=E` | its letter |
| `entry.1.label=Edit` | its label |
| `entry.1.command=edit %p` | its command line |
| `entry.1.run=direct` | `direct`, `shell-close` or `shell-keep` |
| `entry.1.elevation=user` | `user` or `administrator` |
| `entry.2=separator` | the second row is a line |

- `run` and `elevation` may be left out, for a direct start as the user.
- The placeholders of the command line are in
  [Running files](10-running-files.md#what-an-entry-holds).

## The log

`marbas.log` lies beside the settings. Each start cuts it to its last
megabyte, and every line is stamped with the moment it was written.

- A crash is written with its stack trace and the version of Marbas. Once the
  terminal is back, one line says where the log is, and Marbas exits with
  code 1.
- `--debug` makes it record more for one session, see
  [The command line](14-command-line-and-environment.md#switches).

## The working directory

While it runs, Marbas keeps a working directory under the system's temporary
directory: `%TEMP%\Marbas\<session>` on Windows, `/tmp/Marbas/<session>` on
Linux.

- It holds the entries unpacked for the clipboard, the list file of `%f` in
  the user menu, and what crosses between two archives.
- Quitting with `F10` or `Ctrl+Q` removes it.
- When a session ends otherwise, closed with the window's button, killed or
  crashed, the next start removes what it left, in the background, after the
  panels are on screen. The directory of a copy of Marbas still running is
  kept.
- A directory that cannot be removed, because a file in it is held open, is
  left for the next start and written to the log.
