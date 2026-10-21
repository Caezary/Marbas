# History

Marbas remembers the directories the panels went to, the files viewed,
edited and started, the commands run, and what was typed into its fields.

## The history popup

| Key | Alternative | Action |
|-----|-------------|--------|
| `Alt+F12` | `Alt+Shift+H` | Open the history |

The popup has three tabs, *History*, *View history* and *Folder history*, each
listing when and what, newest first. It opens on the tab it was left on, and
on *Folder history* the first time.

| Key | Alternative | Action |
|-----|-------------|--------|
| `←` | `Ctrl+PgUp` | The previous tab |
| `→` | `Ctrl+PgDn` | The next tab |
| `Enter` | | Do again what the row says |
| `Alt+G` | | Go to the place of the row |
| `F3` | | View the file of the row |
| `F4` | | Edit the file of the row |
| `Del` | | Forget the row |
| `Shift+Del` | | Forget the whole tab |
| `Backspace` | | Take back a character of the filter |
| `Esc` | | Clear the filter, or close the popup |

- `↑` and `↓` stop at the first and the last row.
- *Folder history* lists every directory a panel landed on, a level inside an
  archive included, a hundred at most. A directory visited again moves to the
  top. `Enter` and `Alt+G` send the active panel there.
- *History* lists the files started with `Enter`, `Alt+R` and `Alt+Shift+R`,
  with the program chosen in brackets and `admin` where it applies, and the
  commands run from the [user menu](10-running-files.md#the-user-menu).
  `Enter` starts one again exactly as it started. `Alt+G` puts the panel
  beside the file, or in the directory a command ran in.
- *View history* lists the files viewed with `F3`, marked `V`, and edited with
  `F4` or `Shift+F4`, marked `E`. `Enter` does again what the row says, `F3`
  views and `F4` edits whatever it says, and a file inside an archive can only
  be viewed. `Alt+G` puts the panel beside the file.
- `Shift+Del` asks first, and the answer offered is *No*.
- Typing filters the tab in front to the rows that hold the text, anywhere and
  whatever the case, shown under the table as *Filter:*. Switching tabs clears
  the filter. `Shift+Del` forgets the whole tab, not only the rows shown.
- Reading a panel again, sorting, showing hidden entries, switching tabs and a
  panel of search results add nothing to *Folder history*. Nor does a
  directory or a drive handed to the system file manager, or a file that did
  not start, add anything to *History*.
- The history is written on every change, so a crash loses nothing and two
  copies of Marbas running at once share it. If it cannot be written, one
  dialog says so and the session carries on.

## Fields that remember

Fields remember what was typed into them, newest first.

| Key | Alternative | Action |
|-----|-------------|--------|
| `Alt+↓` | `F4` | Open the list of the field |

In the list:

| Key | Alternative | Action |
|-----|-------------|--------|
| `Enter` | | Put the row into the field |
| `Esc` | | Close the list, keeping the popup |

- The fields are where `F6` moves to, the names of `F7` and `Shift+F4`, the
  patterns of `+` and `-`, which share a list, the masks and the text of
  [Finding files](09-finding-files.md), the archive of `Shift+F1`, the place
  of `Alt+F6`, the program of `Alt+R`, and the search of the
  [viewer](11-viewer.md#searching).
- The list opens with the newest row chosen. The row put into the field can be
  changed before it is confirmed.
- What was typed is remembered when its popup is confirmed. A popup cancelled
  and a blank field remember nothing. `F7` remembers a name only once the
  directory is made.
- In a popup of several fields, `↑` and `↓` move between the fields.
- The password of an archive is never remembered.
