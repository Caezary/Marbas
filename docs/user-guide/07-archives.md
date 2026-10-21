# Archives

Marbas packs entries into an archive, unpacks one, and opens it in a panel as
though it were a directory.

| Kind | Opened in a panel | Created and changed |
|------|-------------------|---------------------|
| zip | yes | yes |
| tar.gz | yes | yes |
| 7z | yes | yes |
| tar | yes | no |
| tar.bz2 | yes | no |
| tar.xz | yes | no |
| rar | yes | no |

## Packing and unpacking

| Key | Alternative | Action |
|-----|-------------|--------|
| `Shift+F1` | | Pack into an archive in the other panel |
| `Shift+F2` | | Unpack into the other panel's directory |

- The archive is named after what is packed: one entry gives its own name,
  several give the name of their directory, and a drive root gives a name
  such as `diskCcontents`.
- The popup offers zip, tar.gz or 7z, three levels of compression and a
  password. Switching the kind changes the extension of the name; a bare name
  puts the archive beside the entries instead.
- Only a zip can carry a password. It is AES-256: the names inside stay
  readable, and only the contents are locked.
- Directories go in whole, empty ones included, with their dates.
- An archive that exists already is replaced only after a question. An archive
  that would land inside what it packs is refused.
- `Esc` stops packing and leaves no half-written archive.
- `Shift+F2` writes what each archive holds straight into the other panel's
  directory, with the dates of its entries. An archive with a password asks
  for it once, and an entry already there asks the question `F5` asks.
- A file that is no archive Marbas can read is listed as failed.

> [!NOTE]
> Explorer cannot open a zip locked with AES. 7-Zip can.

## Browsing an archive

| Key | Alternative | Action |
|-----|-------------|--------|
| `Enter` | | Open the archive under the cursor |
| `Alt+W` | `Ctrl+PgDn` | Walk into the archive or directory under the cursor |

- The title spells the place through the archive, such as
  `C:\work\backup.zip\docs`.
- `..` at the top level leaves the archive, with the cursor on its file.
- Sorting, hidden entries, marking, the quick search and `Ctrl+R` work as on
  disk.
- `Alt+W` opens an archive even when [Setup](12-settings.md#setup) names a
  program the extension is *Opened with*, which `Enter` would start instead.
- `F3` views a file inside the archive, asking once for a password if the
  archive has one. `F4` refuses: an entry cannot be edited in place.
- The session keeps the directory holding the archive, so a restart opens
  there.

> [!NOTE]
> Some terminals keep `Ctrl+PgDn` for themselves. `Alt+W` arrives everywhere.

## Changing an archive

From a panel inside an archive, the ordinary keys work on it.

- `F5` copies out into the other panel, with the dates of the entries, and
  leaves the archive as it was.
- Into a zip, a tar.gz or a 7z, `F5` from the other panel adds entries at the
  level shown, `F7` makes a directory, `F8` removes entries for good, and `F6`
  moves in either direction.
- A kind that cannot be changed, and an archive locked with a password, refuse
  by name before anything is asked.
- With an archive in each panel, `F5` and `F6` carry entries from one to the
  other.
- Every change runs behind the progress window. `Esc` leaves the archive as it
  was.
- `Shift+F1` and `Shift+F4` do nothing inside an archive.

> [!NOTE]
> On Linux a backslash is part of a file name, and it stays part of the name
> inside a zip or a tar.gz.
