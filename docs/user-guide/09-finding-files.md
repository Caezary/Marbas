# Finding files

Marbas searches by name and by text, on disk and inside archives, and shows
the results in a window of their own or in a panel.

| Key | Alternative | Action |
|-----|-------------|--------|
| `Alt+F7` | `Alt+Shift+F` | Find files |

> [!NOTE]
> A GNOME desktop keeps `Alt+F7` for moving a window; press `Alt+Shift+F`.

## What to look for

- **File names** are masks separated by `;`, such as `*.jpg;*.png`. `*` and
  `*.*` both mean every name, and a mask without `*` or `?` is found anywhere
  in a name, so `report` finds `2026-report.pdf`. Case never matters.
- **Containing text** left empty searches by name alone. With text, the case
  can be matched, whole words only can be matched, files *not* holding the
  text can be asked for, and binary files can be searched as well. The
  encoding of each file is detected, so a UTF-16 file is searched as text.
- **Where** is the panel's directory and everything below it, that directory
  alone, the marked directories, the whole drive the panel is on, every drive,
  or the directories on `PATH`.
- **Options** add directories to the results, include hidden entries, follow
  links to directories and look inside archives.
- The whole drive stops at other drives mounted inside it. A link that leads
  back to where it is followed from is not followed twice.
- *Last results* brings back the results of the last search, without what has
  been deleted since, and says how many went. They are forgotten when Marbas
  quits.

## The results window

The results fill a window while the search runs, grouped under the directory
each one is in.

| Key | Alternative | Action |
|-----|-------------|--------|
| `Enter` | | Put the panel on the result under the cursor |
| `F2` | | Send the results into the panel |
| `F3` | | View the file under the cursor |
| `Esc` | | Stop the search; a second `Esc` closes the window |
| `F10` | | Close the window |

- `Enter` on a directory header goes into that directory. A hidden result
  shows hidden entries, and a result inside an archive opens the archive.
- `Esc` in the viewer goes back to the list.

## Results in a panel

After `F2` the panel lists the results by their path below where the search
started, such as `photos\2025\beach.jpg`. The title says what was searched for
and where: `Find: *.jpg in C:\photos`.

- Sorting, hidden entries, marking and the quick search work as usual.
- `..` goes back to where the panel was. `Enter` goes into a directory and
  runs a file.
- `Ctrl+R` drops the results that are gone. `Alt+F7` searches again from where
  the panel was.
- `F5`, `F6`, `F8`, `Shift+F8`, `Alt+Del` and `F4` act on the marked results,
  even when they lie in different directories and archives.
- A mark inside an archive makes `F8` delete for good. An archive of a kind
  that cannot be changed is reported by name, and the rest goes ahead.
- What needs one real directory says so instead: `F7`, `Shift+F4`,
  `Shift+F1`, `Shift+F2`, `Alt+F6`, a rename with `F6`, and copying or moving
  into a panel of results.
