# Panels, the cursor and sorting

Each panel lists one directory and keeps its own cursor, sort order and choice
of hidden entries. The panels follow the disk on their own.

## Moving the cursor

| Key | Alternative | Action |
|-----|-------------|--------|
| `↑` `↓` | | One entry up or down |
| `PgUp` `PgDn` | | One page up or down |
| `Home` `End` | | The first or the last entry |
| `←` | | The top of the page, then a page back |
| `→` | | The bottom of the page, then a page forward |
| `Enter` | | Open the directory under the cursor |

- The cursor stops at the first and the last entry. Only `Tab` moves to the
  other panel.
- `Enter` on `..` goes up and puts the cursor on the directory just left.
- `Enter` on a file runs it, see [Running files](10-running-files.md); on an
  archive it opens the archive, see [Archives](07-archives.md).
- More ways to change directory are in
  [Directories and favourites](06-directories-and-favourites.md).

## Quick search

Typing a printable character opens a search at the bottom of the panel,
`Search: …`. The cursor moves to the first entry whose name holds the text,
anywhere in the name, whatever the case.

| Key | Alternative | Action |
|-----|-------------|--------|
| `Backspace` | | Take back a character; the last one closes the search |
| `Esc` | | Close the search |

- Any other key closes the search and then does its own job, so `Enter` opens
  what was found and `F5` copies it.
- While the search is closed, `Space`, `+`, `-` and `*` mark entries, see
  [Marking](03-marking.md). While it is open they are typed into it.
- Switching panels or reading the panel again closes the search, and the
  cursor stays where the search left it.

## Reading the panel again

A file or a directory another program adds, renames or removes shows up in
the panels on screen within a fraction of a second, without a key.

| Key | Alternative | Action |
|-----|-------------|--------|
| `Ctrl+R` | | Read the active panel again |

- The cursor stays on its entry, and the marks of the entries still there stay.
- Only the tab in front is watched. Another tab is read again when you switch
  to it.
- Nothing is read again while a popup or a progress window is open.
- If the directory a panel shows is deleted, the panel steps up to the
  nearest one that is still there. If its drive is removed, it moves to a root
  that exists.

> [!NOTE]
> A network share does not always report its changes. Press `Ctrl+R` there.

## Sorting

| Key | Alternative | Action |
|-----|-------------|--------|
| `Ctrl+F3` | | Sort by name |
| `Ctrl+F4` | | Sort by extension |
| `Ctrl+F5` | | Sort by date |
| `Ctrl+F6` | | Sort by size |

- Pressing the key of the sort in use reverses it. The title shows the sort,
  such as `[size ↑]`.
- `..` stays first and directories stay above files. Entries the key cannot
  tell apart are sorted by name.
- Names are sorted the way a reader expects: punctuation before digits, digits
  before letters, case ignored.
- The cursor stays on its entry and the marks stay as they are.
- Each panel keeps its sort through every directory it shows, and the session
  keeps it after a restart.
- By size, a directory counts with the size `F3` measured, and as nothing
  until it is measured.

## Hidden entries

An entry is hidden when its name starts with a dot, or when the system marks
it hidden or system. Hidden entries are not shown until asked for.

| Key | Alternative | Action |
|-----|-------------|--------|
| `Alt+H` | `Ctrl+H` | Show or hide the hidden entries |

- The title says `hidden` while they are shown, and shown entries take their
  place in the sort order.
- A mark on an entry that is hidden again is dropped, so nothing is acted on
  unseen.
- `..` is never hidden.

> [!NOTE]
> Most terminals send `Ctrl+H` as `Backspace`, so it works only where the
> terminal reports it as a key of its own. `Alt+H` works everywhere.

## Sizes

| Key | Alternative | Action |
|-----|-------------|--------|
| `F3` | | On a directory, measure its size |

- `F3` measures the marked directories, or the directory under the cursor, and
  writes the total in the size column in place of `<DIR>`. On `..` it measures
  the directory `..` leads to. On a file `F3` opens the
  [viewer](11-viewer.md).
- The measurement runs behind a progress window and `Esc` stops it.
- A `≥` before a size means something inside could not be read, or the
  measurement was stopped, so the real size is at least that.
- The sizes are forgotten when the panel is read again.
- The listing rounds sizes to their unit, such as `1.5 KB` or `59.6 GB`. The
  status line and the line under the listing give the exact number of bytes.
