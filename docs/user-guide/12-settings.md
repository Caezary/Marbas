# Settings

*Setup* chooses the programs Marbas starts. Everything else Marbas keeps, it
keeps from what you do: the session is saved as you leave it.

## Setup

| Key | Alternative | Action |
|-----|-------------|--------|
| `F12` | | Open Setup |

- *Default editor* is the program `F4` starts for an extension that has none
  of its own. Left blank, `F4` falls back to `EDITOR`, then to `notepad` on
  Windows and `vi` on Linux.
- Below it, an extension and a program are added to one of two lists, chosen
  by *Used for*: *Edited with* is what `F4` starts, and *Opened with* is what
  `Enter` starts.
- *Add* puts the pair in the list, replacing the extension's program if it has
  one; *Remove* takes it out. `Enter` in either field does what *Add* does.
- Moving the cursor over a row fills both fields from it and chooses its list.
- An extension is written with or without its dot, in any case: `.TXT` and
  `txt` are the same.
- What *Setup* holds is saved with the session.

In Setup:

| Key | Alternative | Action |
|-----|-------------|--------|
| `Alt+U` | | Fill the program field with the program under the cursor |

- *Under cursor* takes the program the panel cursor stood on when `F12` was
  pressed, so a program can be chosen by walking to it.
- A program is what the panel colours as one: an `.exe`, `.com`, `.bat`,
  `.cmd` or `.msi` on Windows, a file with its execute bit on Linux. On
  anything else the button is greyed out.

## The session

| Key | Alternative | Action |
|-----|-------------|--------|
| `Shift+F9` | | Save the session now |

- The session is every tab: where each panel stands, how it sorts, whether it
  shows hidden entries, the entry its cursor is on, and which panel is active.
  It also holds the tab in front and what *Setup* holds.
- It is saved on quitting, on opening, switching or closing a tab, and on
  `Shift+F9`.
- The next start opens it again. A directory that is no longer there is
  replaced by the directory Marbas was started in. A panel inside an archive
  comes back in the directory holding the archive.
- The session is kept in `settings.conf`, which can be edited by hand, see
  [The files Marbas keeps](15-files-marbas-keeps.md#the-settings).
