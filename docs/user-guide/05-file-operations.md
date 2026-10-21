# File operations

Every operation acts on the marked entries of the active panel, or on the
entry under the cursor when nothing is marked. `..` is never acted on.

## Copying and moving

| Key | Alternative | Action |
|-----|-------------|--------|
| `F5` | | Copy into the other panel's directory |
| `F6` | | Move into a directory, or rename |

- `F5` asks nothing until an entry is already there. Then it asks *Yes*, *No*,
  *Yes to all*, *No to all* or *Cancel*: *No* skips that entry, the two *to
  all* answers settle every later clash, and *Cancel* drops the rest.
- `F6` asks where to, filled with the other panel's directory. Accept it to
  move there. Type a bare name instead to rename a single entry where it
  stands; with several entries marked, a bare name is a directory beside them.
- Directories are copied and moved with everything inside them.
- A copy or a move into the entry itself, or below it, is refused before
  anything is asked.
- Afterwards both panels are read again. The entries done lose their marks;
  the ones that failed keep them and are listed in one dialog.

## The progress window

Copying, moving, deleting, wiping, packing, unpacking and measuring run behind
a progress window that names the entry being worked on.

| Key | Alternative | Action |
|-----|-------------|--------|
| `Esc` | | Stop at the next entry |

- What is already done stays done, and the entries not reached keep their
  marks.
- A move stopped part way never deletes the source of the entry it was on.

## Making directories and files

| Key | Alternative | Action |
|-----|-------------|--------|
| `F7` | | Make a directory |
| `Shift+F4` | | Make a file and open it in the editor |

- `F7` offers the name the last directory was given, selected, so typing
  replaces it.
- `Shift+F4` on the name of an existing file opens that file as it is. The
  name of a directory is refused.
- The cursor lands on the new entry and the marks stay.

## Deleting and wiping

| Key | Alternative | Action |
|-----|-------------|--------|
| `F8` | `Del` | Send to the trash |
| `Shift+F8` | `Shift+Del` | Delete for good |
| `Alt+Del` | | Wipe: overwrite, rename, then delete |

- Each asks first, in the words of what is about to happen, and warns when a
  directory is not empty.
- The trash is the Recycle Bin on Windows and the home trash of the desktop,
  `~/.local/share/Trash`, on Linux.
- Inside an archive, `F8` deletes for good, since an archive has no trash.
- Wiping overwrites every file with random bytes, renames it and deletes it. A
  wipe stopped part way leaves the file it was on, already overwritten, and
  says which one it was.
- A link is deleted or wiped as a link; what it points at is left alone.

> [!WARNING]
> On a solid-state disk, or on a copy-on-write or journaling file system, the
> old data may survive a wipe. The question before the wipe says so.

## Links

| Key | Alternative | Action |
|-----|-------------|--------|
| `Alt+F6` | | Make a link |

- The popup offers the other panel's directory and the name of the entry, and
  the kinds the system can make: a symbolic link, a hard link and, on
  Windows, a junction.
- A kind that cannot point at what you typed is greyed out with the reason
  beneath: a hard link only to a file on the same drive, a junction only to a
  directory.
- With several entries marked, one link each is made inside the directory you
  typed.
- Copying, moving, deleting, packing, measuring and wiping treat a link to a
  directory as the link itself and never go inside it. A copy is a new link to
  the same place.

> [!NOTE]
> Windows makes a symbolic link only for an administrator or with Developer
> Mode on, and says so when neither holds. Junctions exist on Windows only.

## Editing

| Key | Alternative | Action |
|-----|-------------|--------|
| `F4` | | Edit the file, or show the directory's properties |

- The editor is the program the extension is *Edited with* in
  [Setup](12-settings.md#setup); else the default editor named there; else the
  one `EDITOR` names; else `notepad` on Windows and `vi` on Linux.
- A file saved in the editor shows up in the panel by itself.
- A file inside an archive cannot be edited; `F4` says so.

> [!NOTE]
> On Windows Marbas does not wait for the editor: a console editor opens in a
> window of its own and the panels go on taking keys. On Linux the editor gets
> the whole terminal, and the panels come back when it closes.

### Properties of a directory

`F4` on a directory opens its properties.

- The flags the platform keeps can be ticked: read-only, hidden, system and
  archive on Windows, read-only on Linux.
- The created, modified and accessed times are written as
  `2024-03-01 09:05:07`. A time that is not a date leaves the one the
  directory had.
- A directory made hidden disappears from a panel that hides hidden entries.
