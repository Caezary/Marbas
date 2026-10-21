# The clipboard

What the panel points at can be handed to other programs through the system
clipboard, as text or as files. Each command takes the marked entries, or the
entry under the cursor when nothing is marked.

| Key | Alternative | Action |
|-----|-------------|--------|
| `Ctrl+Ins` | `Alt+C` | Copy the names as text |
| `Alt+Shift+Ins` | `Alt+P` | Copy the full paths as text |
| `Ctrl+Alt+Ins` | `Alt+L` | Copy where the links lead as text |
| `Ctrl+Shift+Ins` | `Alt+J` | Go to the path on the clipboard |
| `Alt+Shift+C` | | Put the files on the clipboard to be copied |
| `Alt+Shift+X` | | Put the files on the clipboard to be moved |
| `Alt+Shift+V` | | Paste the files on the clipboard here |

> [!WARNING]
> The clipboard works on Windows only. On Linux the first attempt in a session
> says so, and later ones stay quiet.

## Text

- One line per entry, in the order of the listing, with no quotes, no
  separator after a directory and no line break after the last.
- A full path inside an archive is spelled through the archive, as the title
  spells it: `C:\work\backup.zip\docs\notes.txt`.
- In a panel of search results each hit gives its own name and its own path.
- With `..` under the cursor and nothing marked, nothing is copied, except
  that the full path is then the place the panel shows.
- Where a link leads is the target of a symbolic link or a junction, made a
  full path, or the file a `.lnk` shortcut names. Anything else gives its own
  full path. Only the entry itself is followed.

## Going to a path

`Ctrl+Shift+Ins` sends the active panel to the path on the clipboard: into a
directory, or beside a file with the cursor on it.

- Spaces and quotes around the text are dropped, and a `file:///` address is
  read as the path it names.
- `~` is the home directory, and environment variables are expanded: `%TEMP%`
  on Windows, `$HOME` or `${HOME}` on Linux.
- A relative path is taken from the directory of the active panel.
- A path into an archive Marbas reads, such as
  `C:\work\backup.zip\docs\notes.txt`, opens the archive at that level.
- Several lines, an empty clipboard and a path that does not exist change
  nothing and say nothing.

## Files

- Files put on the clipboard can be pasted by Explorer, a mail program or any
  program that takes files. Files put there to be moved leave their place when
  pasted.
- Entries inside an archive are first unpacked into the
  [working directory](15-files-marbas-keeps.md#the-working-directory), and
  those copies go on the clipboard. They last until Marbas quits.
- Entries inside an archive cannot be put there to be moved.
- `Alt+Shift+V` pastes the files on the clipboard into the place the active
  panel shows, whoever put them there: copied or moved, as `F5` and `F6` do,
  with the same question before replacing an entry.
- Pasting into an archive that can be changed adds the files to it.
- After a move the clipboard is emptied, so the same files are not moved
  twice.
- A clipboard holding no files pastes nothing and says nothing. Files
  Explorer cut or copied from inside a zip may be offered in a form Marbas
  does not read, and then nothing is pasted.

> [!NOTE]
> Windows Terminal uses `Ctrl+Ins` to copy text that is selected on screen,
> and passes the key on when nothing is selected.
