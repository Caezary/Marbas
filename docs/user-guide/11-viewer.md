# The viewer

`F3` opens the file under the cursor full screen, read only, as text or as a
hex dump. A file of any size opens at once, since only the rows on screen are
read.

| Key | Alternative | Action |
|-----|-------------|--------|
| `F3` | | View the file under the cursor |

In the viewer:

| Key | Alternative | Action |
|-----|-------------|--------|
| `↑` `↓` | | One line up or down |
| `PgUp` `PgDn` | | One page up or down |
| `Home` | `Ctrl+Home` | The start of the file |
| `End` | `Ctrl+End` | The end of the file |
| `←` `→` | | Across a long line |
| `Ctrl+←` `Ctrl+→` | | Across a long line, a screen at a time |
| `F2` | | Wrap or unwrap long lines |
| `F4` | | Switch between text and the hex dump |
| `F7` | | Search |
| `Shift+F7` | | Find the next match without asking |
| `F8` | | Read the file in another code page |
| `F1` | | Open this chapter in the help |
| `Esc` | `F3` `F10` | Close the viewer |

- `F3` views the entry under the cursor only, never the marks. On a directory
  it measures the directory instead, see
  [Panels](02-panels.md#sizes).
- The top row names the file, the mode, the code page, the size, the line on
  top of the screen and how far into the file that is.
- The code page is detected from a byte order mark, the zero bytes of UTF-16,
  or whether the bytes are valid UTF-8; otherwise the file is read as Latin-1.
- `F4` keeps the place in the file. A row of the hex dump is a ten-digit
  offset, sixteen bytes with a line after the eighth, and the same sixteen as
  characters; it fits 80 columns.
- A file inside an archive is read as it scrolls, and asks for the archive's
  password once.
- A file viewed is a row of the *View history* tab of
  [History](13-history.md#the-history-popup).

## Searching

`F7` asks for the text and four options: *Case sensitive*, *Whole words*,
*Regular expression* and *Backwards*.

- The search runs forward from the line on top of the screen, or with
  *Backwards* up from the last line on screen. A match never spans two lines.
- *Whole words* takes a match with no letter, digit or `_` on either side.
- With *Regular expression* the text is a .NET regular expression, matched
  against one line at a time, so `^` and `$` are the ends of the line. *Whole
  words* is then greyed out; use `\b`.
- Look-arounds, back-references and atomic groups are not available. A
  pattern that cannot be used is reported, and the popup stays open to fix it.
- The match is drawn black on cyan and scrolled into view. Reaching the end or
  the start of the file without a match is reported.
- A long search shows its progress, and `Esc` stops it.
- `Shift+F7` goes on from the match on screen in the same direction. Before
  the first search it opens the popup.
- `F7` again opens on the last text and options, until the viewer closes. The
  field remembers the texts searched for, the same list the text of
  [Finding files](09-finding-files.md) keeps.

### Searching the hex dump

- In the hex dump `F7` looks for bytes, typed as hexadecimal digits: `4D 5A`
  or `4D5A`. A single digit is a byte of its own.
- Only *Backwards* applies. The field keeps a list of its own.
- The bytes found are highlighted in both columns.
- Text and hex keep a last search each.

## Code pages

`F8` lists the code pages: *Auto*, named for what was detected, then UTF-8,
UTF-16 LE, UTF-16 BE, Latin-1, Windows-1250, CP852, ISO-8859-2, Windows-1252
and CP437.

| Key | Alternative | Action |
|-----|-------------|--------|
| `Enter` | | Read the file in the code page under the cursor |
| `Esc` | | Close the list, changing nothing |

- The file is read again at the same place, and the top row names the code
  page.
- The choice lasts until the viewer closes. The next `F3` detects the code
  page again.
