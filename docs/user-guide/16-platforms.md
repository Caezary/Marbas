# Differences between platforms

Marbas runs on Windows and on Linux. Most of it behaves the same on both; what
differs is collected here, and each chapter says it again beside the key it
concerns.

## Keys a terminal or a desktop keeps

| Key | Kept by | Press instead |
|-----|---------|---------------|
| `F11` | Windows Terminal and the Windows console, for full screen | `Alt+T` |
| `Alt+F11` | gnome-terminal under WSLg, for full screen | `Alt+Z` |
| `Alt+Shift+F11` | gnome-terminal under WSLg | `Alt+Shift+Z` |
| `Ctrl+PgUp` | gnome-terminal with several tabs | `Alt+U` |
| `Alt+0` … `Alt+9` | gnome-terminal with several tabs | `Alt+B` |
| `Alt+F7` | a GNOME desktop, to move a window | `Alt+Shift+F` |
| `Alt+F8` | a GNOME desktop, to resize a window | `Alt+B` |
| `Alt+F1` `Alt+F2` | a GNOME desktop | nothing yet |
| `Alt+F6` | a GNOME desktop | nothing yet |
| `Ctrl+H` | most terminals, which send it as `Backspace` | `Alt+H` |
| `Ctrl+PgDn` | some terminals | `Alt+W` |

- A WSL session runs in the terminal it was started from, so the same Linux
  binary receives `F11` in one terminal and not in another.
- Terminals send `Ctrl+\` as `Ctrl+4`, so either can be pressed for the
  [root](06-directories-and-favourites.md#going-up-and-to-the-root).
- The grey `+`, `-` and `*` arrive as the keys of the main keyboard, and not
  at all with a modifier held, see [Marking](03-marking.md#taking-marks-back).

## What works differently

- **Drives.** Windows lists the drives that are ready. Linux lists the mounts
  that hold files, leaving out `proc`, `tmpfs`, snap loops and the like, and
  then the home directory. See
  [Drives](06-directories-and-favourites.md#drives).
- **Favourites.** On Linux `Alt+K` closes its popup before a digit can be
  pressed; keep a place with `Ins` in
  [the list of favourites](06-directories-and-favourites.md#the-list-of-favourites).
- **Links.** A junction exists on Windows only. Windows makes a symbolic link
  only for an administrator or with Developer Mode on; Linux lets anyone make
  one. See [Links](05-file-operations.md#links).
- **The trash.** Windows uses the Recycle Bin, Linux the home trash under
  `~/.local/share/Trash`. See
  [Deleting](05-file-operations.md#deleting-and-wiping).
- **The editor.** On Windows Marbas does not wait for it. On Linux it gets the
  terminal until it closes. See [Editing](05-file-operations.md#editing).
- **Running files.** On Linux a file with its execute bit runs itself and the
  rest goes to `xdg-open`; `Alt+Shift+R` is refused. See
  [Running a file](10-running-files.md#running-a-file).
- **The shell of the user menu** is `cmd` on Windows and `sh` on Linux.
- **The clipboard** works on Windows only. See
  [The clipboard](08-clipboard.md).
- **The key bar** follows a held `Ctrl`, `Alt` or `Shift` on Windows only. See
  [The key bar](01-getting-started.md#the-key-bar).
- **Names.** On Linux a backslash is part of a file name, inside a zip or a
  tar.gz too. See [Archives](07-archives.md#changing-an-archive).
- **Properties.** Windows offers read-only, hidden, system and archive; Linux
  offers read-only. See
  [Properties](05-file-operations.md#properties-of-a-directory).
- **Where the files are.** See
  [The files Marbas keeps](15-files-marbas-keeps.md) and
  [the environment](14-command-line-and-environment.md#variables).
