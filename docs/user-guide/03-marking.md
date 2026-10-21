# Marking

The marked entries are what the file operations act on; with nothing marked,
they act on the entry under the cursor. Marked entries are yellow, and the
line under the listing counts them and adds up their size.

## Marking entries one by one

| Key | Alternative | Action |
|-----|-------------|--------|
| `Ins` | `Space` | Mark or unmark, then move down |
| `Shift+↓` | | Mark or unmark, then move down |
| `Shift+↑` | | Mark or unmark, then move up |
| `Shift+PgDn` | `Shift+→` | Mark or unmark a page downwards |
| `Shift+PgUp` | `Shift+←` | Mark or unmark a page upwards |

- Every key toggles, so running `Shift+↓` over a marked stretch unmarks it.
- A page runs from the cursor up to the entry a page jump would land on, and
  the cursor moves onto that entry without marking it. Holding the key
  therefore marks the whole listing.
- The entry under the cursor decides for the whole page: if it is unmarked,
  the page is marked; if it is marked, the page is unmarked.
- `..` is never marked.
- The marks stay while the cursor moves and through `Ctrl+R`. They are
  dropped when the panel changes directory.

## Marking by pattern

| Key | Alternative | Action |
|-----|-------------|--------|
| `+` | | Mark the entries a pattern matches |
| `-` | | Unmark the entries a pattern matches |
| `Alt+E` | | Mark the entries with the extension under the cursor |
| `Alt+Shift+E` | | Unmark the entries with that extension |
| `Alt+N` | | Mark the entries with the name under the cursor |
| `Alt+Shift+N` | | Unmark the entries with that name |
| `Alt+A` | | Mark every file |
| `Alt+Shift+A` | | Unmark every file |
| `Alt+I` | `*` | Invert the marks |

- A pattern is a wildcard such as `*.fs`; `*` stands for any text and `?` for
  one character. Case is ignored, and `\` before `*` or `?` matches that
  character itself.
- The extension is what follows the last dot: `archive.tar.gz` has the
  extension `gz` and the name `archive.tar`. A name without a dot, or with a
  dot only in front, has no extension, so `readme`, `Makefile` and a directory
  `src` go together.
- Directories take part in the extension and the name keys. With `..` under
  the cursor these keys do nothing.
- `Alt+A` and `Alt+Shift+A` leave the marks of directories as they were.
  Inverting includes directories.
- All of them add to and take from the same marks, so `*.txt` followed by `*`
  marks everything but the text files.
- The pattern field remembers what you typed, see
  [History](13-history.md#fields-that-remember).

## Taking marks back

| Key | Alternative | Action |
|-----|-------------|--------|
| `Alt+M` | | Undo the last marking command; again to redo it |

- Each panel remembers one step.
- Moving the cursor, the quick search and `Tab` keep that step. Any other
  command forgets it, a change of directory and a file operation included.
- Entries that are gone by then are left out.

> [!NOTE]
> The grey `+`, `-` and `*` of the numeric keypad arrive as the keys of the
> main keyboard. With `Ctrl`, `Alt` or `Shift` held, no terminal measured
> delivers them at all, so use the keys above.
