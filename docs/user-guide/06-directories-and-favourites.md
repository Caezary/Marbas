# Directories and favourites

Besides walking with `Enter`, a panel can go up a level, to the root, to a
drive or to a place kept under a digit. Every place a panel lands on is
remembered in the folder history, see [History](13-history.md).

## Going up and to the root

| Key | Alternative | Action |
|-----|-------------|--------|
| `Ctrl+PgUp` | `Alt+U` | Up one level, wherever the cursor is |
| `Ctrl+4` | | The root of the drive the panel is on |

- `Ctrl+PgUp` does what `Enter` on `..` does: the parent directory, with the
  cursor on the directory just left. Inside an archive it goes to the level
  above, and at its top out of the archive onto its file. From search results
  it goes back to where the search started. On a root it does nothing.
- `Ctrl+4` leaves an archive the panel is inside. On a root it does nothing.

> [!NOTE]
> Terminals send `Ctrl+\` as `Ctrl+4`, so either can be pressed.
> gnome-terminal keeps `Ctrl+PgUp` for its own tabs once it has several;
> press `Alt+U`.

## Drives

| Key | Alternative | Action |
|-----|-------------|--------|
| `Alt+F1` | | Choose a drive for the left panel |
| `Alt+F2` | | Choose a drive for the right panel |

*Drives* lists one row per drive: its root, label, kind, size and free space.
On Windows these are the drives that are ready; on Linux the mounts that hold
files, then the home directory. The list is read each time it opens.

| Key | Alternative | Action |
|-----|-------------|--------|
| `Enter` | | Go to the drive under the cursor |
| `Alt+R` | | Open the drive in the system file manager |
| `Esc` | | Close, changing nothing |

- If the other panel is on the drive chosen, the panel goes to the other
  panel's directory; any other drive opens at its root.
- `Alt+R` leaves both panels where they are.

> [!NOTE]
> A GNOME desktop keeps `Alt+F1` and `Alt+F2` for itself, and Marbas has no
> second key for them yet.

## Favourites

Ten places can be kept under the digits `0` to `9`, the same ten for every tab
and both panels. A place is a directory or a level inside an archive.

| Key | Alternative | Action |
|-----|-------------|--------|
| `Alt+K` | | Keep the active panel's place under a digit |
| `Alt+0` … `Alt+9` | | Go to the place kept under the digit |
| `Alt+F8` | `Alt+B` | List the favourites |

- `Alt+K` names the place and waits for a digit, which keeps the place under
  it and replaces what the digit held. Any other key keeps nothing.
- A panel of search results has no place to keep.
- A digit that holds nothing does nothing. A place that is no longer there is
  reported, and the panel stays.
- The favourites are written the moment they change, so two copies of Marbas
  running at once keep each other's.

### The list of favourites

The ten digits in order, each with its place.

| Key | Alternative | Action |
|-----|-------------|--------|
| `Enter` | | Go to the place under the cursor |
| `0` … `9` | | Go to the place under the digit |
| `Ins` | | Keep the active panel's place under the cursor |
| `Del` | | Empty the digit under the cursor |
| `Esc` | | Close the list |

- A digit that holds nothing moves the cursor onto it.

> [!WARNING]
> On Linux `Alt+K` closes its popup before a digit can be pressed. Keep a
> place with `Ins` in the list of favourites instead.

> [!NOTE]
> gnome-terminal keeps `Alt+1` … `Alt+0` for its own tabs once it has several,
> and a GNOME desktop keeps `Alt+F8`. Press `Alt+B` and choose from the list.
