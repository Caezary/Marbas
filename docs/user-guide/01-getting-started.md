# Getting started

Marbas shows two panels side by side, each listing a directory. One panel is
active at a time: commands act on its marked entries, or on the entry under
its cursor, and copy or move into the directory the other panel shows.

## Installing

Download the binary for your platform from the releases page of the project
on GitHub and run it. There is one for Windows and one for Linux, each with
its own .NET runtime inside, so nothing else needs to be installed.

- On Linux, mark the file executable first: `chmod +x Marbas`.
- `Marbas --version` prints the version and exits. The other switch is in
  [The command line and the environment](14-command-line-and-environment.md).

## Keys everywhere

| Key | Alternative | Action |
|-----|-------------|--------|
| `F1` | | Open the help at what was done last |
| `Tab` | | Switch to the other panel |
| `F10` | `Ctrl+Q` | Quit and save the session |
| `Esc` | | Close a popup, changing nothing |

- Quitting saves every tab and where its panels stand; the next start opens
  them again. See [Settings](12-settings.md#the-session).
- A mouse click on an entry moves the cursor there and makes its panel active.
  A click marks nothing.

## The screen

- The top border of a panel is its title: the place it shows, its sort order
  and whether hidden entries are shown, such as `C:\work [name ↑ hidden]`. The
  active panel has a double border.
- The top right corner says which tab is in front and how many there are,
  such as `[2/3]`.
- The listing has a name, a size and a date column. `..` comes first, then the
  directories, then the files.
- The line under the listing counts what is marked and adds up its size. With
  nothing marked it counts the directory: `3 files, 2 folders   1,234 bytes`.
- The status line under the panels describes the entry under the cursor of the
  active panel: its whole name, its exact size and its date. A link adds where
  it points: `twin -> C:\data\docs`.
- The bottom row is the key bar.

## Colours

- Directories and `..` are white, files bright blue, archives bright magenta,
  programs bright green and links bright cyan.
- Marked entries, and the line counting them, are yellow.
- Every colour is one of the sixteen ANSI colours, so a terminal with a small
  palette shows them as they are.

## The key bar

The bar has one slot for each function key, `F1` to `F12`, sharing the width
of the terminal. A slot shows the key and its command, `F5 Copy`, and a click
on it runs the command.

- Holding `Ctrl`, `Alt` or `Shift` changes the commands to the ones of that
  modifier: with `Ctrl`, `F3 Name` … `F6 Size`; with `Alt`, `F1 Left`,
  `F2 Right`, `F6 Link`; with `Shift`, `F4 NewFile`, `F8 Erase`. The key names
  stay where they are.
- Below about 90 columns the longer commands are cut to fit.
- Keys that are not function keys, such as `Alt+H` or `Ctrl+R`, are not on
  the bar.

> [!NOTE]
> The bar follows a held modifier on Windows only. Elsewhere it always shows
> the commands without a modifier.

## The help

`F1` opens this guide full screen. In a panel it opens at the section of the
command run last in the session, or at the list of chapters before the first;
moving the cursor and the quick search are not commands. In the viewer it
opens at [The viewer](11-viewer.md), and in a popup at the section about that
popup.

| Key | Alternative | Action |
|-----|-------------|--------|
| `↑` `↓` `PgUp` `PgDn` `Home` `End` | | Scroll, as in the viewer |
| `Tab` | | Choose the next link on screen |
| `Shift+Tab` | | Choose the previous link on screen |
| `Enter` | | Follow the chosen link |
| `Backspace` | `Alt+←` | Back to where the link was followed |
| `F7` | | Search the chapter on screen |
| `Shift+F7` | | Find the next match without asking |
| `Esc` | `F10` | Close the help |

- A link is green and underlined, the chosen link black on cyan.
- Opened at a section, the help goes back from it to the list of chapters.
- `F7` asks as the viewer does, with the same options, and offers the texts
  searched for since the help opened. A search looks at the lines as they stand on
  screen, so words broken over two lines are not found. After a link is
  followed, the next search starts in the new chapter.
- A popup stays open under the help and is there again when the help closes.
- The same chapters are the files of `docs/user-guide` in the project on
  GitHub.

## When something goes wrong

If Marbas stops on an error, it writes what happened to `marbas.log` and,
once the terminal is back, says where that file is. That file is what to
attach to a bug report. Where it is, and how `--debug` makes it record more,
is in [The files Marbas keeps](15-files-marbas-keeps.md#the-log).
